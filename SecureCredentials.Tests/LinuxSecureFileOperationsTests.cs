
using System.Reflection;
using Mono.Unix.Native;
using Xunit;

namespace SecureCredentials.Tests;

public class LinuxSecureFileOperationsTests
{
    // PRUEBA 33: Abrir correctamente un directorio privado.
    [Fact]
    public void OpenDirectory_OnLinux_ReturnsValidDescriptor()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        int fd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            fd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            Assert.True(fd >= 0);

            // Verificar que el descriptor corresponde
            // a un objeto existente.
            Assert.Equal(0, Syscall.fstat(fd, out var stat));
        }
        finally
        {
            if (fd >= 0)
                Syscall.close(fd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 34: Rechazar enlaces simbólicos a directorios.
    [Fact]
    public void OpenDirectory_OnLinux_RejectsSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string realDirectory = Path.Combine(directory, "real");
        string symbolicLink = Path.Combine(directory, "link");

        try
        {
            CredentialDirectory.CreatePrivate(directory);
            CredentialDirectory.CreatePrivate(realDirectory);

            // Crear un enlace simbólico al directorio real.
            Directory.CreateSymbolicLink(
                symbolicLink,
                realDirectory);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            // OpenDirectory debe rechazar el enlace simbólico.
            var exception = Assert.Throws<TargetInvocationException>(
                () => openDirectory.Invoke(
                    null,
                    new object[] { symbolicLink }));

            Assert.IsType<UnauthorizedAccessException>(
                exception.InnerException);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 35: Comprobar permisos privados mediante fstat.
    [Fact]
    public void OpenDirectory_OnLinux_HasPrivatePermissions()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        int fd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            fd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            Assert.True(fd >= 0);

            // Consultar el directorio mediante su descriptor.
            Assert.Equal(0, Syscall.fstat(fd, out var stat));

            // Comprobar exclusivamente los bits de permisos.
            var permissions = stat.st_mode &
                (FilePermissions.S_IRWXU |
                 FilePermissions.S_IRWXG |
                 FilePermissions.S_IRWXO);

            Assert.Equal(
                FilePermissions.S_IRWXU,
                permissions);
        }
        finally
        {
            if (fd >= 0)
                Syscall.close(fd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 36: Rechazar directorios con permisos inseguros.
    [Fact]
    public void OpenDirectory_OnLinux_RejectsInsecurePermissions()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        int fd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Cambiar los permisos de 700 a 755.
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            // Debe rechazar un directorio con permisos 755.
            var exception = Assert.Throws<TargetInvocationException>(
                () => openDirectory.Invoke(
                    null,
                    new object[] { directory }));

            Assert.IsType<UnauthorizedAccessException>(
                exception.InnerException);
        }
        finally
        {
            if (fd >= 0)
                Syscall.close(fd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 37: Verificar el propietario del directorio abierto.
    [Fact]
    public void OpenDirectory_OnLinux_OwnerMatchesEffectiveUser()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        int fd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            fd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            Assert.True(fd >= 0);

            // Consultar el propietario del directorio abierto.
            Assert.Equal(0, Syscall.fstat(fd, out var stat));

            // Obtener el UID efectivo del proceso.
            uint effectiveUid = Syscall.geteuid();

            // Ambos deben coincidir.
            Assert.Equal(effectiveUid, stat.st_uid);
        }
        finally
        {
            if (fd >= 0)
                Syscall.close(fd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 38: Rechazar un propietario diferente.
    [Fact]
    public void ValidateDirectoryOwner_RejectsDifferentUserId()
    {
        if (!OperatingSystem.IsLinux())
            return;

        uint effectiveUid = Syscall.geteuid();

        // Simular un propietario distinto del usuario efectivo.
        uint differentUid = effectiveUid == 0
            ? 1u
            : 0u;

        Type operationsType = typeof(CredentialDirectory)
            .Assembly
            .GetType(
                "SecureCredentials.LinuxSecureFileOperations",
                throwOnError: true)!;

        MethodInfo? validateOwner = operationsType.GetMethod(
            "ValidateDirectoryOwner",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(validateOwner);

        var exception = Assert.Throws<TargetInvocationException>(
            () => validateOwner.Invoke(
                null,
                new object[] { effectiveUid, differentUid }));

        Assert.IsType<UnauthorizedAccessException>(
            exception.InnerException);
    }


    // PRUEBA 39: Aceptar un propietario que coincide.
    [Fact]
    public void ValidateDirectoryOwner_AcceptsMatchingUserId()
    {
        if (!OperatingSystem.IsLinux())
            return;

        uint effectiveUid = Syscall.geteuid();

        Type operationsType = typeof(CredentialDirectory)
            .Assembly
            .GetType(
                "SecureCredentials.LinuxSecureFileOperations",
                throwOnError: true)!;

        MethodInfo validateOwner = operationsType.GetMethod(
            "ValidateDirectoryOwner",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        // No debe producir ninguna excepción.
        Exception? exception = Record.Exception(
            () => validateOwner.Invoke(
                null,
                new object[] { effectiveUid, effectiveUid }));

        Assert.Null(exception);
    }


    // PRUEBA 40: Abrir un archivo relativo a un directorio.
    [Fact]
    public void OpenFileAt_OnLinux_ReturnsValidDescriptor()
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
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un archivo privado para la prueba.
            File.WriteAllText(filePath, "contenido de prueba");

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            directoryFd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            Assert.True(directoryFd >= 0);

            // Método que implementaremos en el siguiente paso.
            MethodInfo? openFileAt = operationsType.GetMethod(
                "OpenFileAt",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(openFileAt);

            fileFd = (int)openFileAt.Invoke(
                null,
                new object[] { directoryFd, "credentials.enc" })!;

            Assert.True(fileFd >= 0);

            // Verificar el descriptor del archivo abierto.
            Assert.Equal(0, Syscall.fstat(fileFd, out var stat));

            Assert.Equal(
                FilePermissions.S_IFREG,
                stat.st_mode & FilePermissions.S_IFMT);
        }
        finally
        {
            if (fileFd >= 0)
                Syscall.close(fileFd);

            if (directoryFd >= 0)
                Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 41: Rechazar enlaces simbólicos mediante openat.
    [Fact]
    public void OpenFileAt_OnLinux_RejectsSymbolicLink()
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

        int directoryFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            // Crear un archivo privado real.
            File.WriteAllText(realFile, "contenido de prueba");

            File.SetUnixFileMode(
                realFile,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            // Crear un enlace simbólico al archivo real.
            File.CreateSymbolicLink(
                symbolicLink,
                realFile);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            MethodInfo openFileAt = operationsType.GetMethod(
                "OpenFileAt",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            directoryFd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            Assert.True(directoryFd >= 0);

            // Debe rechazar el enlace simbólico.
            var exception = Assert.Throws<TargetInvocationException>(
                () => openFileAt.Invoke(
                    null,
                    new object[] { directoryFd, "credentials.enc" }));

            Assert.IsType<UnauthorizedAccessException>(
                exception.InnerException);
        }
        finally
        {
            if (directoryFd >= 0)
                Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 42: Rechazar archivos con permisos inseguros.
    [Fact]
    public void OpenFileAt_OnLinux_RejectsInsecurePermissions()
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

            // Crear el archivo.
            File.WriteAllText(filePath, "contenido de prueba");

            // Asignar permisos inseguros: 644.
            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.GroupRead |
                UnixFileMode.OtherRead);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            MethodInfo openFileAt = operationsType.GetMethod(
                "OpenFileAt",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            directoryFd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            Assert.True(directoryFd >= 0);

            // Debe rechazar el archivo con permisos 644.
            var exception = Assert.Throws<TargetInvocationException>(
                () => openFileAt.Invoke(
                    null,
                    new object[] { directoryFd, "credentials.enc" }));

            Assert.IsType<UnauthorizedAccessException>(
                exception.InnerException);
        }
        finally
        {
            if (directoryFd >= 0)
                Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 43: Rechazar nombres con componentes de ruta.
    [Theory]
    [InlineData("../credentials.enc")]
    [InlineData("subdirectory/credentials.enc")]
    [InlineData("/tmp/credentials.enc")]
    [InlineData("subdirectory\\credentials.enc")]
    [InlineData(".")]
    [InlineData("..")]
    public void OpenFileAt_OnLinux_RejectsPathComponents(
        string fileName)
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

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            MethodInfo openFileAt = operationsType.GetMethod(
                "OpenFileAt",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            directoryFd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            Assert.True(directoryFd >= 0);

            // Debe rechazar cualquier componente de ruta.
            var exception = Assert.Throws<TargetInvocationException>(
                () => openFileAt.Invoke(
                    null,
                    new object[] { directoryFd, fileName }));

            Assert.IsType<ArgumentException>(
                exception.InnerException);
        }
        finally
        {
            if (directoryFd >= 0)
                Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 44: Rechazar enlaces simbólicos intermedios.
    [Fact]
    public void OpenDirectory_OnLinux_RejectsIntermediateSymbolicLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string root = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string realDirectory = Path.Combine(root, "real");
        string credentialsDirectory =
            Path.Combine(realDirectory, "credentials");

        string symbolicLink = Path.Combine(root, "link");
        string pathThroughLink =
            Path.Combine(symbolicLink, "credentials");

        try
        {
            CredentialDirectory.CreatePrivate(root);
            CredentialDirectory.CreatePrivate(realDirectory);
            CredentialDirectory.CreatePrivate(credentialsDirectory);

            // Crear un enlace simbólico intermedio.
            Directory.CreateSymbolicLink(
                symbolicLink,
                realDirectory);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            // Debe rechazar la ruta que atraviesa el enlace.
            var exception = Assert.Throws<TargetInvocationException>(
                () => openDirectory.Invoke(
                    null,
                    new object[] { pathThroughLink }));

            Assert.IsType<UnauthorizedAccessException>(
                exception.InnerException);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }



    // PRUEBA 45: No dejar descriptores abiertos
    // cuando falla OpenDirectory().

    [Fact]
    public void OpenDirectory_OnLinux_FailureDoesNotLeakDescriptors()
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
            CredentialDirectory.CreatePrivate(root);
            CredentialDirectory.CreatePrivate(realDirectory);

            // Crear un enlace simbólico que OpenDirectory()
            // debe rechazar.
            Directory.CreateSymbolicLink(
                symbolicLink,
                realDirectory);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            // Obtener los descriptores abiertos que apuntan
            // al directorio de esta prueba o sus subdirectorios.
            HashSet<string> GetOpenDescriptorsForTestDirectory()
            {
                var result = new HashSet<string>();

                foreach (string fdPath in
                    Directory.GetFiles("/proc/self/fd"))
                {
                    try
                    {
                        string? target = File.ResolveLinkTarget(
                            fdPath,
                            returnFinalTarget: false)?.FullName;

                        if (target != null &&
                            (target == root ||
                            target.StartsWith(
                                root + Path.DirectorySeparatorChar,
                                StringComparison.Ordinal)))
                        {
                            result.Add(fdPath);
                        }
                    }
                    catch (IOException)
                    {
                        // Un descriptor puede cerrarse mientras
                        // enumeramos /proc/self/fd.
                    }
                }

                return result;
            }

            // Calentar la ruta de reflexión antes de medir.
            var warmupException =
                Assert.Throws<TargetInvocationException>(
                    () => openDirectory.Invoke(
                        null,
                        new object[] { symbolicLink }));

            // CORRECCIÓN: utilizar warmupException.
            Assert.IsType<UnauthorizedAccessException>(
                warmupException.InnerException);

            // Registrar los descriptores relacionados
            // con nuestro directorio antes de las operaciones.
            HashSet<string> before =
                GetOpenDescriptorsForTestDirectory();

            // Provocar 100 aperturas fallidas.
            for (int i = 0; i < 100; i++)
            {
                var exception =
                    Assert.Throws<TargetInvocationException>(
                        () => openDirectory.Invoke(
                            null,
                            new object[] { symbolicLink }));

                Assert.IsType<UnauthorizedAccessException>(
                    exception.InnerException);
            }

            // Comprobar los descriptores restantes.
            HashSet<string> after =
                GetOpenDescriptorsForTestDirectory();

            // No deben quedar descriptores adicionales
            // relacionados con nuestro directorio.
            Assert.Empty(after.Except(before));
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

    // PRUEBA 52:
    // OpenFileAt debe rechazar una FIFO sin bloquearse.
    [Fact]
    public void OpenFileAt_OnLinux_RejectsNamedPipe()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string directory = Path.Combine(
            Path.GetTempPath(),
            "SecureCredentials.Tests",
            Guid.NewGuid().ToString("N"));

        string fifoPath = Path.Combine(
            directory,
            "credentials.enc");

        int directoryFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            int result = Syscall.mkfifo(
                fifoPath,
                FilePermissions.S_IRUSR |
                FilePermissions.S_IWUSR);

            Assert.Equal(0, result);

            File.SetUnixFileMode(
                fifoPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            MethodInfo openFileAt = operationsType.GetMethod(
                "OpenFileAt",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            directoryFd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            var exception =
                Assert.Throws<TargetInvocationException>(
                    () => openFileAt.Invoke(
                        null,
                        new object[]
                        {
                            directoryFd,
                            "credentials.enc"
                        }));

            Assert.IsType<UnauthorizedAccessException>(
                exception.InnerException);
        }
        finally
        {
            if (directoryFd >= 0)
                Syscall.close(directoryFd);

            if (File.Exists(fifoPath))
                File.Delete(fifoPath);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    // PRUEBA 53:
    // Leer contenido desde el descriptor abierto con OpenFileAt.
    [Fact]
    public void OpenFileAt_OnLinux_AllowsReadingFromDescriptor()
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
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            byte[] expected =
                System.Text.Encoding.UTF8.GetBytes(
                    "contenido de prueba");

            File.WriteAllBytes(filePath, expected);

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            MethodInfo openFileAt = operationsType.GetMethod(
                "OpenFileAt",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            directoryFd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            fileFd = (int)openFileAt.Invoke(
                null,
                new object[] { directoryFd, "credentials.enc" })!;

            Assert.True(fileFd >= 0);

            byte[] actual = new byte[expected.Length];

            long bytesRead;

            unsafe
            {
                fixed (byte* buffer = actual)
                {
                    bytesRead = Syscall.read(
                        fileFd,
                        (IntPtr)buffer,
                        (ulong)actual.Length);
                }
            }

            Assert.Equal(expected.Length, bytesRead);
            Assert.Equal(expected, actual);
        }
        finally
        {
            if (fileFd >= 0)
                Syscall.close(fileFd);

            if (directoryFd >= 0)
                Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    [Fact]
    public void ReadAllBytes_OnLinux_ReadsCompleteFile()
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
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            byte[] expected = new byte[100_000];
            Random.Shared.NextBytes(expected);

            File.WriteAllBytes(filePath, expected);

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            MethodInfo openFileAt = operationsType.GetMethod(
                "OpenFileAt",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            MethodInfo readAllBytes = operationsType.GetMethod(
                "ReadAllBytes",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            directoryFd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            fileFd = (int)openFileAt.Invoke(
                null,
                new object[] { directoryFd, "credentials.enc" })!;

            byte[] actual = (byte[])readAllBytes.Invoke(
                null,
                new object[] { fileFd, 1024 * 1024 })!;

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (fileFd >= 0)
                Syscall.close(fileFd);

            if (directoryFd >= 0)
                Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }


    [Fact]
    public void ReadAllBytes_OnLinux_RejectsFileLargerThanLimit()
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
        int fileFd = -1;

        try
        {
            CredentialDirectory.CreatePrivate(directory);

            byte[] content = new byte[1024 * 1024 + 1];
            Random.Shared.NextBytes(content);

            File.WriteAllBytes(filePath, content);

            File.SetUnixFileMode(
                filePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite);

            Type operationsType = typeof(CredentialDirectory)
                .Assembly
                .GetType(
                    "SecureCredentials.LinuxSecureFileOperations",
                    throwOnError: true)!;

            MethodInfo openDirectory = operationsType.GetMethod(
                "OpenDirectory",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            MethodInfo openFileAt = operationsType.GetMethod(
                "OpenFileAt",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            MethodInfo readAllBytes = operationsType.GetMethod(
                "ReadAllBytes",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            directoryFd = (int)openDirectory.Invoke(
                null,
                new object[] { directory })!;

            fileFd = (int)openFileAt.Invoke(
                null,
                new object[] { directoryFd, "credentials.enc" })!;

            var exception =
                Assert.Throws<TargetInvocationException>(
                    () => readAllBytes.Invoke(
                        null,
                        new object[] { fileFd, 1024 * 1024 }));

            Assert.IsType<InvalidDataException>(
                exception.InnerException);
        }
        finally
        {
            if (fileFd >= 0)
                Syscall.close(fileFd);

            if (directoryFd >= 0)
                Syscall.close(directoryFd);

            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }



}
