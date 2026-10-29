using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SecureCredentials;
using Xunit;

namespace SecureCredentials.Tests;

/// <summary>
/// Pruebas de regresión de la política de almacenamiento Linux.
/// Distinguen directorios de documentos cifrados de directorios privados
/// destinados a claves. No modifican archivos reales del usuario.
/// </summary>
public sealed class CredentialStorageSecurityTests
{
    private const UnixFileMode PrivateDirectory =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode NormalDirectory = PrivateDirectory |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    private const UnixFileMode SharedDirectory = NormalDirectory |
        UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PublicFile = PrivateFile |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    /// <summary>Un enlace simbólico no puede utilizarse para leer un documento cifrado.</summary>
    [Fact]
    public async Task Load_RejectsSymbolicLink()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var area = new TemporaryArea();
        string original = Path.Combine(area.Path, "original.enc");
        string link = Path.Combine(area.Path, "enlace.enc");
        await CredentialFileStorage.SaveAsync(original, Sample());
        File.CreateSymbolicLink(link, original);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CredentialFileStorage.LoadAsync(link));
    }

    /// <summary>Un enlace simbólico preexistente no puede ser destino de guardado.</summary>
    [Fact]
    public async Task Save_RejectsSymbolicLinkWithoutChangingTarget()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var area = new TemporaryArea();
        string original = Path.Combine(area.Path, "original.enc");
        string link = Path.Combine(area.Path, "enlace.enc");
        await CredentialFileStorage.SaveAsync(original, Sample());
        byte[] before = await File.ReadAllBytesAsync(original);
        File.CreateSymbolicLink(link, original);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CredentialFileStorage.SaveAsync(link, Sample()));
        Assert.Equal(before, await File.ReadAllBytesAsync(original));
    }

    /// <summary>Un archivo cifrado con permisos 644 no se considera privado.</summary>
    [Fact]
    public async Task Load_RejectsFileWithPermissions644()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var area = new TemporaryArea();
        string file = Path.Combine(area.Path, "credenciales.enc");
        await CredentialFileStorage.SaveAsync(file, Sample());
        File.SetUnixFileMode(file, PublicFile);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CredentialFileStorage.LoadAsync(file));
    }

    /// <summary>Un directorio 777 es modificable por terceros y debe rechazarse.</summary>
    [Fact]
    public async Task Save_RejectsDirectoryWithPermissions777()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var area = new TemporaryArea();
        string folder = Path.Combine(area.Path, "compartido");
        Directory.CreateDirectory(folder);
        File.SetUnixFileMode(folder, SharedDirectory);
        string file = Path.Combine(folder, "credenciales.enc");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CredentialFileStorage.SaveAsync(file, Sample()));
        Assert.False(File.Exists(file));
    }

    /// <summary>CreatePrivate conserva su contrato estricto de permisos 700.</summary>
    [Fact]
    public void CreatePrivate_Rejects755AndAccepts700()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var area = new TemporaryArea();
        string folder = Path.Combine(area.Path, "claves");
        Directory.CreateDirectory(folder);
        File.SetUnixFileMode(folder, NormalDirectory);

        Assert.Throws<UnauthorizedAccessException>(
            () => CredentialDirectory.CreatePrivate(folder));

        File.SetUnixFileMode(folder, PrivateDirectory);
        CredentialDirectory.CreatePrivate(folder);
        Assert.Equal(PrivateDirectory, File.GetUnixFileMode(folder));
    }

    /// <summary>
    /// Un fallo de validación previo a la escritura no deja archivos temporales.
    /// NOTA: no simula un fallo POSIX durante WriteAllBytes/RenameFileAt;
    /// esa variante requiere inyección de fallos mediante ILinuxSecureWriteOperations.
    /// </summary>
    [Fact]
    public async Task Save_WhenValidationFails_DoesNotLeaveTemporaryFiles()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var area = new TemporaryArea();
        string folder = Path.Combine(area.Path, "documentos");
        Directory.CreateDirectory(folder);
        File.SetUnixFileMode(folder, NormalDirectory);
        string destination = Path.Combine(folder, "credenciales.enc");
        Directory.CreateDirectory(destination); // Un directorio no es un destino regular válido.

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CredentialFileStorage.SaveAsync(destination, Sample()));
        Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    /// <summary>
    /// Simula un fallo POSIX después de crear el archivo temporal, durante
    /// WriteAllBytes. Verifica que se elimina el temporal, se cierran ambos
    /// descriptores y no se crea ni modifica el archivo de destino.
    ///
    /// Esta prueba complementa el fallo de validación anterior: comprueba
    /// específicamente la limpieza cuando la escritura ya ha comenzado.
    /// </summary>
    [Fact]
    public void Save_WhenPosixWriteFails_RemovesTemporaryAndClosesDescriptors()
    {
        if (!OperatingSystem.IsLinux()) return;

        using var area = new TemporaryArea();
        string folder = Path.Combine(area.Path, "documentos");
        Directory.CreateDirectory(folder);
        File.SetUnixFileMode(folder, NormalDirectory);

        string destination = Path.Combine(folder, "credenciales.enc");
        byte[] content = System.Text.Encoding.UTF8.GetBytes("contenido-cifrado-de-prueba");
        var operations = new FailDuringWriteOperations();

        IOException error = Assert.Throws<IOException>(() =>
            CredentialFileStorage.SaveLinuxSecure(destination, content, operations));

        Assert.Contains("Fallo simulado", error.Message);
        Assert.True(operations.WriteAttempted);
        Assert.True(operations.TemporaryUnlinked);
        Assert.Equal(2, operations.ClosedDescriptors);
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    /// <summary>
    /// Delegación a las operaciones POSIX reales con un fallo controlado en
    /// WriteAllBytes. La inyección afecta solo a esta instancia de prueba;
    /// no altera el estado global ni la implementación de producción.
    /// </summary>
    private sealed class FailDuringWriteOperations : ILinuxSecureWriteOperations
    {
        private readonly ILinuxSecureWriteOperations _inner = LinuxSecureWriteOperations.Instance;

        public bool WriteAttempted { get; private set; }
        public bool TemporaryUnlinked { get; private set; }
        public int ClosedDescriptors { get; private set; }

        public int OpenDirectory(string path) => _inner.OpenDirectory(path);
        public void ValidateDestinationAt(int directoryFd, string fileName) =>
            _inner.ValidateDestinationAt(directoryFd, fileName);
        public int CreateFileAt(int directoryFd, string fileName) =>
            _inner.CreateFileAt(directoryFd, fileName);

        public void WriteAllBytes(int fileFd, byte[] content)
        {
            WriteAttempted = true;
            // El archivo temporal ya existe, pero forzamos un fallo de E/S.
            throw new IOException("Fallo simulado durante WriteAllBytes.");
        }

        public void SyncFile(int fileFd) => _inner.SyncFile(fileFd);
        public void RenameFileAt(int directoryFd, string sourceFileName, string destinationFileName) =>
            _inner.RenameFileAt(directoryFd, sourceFileName, destinationFileName);

        public void UnlinkFileAt(int directoryFd, string fileName)
        {
            _inner.UnlinkFileAt(directoryFd, fileName);
            TemporaryUnlinked = true;
        }

        public void SyncDirectory(int directoryFd) => _inner.SyncDirectory(directoryFd);

        public void Close(int fd)
        {
            _inner.Close(fd);
            ClosedDescriptors++;
        }
    }

    private static EncryptedData Sample()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        try
        {
            return CredentialEncryptor.Encrypt("{\"usuario\":\"prueba\"}", key, "test-key");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Área temporal privada, aislada y eliminada al finalizar cada prueba.</summary>
    private sealed class TemporaryArea : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "securecredentials-tests-" + Guid.NewGuid().ToString("N"));

        public TemporaryArea()
        {
            Directory.CreateDirectory(Path);
            // La construcción de esta clase también es analizada en Windows.
            // Proteger explícitamente la llamada elimina CA1416.
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(Path, PrivateDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
