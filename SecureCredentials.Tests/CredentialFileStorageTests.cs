
using System.Security.Cryptography;
using SecureCredentials;
using Xunit;

namespace SecureCredentials.Tests;

public class CredentialFileStorageTests
{
    [Fact]
    public async Task SaveLoad_Version2_RestoresOriginalText()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");
        // Crear el directorio temporal con permisos privados.
        CredentialDirectory.CreatePrivate(directory);

        const string originalText =
            "{\"usuario\":\"prueba\",\"password\":\"ficticia\"}";

        try
        {
            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                originalText,
                key,
                "test-key-001");

            // Guardar el archivo cifrado.
            await CredentialFileStorage.SaveAsync(
                filePath,
                encrypted);

            Assert.True(File.Exists(filePath));

            // Recuperar el archivo.
            EncryptedData loaded =
                await CredentialFileStorage.LoadAsync(filePath);

            Assert.Equal(2, loaded.Version);
            Assert.Equal("test-key-001", loaded.KeyId);

            // Descifrar y comprobar el contenido.
            string recovered = CredentialDecryptor.Decrypt(
                loaded,
                key);

            Assert.Equal(originalText, recovered);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // PRUEBA 7: Verificar permisos 600 en Linux.
    [Fact]
    public async Task SaveAsync_OnLinux_CreatesFileWithOwnerOnlyPermissions()
    {
        if (!OperatingSystem.IsLinux())
            return;

        byte[] key = RandomNumberGenerator.GetBytes(32);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(directory, "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                "credenciales ficticias",
                key,
                "test-key-001");

            await CredentialFileStorage.SaveAsync(filePath, encrypted);

            UnixFileMode permissions = File.GetUnixFileMode(filePath);

            UnixFileMode expected =
                UnixFileMode.UserRead | UnixFileMode.UserWrite;

            Assert.Equal(expected, permissions);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    // PRUEBA 8: Crear un directorio privado en Linux.
    [Fact]
    public void PrivateDirectory_OnLinux_HasOwnerOnlyPermissions()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);

            // Establecer explícitamente permisos 700.
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);

            UnixFileMode permissions =
                File.GetUnixFileMode(directory);

            UnixFileMode expected =
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute;

            Assert.Equal(expected, permissions);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 9: CredentialDirectory crea directorios privados.
    [Fact]
    public void CreatePrivate_OnLinux_CreatesDirectoryWith700Permissions()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            // Crear el directorio mediante nuestra biblioteca.
            CredentialDirectory.CreatePrivate(directory);

            // Comprobar que existe.
            Assert.True(Directory.Exists(directory));

            // Consultar los permisos reales.
            UnixFileMode permissions =
                File.GetUnixFileMode(directory);

            UnixFileMode expected =
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute;

            Assert.Equal(expected, permissions);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 10: Rechazar un directorio existente inseguro.
    [Fact]
    public void CreatePrivate_OnLinux_RejectsExistingInsecureDirectory()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);

            // Simular un directorio con permisos inseguros: 755.
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);

            // La biblioteca debe rechazarlo.
            Assert.Throws<UnauthorizedAccessException>(() =>
                CredentialDirectory.CreatePrivate(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 11: Aceptar un directorio existente con permisos 700.
    [Fact]
    public void CreatePrivate_OnLinux_AcceptsExistingPrivateDirectory()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);

            // Preparar un directorio existente con permisos 700.
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);

            // No debe lanzar ninguna excepción.
            CredentialDirectory.CreatePrivate(directory);

            Assert.True(Directory.Exists(directory));

            UnixFileMode expected =
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute;

            Assert.Equal(
                expected,
                File.GetUnixFileMode(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 12: Rechazar enlaces simbólicos a directorios.
    [Fact]
    public void CreatePrivate_OnLinux_RejectsSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string realDirectory = Path.Combine(root, "real");
        string symbolicLink = Path.Combine(root, "link");

        try
        {
            Directory.CreateDirectory(root);

            // Crear el directorio real con permisos privados.
            CredentialDirectory.CreatePrivate(realDirectory);

            // Crear un enlace simbólico hacia ese directorio.
            Directory.CreateSymbolicLink(
                symbolicLink,
                realDirectory);

            // La biblioteca debe rechazar el enlace simbólico.
            Assert.Throws<UnauthorizedAccessException>(() =>
                CredentialDirectory.CreatePrivate(symbolicLink));
        }
        finally
        {
            // Eliminar primero el enlace, sin seguir su destino.
            if (File.Exists(symbolicLink) ||
                Directory.Exists(symbolicLink))
            {
                Directory.Delete(symbolicLink);
            }

            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }


    // PRUEBA 13: Rechazar enlaces simbólicos intermedios.
    [Fact]
    public void CreatePrivate_OnLinux_RejectsIntermediateSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string realDirectory = Path.Combine(root, "real");
        string symbolicLink = Path.Combine(root, "link");

        string credentialsDirectory =
            Path.Combine(symbolicLink, "credenciales");

        try
        {
            Directory.CreateDirectory(root);

            CredentialDirectory.CreatePrivate(realDirectory);

            Directory.CreateSymbolicLink(
                symbolicLink,
                realDirectory);

            // No debe permitirse atravesar el enlace simbólico.
            Assert.Throws<UnauthorizedAccessException>(() =>
                CredentialDirectory.CreatePrivate(
                    credentialsDirectory));
        }
        finally
        {
            // Eliminar primero el enlace simbólico.
            if (Directory.Exists(symbolicLink))
                Directory.Delete(symbolicLink);

            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }


    // PRUEBA 16: Rechazar un directorio de otro propietario.
    [Fact]
    public void CreatePrivate_OnLinux_RejectsDirectoryOwnedByAnotherUser()
    {
        if (!OperatingSystem.IsLinux())
            return;

        const string directory = "/root";

        uint effectiveUid =
            LinuxUserIdentity.GetEffectiveUserId();

        uint ownerUid =
            LinuxFileOwnership.GetOwnerUserId(directory);

        // La prueba solo tiene sentido si los UID son diferentes.
        if (effectiveUid == ownerUid)
            return;

        Assert.Throws<UnauthorizedAccessException>(() =>
            CredentialDirectory.CreatePrivate(directory));
    }


    // PRUEBA 19: Rechazar un archivo como directorio.
    [Fact]
    public void CreatePrivate_OnLinux_RejectsRegularFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(root, "archivo.txt");

        try
        {
            Directory.CreateDirectory(root);

            // Crear un archivo normal.
            File.WriteAllText(filePath, "Contenido de prueba");

            // Debe rechazarse como directorio.
            Assert.ThrowsAny<IOException>(() =>
                CredentialDirectory.CreatePrivate(filePath));

            // El archivo debe permanecer intacto.
            Assert.True(File.Exists(filePath));

            Assert.Equal(
                "Contenido de prueba",
                File.ReadAllText(filePath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }


    // PRUEBA 20: Rechazar rutas vacías o inválidas.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void CreatePrivate_WithEmptyOrWhitespacePath_Throws(
        string path)
    {
        Assert.Throws<ArgumentException>(() =>
            CredentialDirectory.CreatePrivate(path));
    }


    // PRUEBA 23: Rechazar credenciales con permisos inseguros.
    [Fact]
    public async Task LoadAsync_OnLinux_RejectsFileWithInsecurePermissions()
    {
        if (!OperatingSystem.IsLinux())
            return;

        byte[] key = RandomNumberGenerator.GetBytes(32);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(directory, "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                "credenciales ficticias",
                key,
                "test-key-001");

            await CredentialFileStorage.SaveAsync(filePath, encrypted);

            // Simular una modificación insegura de los permisos.
            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.GroupRead |
                UnixFileMode.OtherRead);

            // La lectura debería rechazar el archivo.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.LoadAsync(filePath));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 24: Rechazar enlaces simbólicos al cargar.
    [Fact]
    public async Task LoadAsync_OnLinux_RejectsSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string realFile = Path.Combine(
            directory,
            "real.enc");

        string symbolicLink = Path.Combine(
            directory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un archivo real con permisos 600.
            await File.WriteAllTextAsync(
                realFile,
                "contenido ficticio");

            File.SetUnixFileMode(
                realFile,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            // Crear un enlace simbólico al archivo real.
            File.CreateSymbolicLink(
                symbolicLink,
                realFile);

            // La biblioteca debe rechazar el enlace.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.LoadAsync(symbolicLink));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 25: Rechazar enlaces simbólicos al guardar.
    [Fact]
    public async Task SaveAsync_OnLinux_RejectsSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        byte[] key = RandomNumberGenerator.GetBytes(32);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string realFile = Path.Combine(directory, "real.enc");
        string symbolicLink = Path.Combine(
            directory, "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            await File.WriteAllTextAsync(
                realFile,
                "contenido original");

            File.CreateSymbolicLink(
                symbolicLink,
                realFile);

            EncryptedData encrypted =
                CredentialEncryptor.Encrypt(
                    "credenciales ficticias",
                    key,
                    "test-key-001");

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveAsync(
                    symbolicLink,
                    encrypted));

            // El archivo original debe permanecer intacto.
            Assert.Equal(
                "contenido original",
                await File.ReadAllTextAsync(realFile));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 26: Rechazar sobrescritura de archivo inseguro.
    [Fact]
    public async Task SaveAsync_OnLinux_RejectsExistingFileWithInsecurePermissions()
    {
        if (!OperatingSystem.IsLinux())
            return;

        byte[] key = RandomNumberGenerator.GetBytes(32);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un archivo existente con permisos 644.
            await File.WriteAllTextAsync(
                filePath,
                "contenido original");

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.GroupRead |
                UnixFileMode.OtherRead);

            EncryptedData encrypted =
                CredentialEncryptor.Encrypt(
                    "credenciales ficticias",
                    key,
                    "test-key-001");

            // La biblioteca debe rechazar la sobrescritura.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveAsync(
                    filePath,
                    encrypted));

            // El contenido original debe permanecer intacto.
            Assert.Equal(
                "contenido original",
                await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 27: Validar el propietario antes de sobrescribir.
    [Fact]
    public async Task SaveAsync_OnLinux_ValidatesExistingFileOwner()
    {
        if (!OperatingSystem.IsLinux())
            return;

        byte[] key = RandomNumberGenerator.GetBytes(32);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            await File.WriteAllTextAsync(
                filePath,
                "contenido original");

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            EncryptedData encrypted =
                CredentialEncryptor.Encrypt(
                    "credenciales ficticias",
                    key,
                    "test-key-001");

            // Un archivo propio con permisos 600
            // debe poder sobrescribirse correctamente.
            await CredentialFileStorage.SaveAsync(
                filePath,
                encrypted);

            uint effectiveUid =
                LinuxUserIdentity.GetEffectiveUserId();

            uint ownerUid =
                LinuxFileOwnership.GetOwnerUserId(filePath);

            Assert.Equal(effectiveUid, ownerUid);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 28: No dejar temporales si falla el guardado.
    [Fact]
    public async Task SaveAsync_OnLinux_DoesNotLeaveTemporaryFilesWhenValidationFails()
    {
        if (!OperatingSystem.IsLinux())
            return;

        byte[] key = RandomNumberGenerator.GetBytes(32);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un destino con permisos inseguros.
            await File.WriteAllTextAsync(
                filePath,
                "contenido original");

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.GroupRead |
                UnixFileMode.OtherRead);

            EncryptedData encrypted =
                CredentialEncryptor.Encrypt(
                    "credenciales ficticias",
                    key,
                    "test-key-001");

            // El guardado debe ser rechazado.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveAsync(
                    filePath,
                    encrypted));

            // No deben quedar archivos temporales.
            string[] temporaryFiles = Directory.GetFiles(
                directory,
                ".credentials.enc.*.tmp");

            Assert.Empty(temporaryFiles);

            // El destino original debe permanecer intacto.
            Assert.Equal(
                "contenido original",
                await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }



    // PRUEBA 29: Rechazar un directorio como destino
    // sin crear archivos temporales.
    [Fact]
    public async Task SaveAsync_OnLinux_RejectsDirectoryDestinationWithoutCreatingTemporaryFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        byte[] key = RandomNumberGenerator.GetBytes(32);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un directorio donde debería existir
            // el archivo de destino.
            CredentialDirectory.CreatePrivate(destinationPath);

            EncryptedData encrypted =
                CredentialEncryptor.Encrypt(
                    "credenciales ficticias",
                    key,
                    "test-key-001");

            // La implementación POSIX debe rechazar
            // el destino antes de crear el temporal.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveAsync(
                    destinationPath,
                    encrypted));

            // No deben quedar archivos temporales.
            string[] temporaryFiles = Directory.GetFiles(
                directory,
                ".credentials.enc.*.tmp");

            Assert.Empty(temporaryFiles);

            // El directorio de destino debe seguir existiendo.
            Assert.True(Directory.Exists(destinationPath));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }



    // PRUEBA 30: Rechazar carga de archivo inexistente.
    [Fact]
    public async Task LoadAsync_OnLinux_ThrowsWhenFileDoesNotExist()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // El archivo de credenciales no existe.
            Assert.False(File.Exists(filePath));

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => CredentialFileStorage.LoadAsync(filePath));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 31: Rechazar enlaces simbólicos rotos.
    [Fact]
    public async Task LoadAsync_OnLinux_RejectsBrokenSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string missingTarget = Path.Combine(
            directory,
            "missing.enc");

        string symbolicLink = Path.Combine(
            directory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un enlace a un archivo inexistente.
            File.CreateSymbolicLink(
                symbolicLink,
                missingTarget);

            // Verificar que el destino del enlace no existe.
            Assert.False(File.Exists(missingTarget));

            // Verificar que la ruta creada es un enlace simbólico.
            var linkInfo = new FileInfo(symbolicLink);

            Assert.NotNull(linkInfo.LinkTarget);

            // Debe rechazarse como enlace simbólico,
            // aunque el destino no exista.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.LoadAsync(
                    symbolicLink));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 32: Rechazar enlaces simbólicos rotos al guardar.
    [Fact]
    public async Task SaveAsync_OnLinux_RejectsBrokenSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        byte[] key = RandomNumberGenerator.GetBytes(32);

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string missingTarget = Path.Combine(
            directory,
            "missing.enc");

        string symbolicLink = Path.Combine(
            directory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un enlace simbólico cuyo destino no existe.
            File.CreateSymbolicLink(
                symbolicLink,
                missingTarget);

            // Comprobar que el destino no existe.
            Assert.False(File.Exists(missingTarget));

            // Comprobar que se ha creado un enlace simbólico.
            var linkInfo = new FileInfo(symbolicLink);

            Assert.NotNull(linkInfo.LinkTarget);

            EncryptedData encrypted =
                CredentialEncryptor.Encrypt(
                    "credenciales ficticias",
                    key,
                    "test-key-001");

            // SaveAsync debe rechazar el enlace simbólico.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveAsync(
                    symbolicLink,
                    encrypted));

            // El destino original debe seguir sin existir.
            Assert.False(File.Exists(missingTarget));

            // El enlace simbólico debe permanecer intacto.
            Assert.NotNull(
                new FileInfo(symbolicLink).LinkTarget);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 51:
    // LoadAsync debe rechazar enlaces simbólicos
    // en componentes intermedios de la ruta.
    [Fact]
    public async Task LoadAsync_OnLinux_RejectsIntermediateSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string realDirectory = Path.Combine(root, "real");

        string credentialsDirectory = Path.Combine(
            realDirectory,
            "credentials");

        string symbolicLink = Path.Combine(root, "link");

        string pathThroughLink = Path.Combine(
            symbolicLink,
            "credentials",
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(root);
            CredentialDirectory.CreatePrivate(realDirectory);
            CredentialDirectory.CreatePrivate(
                credentialsDirectory);

            // Crear un archivo de credenciales.
            string realFile = Path.Combine(
                credentialsDirectory,
                "credentials.enc");

            await File.WriteAllTextAsync(
                realFile,
                "{}");

            File.SetUnixFileMode(
                realFile,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            // Crear un enlace simbólico intermedio.
            Directory.CreateSymbolicLink(
                symbolicLink,
                realDirectory);

            // La carga debe rechazar la ruta.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.LoadAsync(
                    pathThroughLink));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
   

    [Fact]
    public async Task LoadAsync_OnLinux_RejectsNamedPipe()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.pipe");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            int result = Mono.Unix.Native.Syscall.mkfifo(
                filePath,
                Mono.Unix.Native.FilePermissions.S_IRUSR |
                Mono.Unix.Native.FilePermissions.S_IWUSR);

            Assert.Equal(0, result);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.LoadAsync(filePath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }


    [Fact]
    public async Task LoadAsync_OnLinux_RejectsFileLargerThanLimit()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Superar en un byte el límite de 1 MiB.
            byte[] oversizedContent = new byte[1024 * 1024 + 1];

            await File.WriteAllBytesAsync(
                filePath,
                oversizedContent);

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => CredentialFileStorage.LoadAsync(filePath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }


    
    [Fact]
    public async Task SaveAsync_OnLinux_RejectsIntermediateSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string realDirectory = Path.Combine(root, "real");
        string symbolicLink = Path.Combine(root, "link");

        string filePath = Path.Combine(
            symbolicLink,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(root);
            CredentialDirectory.CreatePrivate(realDirectory);

            // Crear un enlace simbólico intermedio.
            Directory.CreateSymbolicLink(
                symbolicLink,
                realDirectory);

            // Datos de prueba con la estructura real de EncryptedData.
            var encrypted = new EncryptedData
            {
                Version = 1,
                Algorithm = "AES-256-GCM",
                KeyId = "test-key",
                Nonce = Convert.ToBase64String(new byte[12]),
                Ciphertext = Convert.ToBase64String(new byte[32]),
                Tag = Convert.ToBase64String(new byte[16])
            };

            // SaveAsync no debe permitir escribir a través
            // de un enlace simbólico intermedio.
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveAsync(
                    filePath,
                    encrypted));

            // Verificar que no se ha escrito en el directorio real.
            Assert.False(
                File.Exists(
                    Path.Combine(
                        realDirectory,
                        "credentials.enc")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }


    [Fact]
    public async Task SaveAsync_OnLinux_RejectsExistingNamedPipe()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.pipe");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            int result = Mono.Unix.Native.Syscall.mkfifo(
                filePath,
                Mono.Unix.Native.FilePermissions.S_IRUSR |
                Mono.Unix.Native.FilePermissions.S_IWUSR);

            Assert.Equal(0, result);

            var encrypted = new EncryptedData
            {
                Version = 1,
                Algorithm = "AES-256-GCM",
                KeyId = "test-key",
                Nonce = Convert.ToBase64String(new byte[12]),
                Ciphertext = Convert.ToBase64String(new byte[32]),
                Tag = Convert.ToBase64String(new byte[16])
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveAsync(
                    filePath,
                    encrypted));

            // El objeto original debe seguir siendo una FIFO.
            Assert.Equal(
                Mono.Unix.Native.FilePermissions.S_IFIFO,
                GetFileType(filePath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        static Mono.Unix.Native.FilePermissions GetFileType(
            string path)
        {
            int result = Mono.Unix.Native.Syscall.lstat(
                path,
                out Mono.Unix.Native.Stat stat);

            Assert.Equal(0, result);

            return stat.st_mode &
                Mono.Unix.Native.FilePermissions.S_IFMT;
        }
    }


    [Fact]
    public void CreateFileAt_OnLinux_CreatesPrivateFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        CredentialDirectory.CreatePrivate(directory);

        int directoryFd = -1;
        int fileFd = -1;

        try
        {
            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(directory);

            fileFd = LinuxSecureFileOperations.CreateFileAt(
                directoryFd,
                "credentials.tmp");

            string filePath = Path.Combine(
                directory,
                "credentials.tmp");

            Assert.True(File.Exists(filePath));

            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(filePath));
        }
        finally
        {
            if (fileFd >= 0)
                Mono.Unix.Native.Syscall.close(fileFd);

            if (directoryFd >= 0)
                Mono.Unix.Native.Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    [Fact]
    public void CreateFileAt_OnLinux_RejectsExistingFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.tmp");

        int directoryFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un archivo que no debe sobrescribirse.
            File.WriteAllText(filePath, "CONTENIDO ORIGINAL");

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(directory);

            // O_EXCL debe impedir abrir un archivo existente
            // para su sustitución.
            Assert.Throws<UnauthorizedAccessException>(
                () => LinuxSecureFileOperations.CreateFileAt(
                    directoryFd,
                    "credentials.tmp"));

            // Comprobar que el contenido sigue intacto.
            Assert.Equal(
                "CONTENIDO ORIGINAL",
                File.ReadAllText(filePath));
        }
        finally
        {
            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }


    [Fact]
    public void WriteAllBytes_OnLinux_WritesCompleteContent()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.tmp");

        int directoryFd = -1;
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(directory);

            fileFd =
                LinuxSecureFileOperations.CreateFileAt(
                    directoryFd,
                    "credentials.tmp");

            byte[] expected = new byte[128 * 1024];

            RandomNumberGenerator.Fill(expected);

            // Escribir utilizando exclusivamente el descriptor.
            LinuxSecureFileOperations.WriteAllBytes(
                fileFd,
                expected);

            // Leer desde el mismo descriptor requiere O_RDWR
            // o volver a abrirlo. Para esta prueba verificamos
            // el resultado después de cerrar el descriptor.
            Mono.Unix.Native.Syscall.close(fileFd);
            fileFd = -1;

            byte[] actual = File.ReadAllBytes(filePath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (fileFd >= 0)
                Mono.Unix.Native.Syscall.close(fileFd);

            if (directoryFd >= 0)
                Mono.Unix.Native.Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    [Fact]
    public void SyncFile_OnLinux_FlushesWrittenData()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.tmp");

        int directoryFd = -1;
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(directory);

            fileFd =
                LinuxSecureFileOperations.CreateFileAt(
                    directoryFd,
                    "credentials.tmp");

            byte[] expected =
                System.Text.Encoding.UTF8.GetBytes(
                    "Datos de prueba para fsync");

            LinuxSecureFileOperations.WriteAllBytes(
                fileFd,
                expected);

            // Solicitar la sincronización de los datos
            // utilizando el descriptor POSIX.
            LinuxSecureFileOperations.SyncFile(fileFd);

            Assert.Equal(
                expected,
                File.ReadAllBytes(filePath));
        }
        finally
        {
            if (fileFd >= 0)
                Mono.Unix.Native.Syscall.close(fileFd);

            if (directoryFd >= 0)
                Mono.Unix.Native.Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    [Fact]
    public void RenameFileAt_OnLinux_RenamesFileWithinDirectory()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string tempPath = Path.Combine(
            directory,
            "credentials.tmp");

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        int directoryFd = -1;
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(directory);

            fileFd =
                LinuxSecureFileOperations.CreateFileAt(
                    directoryFd,
                    "credentials.tmp");

            byte[] expected =
                System.Text.Encoding.UTF8.GetBytes(
                    "Contenido cifrado de prueba");

            LinuxSecureFileOperations.WriteAllBytes(
                fileFd,
                expected);

            LinuxSecureFileOperations.SyncFile(fileFd);

            Mono.Unix.Native.Syscall.close(fileFd);
            fileFd = -1;

            // Renombrar dentro del directorio ya validado.
            LinuxSecureFileOperations.RenameFileAt(
                directoryFd,
                "credentials.tmp",
                "credentials.enc");

            Assert.False(File.Exists(tempPath));
            Assert.True(File.Exists(destinationPath));

            Assert.Equal(
                expected,
                File.ReadAllBytes(destinationPath));

            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(destinationPath));
        }
        finally
        {
            if (fileFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(fileFd);
            }

            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }


    [Fact]
    public void UnlinkFileAt_OnLinux_RemovesFileWithinDirectory()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.tmp");

        int directoryFd = -1;
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(directory);

            fileFd =
                LinuxSecureFileOperations.CreateFileAt(
                    directoryFd,
                    "credentials.tmp");

            byte[] content =
                System.Text.Encoding.UTF8.GetBytes(
                    "Contenido temporal");

            LinuxSecureFileOperations.WriteAllBytes(
                fileFd,
                content);

            // Cerrar el descriptor del archivo antes
            // de eliminar su entrada del directorio.
            Mono.Unix.Native.Syscall.close(fileFd);
            fileFd = -1;

            Assert.True(File.Exists(filePath));

            // Eliminar respecto al descriptor del directorio.
            LinuxSecureFileOperations.UnlinkFileAt(
                directoryFd,
                "credentials.tmp");

            Assert.False(File.Exists(filePath));
        }
        finally
        {
            if (fileFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(fileFd);
            }

            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SyncDirectory_OnLinux_SucceedsAfterRename()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        int directoryFd = -1;
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(directory);

            fileFd =
                LinuxSecureFileOperations.CreateFileAt(
                    directoryFd,
                    "credentials.tmp");

            byte[] expected =
                System.Text.Encoding.UTF8.GetBytes(
                    "Contenido cifrado de prueba");

            LinuxSecureFileOperations.WriteAllBytes(
                fileFd,
                expected);

            LinuxSecureFileOperations.SyncFile(fileFd);

            Mono.Unix.Native.Syscall.close(fileFd);
            fileFd = -1;

            LinuxSecureFileOperations.RenameFileAt(
                directoryFd,
                "credentials.tmp",
                "credentials.enc");

            // Sincronizar el directorio para solicitar
            // la persistencia del cambio de nombre.
            LinuxSecureFileOperations.SyncDirectory(
                directoryFd);

            Assert.True(File.Exists(destinationPath));

            Assert.Equal(
                expected,
                File.ReadAllBytes(destinationPath));
        }
        finally
        {
            if (fileFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(fileFd);
            }

            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }


    [Fact]
    public void CreateFileAt_OnLinux_WorksAfterDirectoryRename()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string originalDirectory = Path.Combine(root, "original");
        string renamedDirectory = Path.Combine(root, "renamed");

        int directoryFd = -1;
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(root);
            CredentialDirectory.CreatePrivate(originalDirectory);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(
                    originalDirectory);

            // Cambiar la ruta después de abrir el descriptor.
            Directory.Move(originalDirectory, renamedDirectory);

            // Crear el archivo utilizando el descriptor original.
            fileFd =
                LinuxSecureFileOperations.CreateFileAt(
                    directoryFd,
                    "credentials.tmp");

            byte[] expected =
                System.Text.Encoding.UTF8.GetBytes(
                    "Escritura mediante descriptor estable");

            LinuxSecureFileOperations.WriteAllBytes(
                fileFd,
                expected);

            LinuxSecureFileOperations.SyncFile(fileFd);

            Assert.False(
                Directory.Exists(originalDirectory));

            Assert.Equal(
                expected,
                File.ReadAllBytes(
                    Path.Combine(
                        renamedDirectory,
                        "credentials.tmp")));
        }
        finally
        {
            if (fileFd >= 0)
                Mono.Unix.Native.Syscall.close(fileFd);

            if (directoryFd >= 0)
                Mono.Unix.Native.Syscall.close(directoryFd);

            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public void ValidateDestinationAt_OnLinux_AllowsMissingFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        int directoryFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(directory);

            // El archivo todavía no existe.
            Assert.False(
                File.Exists(
                    Path.Combine(directory, "credentials.enc")));

            // Debe aceptar un destino inexistente.
            LinuxSecureFileOperations.ValidateDestinationAt(
                directoryFd,
                "credentials.enc");
        }
        finally
        {
            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }


    [Fact]
    public void ValidateDestinationAt_OnLinux_RejectsSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string targetPath = Path.Combine(
            directory,
            "target.enc");

        string linkPath = Path.Combine(
            directory,
            "credentials.enc");

        int directoryFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un archivo regular privado.
            File.WriteAllText(targetPath, "Contenido original");

            File.SetUnixFileMode(
                targetPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            // Crear un enlace simbólico hacia el archivo.
            File.CreateSymbolicLink(
                linkPath,
                targetPath);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(
                    directory);

            // La validación debe rechazar el enlace simbólico,
            // aunque su destino sea un archivo regular válido.
            Assert.Throws<UnauthorizedAccessException>(() =>
                LinuxSecureFileOperations.ValidateDestinationAt(
                    directoryFd,
                    "credentials.enc"));

            // Comprobar que el archivo original no se modifica.
            Assert.Equal(
                "Contenido original",
                File.ReadAllText(targetPath));
        }
        finally
        {
            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }


    [Fact]
    public void ValidateDestinationAt_OnLinux_RejectsInsecurePermissions()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        int directoryFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            File.WriteAllText(
                filePath,
                "Contenido de prueba");

            // Permisos 644: lectura para grupo y otros.
            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.GroupRead |
                UnixFileMode.OtherRead);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(
                    directory);

            Assert.Throws<UnauthorizedAccessException>(() =>
                LinuxSecureFileOperations.ValidateDestinationAt(
                    directoryFd,
                    "credentials.enc"));

            // La validación no debe alterar el contenido.
            Assert.Equal(
                "Contenido de prueba",
                File.ReadAllText(filePath));
        }
        finally
        {
            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ValidateDestinationAt_OnLinux_RejectsNamedPipe()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string pipePath = Path.Combine(
            directory,
            "credentials.enc");

        int directoryFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear una tubería con nombre (FIFO).
            int result = Mono.Unix.Native.Syscall.mkfifo(
                pipePath,
                Mono.Unix.Native.FilePermissions.S_IRUSR |
                Mono.Unix.Native.FilePermissions.S_IWUSR);

            Assert.Equal(0, result);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(
                    directory);

            // Una FIFO no es un archivo regular.
            Assert.Throws<UnauthorizedAccessException>(() =>
                LinuxSecureFileOperations.ValidateDestinationAt(
                    directoryFd,
                    "credentials.enc"));

            // La validación no debe eliminar el objeto.
            Assert.True(File.Exists(pipePath));
        }
        finally
        {
            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ValidateDestinationAt_OnLinux_AcceptsPrivateRegularFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        int directoryFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            File.WriteAllText(
                filePath,
                "Contenido cifrado de prueba");

            // Establecer permisos privados 600.
            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(
                    directory);

            // No debe lanzar ninguna excepción.
            LinuxSecureFileOperations.ValidateDestinationAt(
                directoryFd,
                "credentials.enc");

            // La validación no debe modificar el archivo.
            Assert.Equal(
                "Contenido cifrado de prueba",
                File.ReadAllText(filePath));

            Assert.Equal(
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite,
                File.GetUnixFileMode(filePath));
        }
        finally
        {
            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ValidateDestinationAt_OnLinux_WorksAfterDirectoryRename()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string originalDirectory =
            Path.Combine(root, "original");

        string renamedDirectory =
            Path.Combine(root, "renamed");

        int directoryFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(root);
            CredentialDirectory.CreatePrivate(originalDirectory);

            string filePath = Path.Combine(
                originalDirectory,
                "credentials.enc");

            File.WriteAllText(
                filePath,
                "Contenido original");

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(
                    originalDirectory);

            // Renombrar el directorio después de abrirlo.
            Directory.Move(
                originalDirectory,
                renamedDirectory);

            Assert.False(
                Directory.Exists(originalDirectory));

            // Debe funcionar mediante el descriptor abierto,
            // aunque la ruta original ya no exista.
            LinuxSecureFileOperations.ValidateDestinationAt(
                directoryFd,
                "credentials.enc");

            Assert.Equal(
                "Contenido original",
                File.ReadAllText(
                    Path.Combine(
                        renamedDirectory,
                        "credentials.enc")));
        }
        finally
        {
            if (directoryFd >= 0)
            {
                Mono.Unix.Native.Syscall.close(directoryFd);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }


    [Fact]
    public async Task SaveAsync_OnLinux_RejectsIntermediateDirectorySymlink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string realDirectory = Path.Combine(root, "real");
        string linkedDirectory = Path.Combine(root, "linked");
        string destinationPath = Path.Combine(
            linkedDirectory,
            "credentials.enc");

        try
        {
            CredentialDirectory.CreatePrivate(root);
            CredentialDirectory.CreatePrivate(realDirectory);

            Directory.CreateSymbolicLink(
                linkedDirectory,
                realDirectory);

            // Utilizar un objeto EncryptedData válido.
            // Sustituir esta construcción por el patrón
            // empleado en las pruebas existentes si difiere.
            EncryptedData encrypted = new EncryptedData
            {
                // Inicializar aquí las propiedades obligatorias.
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveAsync(
                    destinationPath,
                    encrypted));

            Assert.False(
                File.Exists(
                    Path.Combine(
                        realDirectory,
                        "credentials.enc")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RenameFileAt_OnLinux_ReplacesExistingPrivateFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        int directoryFd = -1;
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear el archivo de credenciales anterior.
            File.WriteAllText(
                destinationPath,
                "Credenciales anteriores");

            File.SetUnixFileMode(
                destinationPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            directoryFd =
                LinuxSecureFileOperations.OpenDirectory(directory);

            // Validar el destino existente.
            LinuxSecureFileOperations.ValidateDestinationAt(
                directoryFd,
                "credentials.enc");

            // Crear el nuevo archivo temporal.
            fileFd =
                LinuxSecureFileOperations.CreateFileAt(
                    directoryFd,
                    "credentials.tmp");

            byte[] newContent =
                System.Text.Encoding.UTF8.GetBytes(
                    "Credenciales nuevas");

            LinuxSecureFileOperations.WriteAllBytes(
                fileFd,
                newContent);

            LinuxSecureFileOperations.SyncFile(fileFd);

            Mono.Unix.Native.Syscall.close(fileFd);
            fileFd = -1;

            // Sustituir el archivo anterior.
            LinuxSecureFileOperations.RenameFileAt(
                directoryFd,
                "credentials.tmp",
                "credentials.enc");

            LinuxSecureFileOperations.SyncDirectory(
                directoryFd);

            Assert.Equal(
                "Credenciales nuevas",
                File.ReadAllText(destinationPath));

            Assert.Equal(
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite,
                File.GetUnixFileMode(destinationPath));

            Assert.False(
                File.Exists(
                    Path.Combine(directory, "credentials.tmp")));
        }
        finally
        {
            if (fileFd >= 0)
                Mono.Unix.Native.Syscall.close(fileFd);

            if (directoryFd >= 0)
                Mono.Unix.Native.Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveLinuxSecure_OnLinux_CreatesPrivateFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        byte[] content =
            System.Text.Encoding.UTF8.GetBytes(
                "Contenido cifrado de prueba");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            CredentialFileStorage.SaveLinuxSecure(
                destinationPath,
                content);

            Assert.True(File.Exists(destinationPath));

            Assert.Equal(
                content,
                File.ReadAllBytes(destinationPath));

            Assert.Equal(
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite,
                File.GetUnixFileMode(destinationPath));

            // No deben quedar archivos temporales.
            Assert.Single(
                Directory.GetFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveLinuxSecure_OnLinux_ReplacesExistingPrivateFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        byte[] originalContent =
            System.Text.Encoding.UTF8.GetBytes(
                "Credenciales anteriores");

        byte[] newContent =
            System.Text.Encoding.UTF8.GetBytes(
                "Credenciales actualizadas");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear inicialmente el archivo privado.
            CredentialFileStorage.SaveLinuxSecure(
                destinationPath,
                originalContent);

            // Guardar nuevas credenciales en el mismo destino.
            CredentialFileStorage.SaveLinuxSecure(
                destinationPath,
                newContent);

            // El archivo debe contener los datos nuevos.
            Assert.Equal(
                newContent,
                File.ReadAllBytes(destinationPath));

            // Los permisos deben continuar siendo 600.
            Assert.Equal(
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite,
                File.GetUnixFileMode(destinationPath));

            // No deben quedar archivos temporales.
            Assert.Single(
                Directory.GetFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveLinuxSecure_OnLinux_RejectsInsecureDestinationWithoutModifyingIt()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        byte[] originalContent =
            System.Text.Encoding.UTF8.GetBytes(
                "Credenciales originales");

        byte[] newContent =
            System.Text.Encoding.UTF8.GetBytes(
                "Credenciales nuevas");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            File.WriteAllBytes(
                destinationPath,
                originalContent);

            // Permisos inseguros: 644.
            File.SetUnixFileMode(
                destinationPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.GroupRead |
                UnixFileMode.OtherRead);

            Assert.Throws<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveLinuxSecure(
                    destinationPath,
                    newContent));

            // El contenido original debe permanecer intacto.
            Assert.Equal(
                originalContent,
                File.ReadAllBytes(destinationPath));

            // Los permisos originales no deben alterarse.
            Assert.Equal(
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.GroupRead |
                UnixFileMode.OtherRead,
                File.GetUnixFileMode(destinationPath));

            // No deben haberse creado archivos temporales.
            Assert.Single(
                Directory.GetFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }


    // PRUEBA 79: Rechazar un enlace simbólico como destino
    // sin modificar el archivo al que apunta.
    [Fact]
    public void SaveLinuxSecure_OnLinux_RejectsSymbolicLinkWithoutModifyingTarget()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string targetPath = Path.Combine(
            directory,
            "original.enc");

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        byte[] originalContent =
            System.Text.Encoding.UTF8.GetBytes(
                "Contenido original");

        byte[] newContent =
            System.Text.Encoding.UTF8.GetBytes(
                "Contenido nuevo");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            File.WriteAllBytes(
                targetPath,
                originalContent);

            File.SetUnixFileMode(
                targetPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            File.CreateSymbolicLink(
                destinationPath,
                targetPath);

            Assert.Throws<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveLinuxSecure(
                    destinationPath,
                    newContent));

            // El archivo original no debe modificarse.
            Assert.Equal(
                originalContent,
                File.ReadAllBytes(targetPath));

            // El enlace simbólico debe seguir existiendo.
            Assert.NotNull(
                new FileInfo(destinationPath).LinkTarget);

            // No deben quedar temporales.
            Assert.Empty(
                Directory.GetFiles(
                    directory,
                    ".credentials.enc.*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }


    // PRUEBA 80: Rechazar un FIFO como destino
    // sin sustituirlo ni crear archivos temporales.
    [Fact]
    public void SaveLinuxSecure_OnLinux_RejectsFifoWithoutCreatingTemporaryFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        byte[] content =
            System.Text.Encoding.UTF8.GetBytes(
                "Credenciales nuevas");

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            int result = Mono.Unix.Native.Syscall.mkfifo(
                destinationPath,
                Mono.Unix.Native.FilePermissions.S_IRUSR |
                Mono.Unix.Native.FilePermissions.S_IWUSR);

            Assert.Equal(0, result);

            Assert.Throws<UnauthorizedAccessException>(
                () => CredentialFileStorage.SaveLinuxSecure(
                    destinationPath,
                    content));

            // Comprobar que el FIFO sigue existiendo
            // y no ha sido sustituido por un archivo regular.
            Assert.Equal(
                0,
                Mono.Unix.Native.Syscall.lstat(
                    destinationPath,
                    out Mono.Unix.Native.Stat destinationStat));

            Assert.Equal(
                Mono.Unix.Native.FilePermissions.S_IFIFO,
                destinationStat.st_mode &
                Mono.Unix.Native.FilePermissions.S_IFMT);

            // No deben quedar archivos temporales.
            Assert.Empty(
                Directory.GetFiles(
                    directory,
                    ".credentials.enc.*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }


    // PRUEBA 81: Rechazar contenido excesivo
    // sin modificar las credenciales existentes.
    [Fact]
    public void SaveLinuxSecure_OnLinux_RejectsOversizedContentWithoutModifyingDestination()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string destinationPath = Path.Combine(
            directory,
            "credentials.enc");

        byte[] originalContent =
            System.Text.Encoding.UTF8.GetBytes(
                "Credenciales originales");

        byte[] oversizedContent =
            new byte[1024 * 1024 + 1];

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            CredentialFileStorage.SaveLinuxSecure(
                destinationPath,
                originalContent);

            Assert.Throws<InvalidDataException>(
                () => CredentialFileStorage.SaveLinuxSecure(
                    destinationPath,
                    oversizedContent));

            // El contenido anterior debe permanecer intacto.
            Assert.Equal(
                originalContent,
                File.ReadAllBytes(destinationPath));

            // Los permisos deben seguir siendo 600.
            Assert.Equal(
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite,
                File.GetUnixFileMode(destinationPath));

            // No deben existir archivos temporales.
            Assert.Empty(
                Directory.GetFiles(
                    directory,
                    ".credentials.enc.*.tmp"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                oversizedContent);

            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void ExecuteCleanupPreservingOriginalException_PreservesOriginalFailure()
    {
        IOException originalException =
            new IOException("Error original de escritura");

        bool closeExecuted = false;

        Action operation = () =>
        {
            try
            {
                throw originalException;
            }
            catch
            {
                CredentialFileStorage.ExecuteCleanupPreservingOriginalException(
                    () => throw new IOException(
                        "Error secundario de limpieza"),
                    () => closeExecuted = true);

                throw;
            }
        };

        Exception? capturedException =
            Record.Exception(operation);

        Assert.Same(
            originalException,
            capturedException);

        Assert.True(closeExecuted);
    }

    private sealed class FailingLinuxWriteOperations
        : ILinuxSecureWriteOperations
    {
        public readonly IOException WriteException =
            new("Error simulado de escritura.");

        public bool CleanupAttempted { get; private set; }
        public bool FileClosed { get; private set; }
        public bool DirectoryClosed { get; private set; }

        public int OpenDirectory(string path) => 100;

        public void ValidateDestinationAt(int directoryFd, string fileName)
        {
        }

        public int CreateFileAt(int directoryFd, string fileName) => 101;

        public void WriteAllBytes(int fileFd, byte[] content)
        {
            if (FailOnSyncFile || FailOnRename || FailOnSyncDirectory)
                return;

            throw WriteException;
        }

        public void RenameFileAt(
            int directoryFd,
            string sourceFileName,
            string destinationFileName)
        {
            RenameAttempted = true;

            if (FailOnRename)
                throw RenameException;

            RenameCompleted = true;
        }

        public void SyncFile(int fileFd)
        {
            if (FailOnSyncFile)
            {
                throw SyncFileException;
            }
        }

        public void UnlinkFileAt(int directoryFd, string fileName)
        {
            CleanupAttempted = true;
            throw new IOException("Error simulado de limpieza.");
        }

        public void SyncDirectory(int directoryFd)
        {
            SyncDirectoryAttempted = true;

            if (FailOnSyncDirectory)
                throw SyncDirectoryException;
        }

        public void Close(int fd)
        {
            if (fd == 101)
            {
                FileClosed = true;

                if (FailOnFileClose)
                {
                    throw new IOException(
                        "Error simulado al cerrar el archivo temporal.");
                }
            }

            if (fd == 100)
            {
                DirectoryClosed = true;

                if (FailOnDirectoryClose)
                {
                    throw new IOException(
                        "Error simulado al cerrar el directorio.");
                }
            }
        }

        public bool FailOnFileClose { get; set; }
        public bool FailOnDirectoryClose { get; set; }

        public bool FailOnSyncFile { get; set; }

        public IOException SyncFileException { get; } =
            new("Error simulado durante la sincronización del archivo.");

        public bool RenameAttempted { get; private set; }
        public bool FailOnRename { get; set; }

        public IOException RenameException { get; } =
            new("Error simulado durante RenameFileAt.");

        public bool FailOnSyncDirectory { get; set; }

        public IOException SyncDirectoryException { get; } =
            new("Error simulado durante SyncDirectory.");

        public bool SyncDirectoryAttempted { get; private set; }
        public bool RenameCompleted { get; private set; }

    }

    [Fact]
    public void SaveLinuxSecure_PreservesWriteException_WhenCleanupFails()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Arrange
        var operations = new FailingLinuxWriteOperations();

        string path = Path.Combine(
            Path.GetTempPath(),
            "securecredentials-test-83.json");

        byte[] content = [1, 2, 3, 4];

        // Act
        Action operation = () =>
            CredentialFileStorage.SaveLinuxSecure(
                path,
                content,
                operations);

        Exception? exception = Record.Exception(operation);

        // Assert
        Assert.Same(
            operations.WriteException,
            exception);

        Assert.True(
            operations.CleanupAttempted,
            "Debe intentarse eliminar el archivo temporal.");

        Assert.True(
            operations.FileClosed,
            "Debe cerrarse el descriptor del archivo.");

        Assert.True(
            operations.DirectoryClosed,
            "Debe cerrarse el descriptor del directorio.");
    }

    [Fact]
    public void SaveLinuxSecure_PreservesWriteException_WhenFileCloseFails()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Arrange
        var operations = new FailingLinuxWriteOperations
        {
            FailOnFileClose = true
        };

        string path = Path.Combine(
            Path.GetTempPath(),
            "securecredentials-test-84.json");

        byte[] content = [1, 2, 3, 4];

        // Act
        Action operation = () =>
            CredentialFileStorage.SaveLinuxSecure(
                path,
                content,
                operations);

        Exception? exception = Record.Exception(operation);

        // Assert

        // Debe conservarse exactamente la excepción original.
        Assert.Same(
            operations.WriteException,
            exception);

        // Debe intentarse cerrar el archivo temporal.
        Assert.True(
            operations.FileClosed,
            "Debe intentarse cerrar el archivo temporal.");

        // Debe intentarse eliminar el temporal.
        Assert.True(
            operations.CleanupAttempted,
            "Debe intentarse eliminar el archivo temporal.");

        // Debe cerrarse el directorio aunque falle el cierre del archivo.
        Assert.True(
            operations.DirectoryClosed,
            "Debe intentarse cerrar el directorio.");
    }

    [Fact]
    public void SaveLinuxSecure_PreservesWriteException_WhenDirectoryCloseFails()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Arrange
        var operations = new FailingLinuxWriteOperations
        {
            FailOnDirectoryClose = true
        };

        string path = Path.Combine(
            Path.GetTempPath(),
            "securecredentials-test-85.json");

        byte[] content = [1, 2, 3, 4];

        // Act
        Action operation = () =>
            CredentialFileStorage.SaveLinuxSecure(
                path,
                content,
                operations);

        Exception? exception = Record.Exception(operation);

        // Assert
        Assert.Same(
            operations.WriteException,
            exception);

        Assert.True(
            operations.FileClosed,
            "Debe intentarse cerrar el archivo temporal.");

        Assert.True(
            operations.CleanupAttempted,
            "Debe intentarse eliminar el archivo temporal.");

        Assert.True(
            operations.DirectoryClosed,
            "Debe intentarse cerrar el descriptor del directorio.");
    }

    [Fact]
    public void SaveLinuxSecure_PreservesSyncFileException_AndCleansUp()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Arrange
        var operations = new FailingLinuxWriteOperations
        {
            FailOnSyncFile = true
        };

        string path = Path.Combine(
            Path.GetTempPath(),
            "securecredentials-test-86.json");

        byte[] content = [1, 2, 3, 4];

        // Act
        Action operation = () =>
            CredentialFileStorage.SaveLinuxSecure(
                path,
                content,
                operations);

        Exception? exception = Record.Exception(operation);

        // Assert

        // Debe conservarse exactamente la excepción de SyncFile.
        Assert.Same(
            operations.SyncFileException,
            exception);

        // Debe intentarse eliminar el archivo temporal.
        Assert.True(
            operations.CleanupAttempted,
            "Debe intentarse eliminar el archivo temporal.");

        // Debe intentarse cerrar el descriptor del archivo.
        Assert.True(
            operations.FileClosed,
            "Debe intentarse cerrar el descriptor del archivo.");

        // Debe intentarse cerrar el descriptor del directorio.
        Assert.True(
            operations.DirectoryClosed,
            "Debe intentarse cerrar el descriptor del directorio.");

        // Nunca debe renombrarse un archivo cuya
        // sincronización ha fallado.
        Assert.False(
            operations.RenameAttempted,
            "No debe ejecutarse RenameFileAt si SyncFile falla.");
    }


    [Fact]
    public void SaveLinuxSecure_PreservesRenameException_AndCleansUp()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Arrange
        var operations = new FailingLinuxWriteOperations
        {
            FailOnRename = true
        };

        string path = Path.Combine(
            Path.GetTempPath(),
            "securecredentials-test-87.json");

        byte[] content = [1, 2, 3, 4];

        // Act
        Action operation = () =>
            CredentialFileStorage.SaveLinuxSecure(
                path,
                content,
                operations);

        Exception? exception = Record.Exception(operation);

        // Assert
        Assert.Same(
            operations.RenameException,
            exception);

        Assert.True(
            operations.RenameAttempted,
            "Debe haberse intentado el renombrado.");

        Assert.True(
            operations.CleanupAttempted,
            "Debe intentarse eliminar el temporal.");

        Assert.True(
            operations.FileClosed,
            "Debe intentarse cerrar el descriptor del archivo.");

        Assert.True(
            operations.DirectoryClosed,
            "Debe intentarse cerrar el descriptor del directorio.");
    }

    [Fact]
    public void SaveLinuxSecure_PreservesSyncDirectoryException_AfterRename()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Arrange
        var operations = new FailingLinuxWriteOperations
        {
            FailOnSyncDirectory = true
        };

        string path = Path.Combine(
            Path.GetTempPath(),
            "securecredentials-test-88.json");

        byte[] content = [1, 2, 3, 4];

        // Act
        Action operation = () =>
            CredentialFileStorage.SaveLinuxSecure(
                path,
                content,
                operations);

        Exception? exception = Record.Exception(operation);

        // Assert
        Assert.Same(
            operations.SyncDirectoryException,
            exception);

        Assert.True(
            operations.RenameAttempted,
            "Debe haberse intentado el renombrado.");

        Assert.True(
            operations.RenameCompleted,
            "El renombrado debe completarse antes de SyncDirectory.");

        Assert.True(
            operations.SyncDirectoryAttempted,
            "Debe intentarse sincronizar el directorio.");

        Assert.False(
            operations.CleanupAttempted,
            "No debe eliminarse el temporal después de un renombrado correcto.");

        Assert.True(
            operations.FileClosed,
            "Debe haberse intentado cerrar el archivo.");

        Assert.True(
            operations.DirectoryClosed,
            "Debe intentarse cerrar el directorio.");
    }

}
