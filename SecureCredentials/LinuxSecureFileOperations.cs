
using Mono.Unix.Native;

namespace SecureCredentials;

internal static class LinuxSecureFileOperations
{
    /// <summary>
    /// Abre un directorio Linux sin seguir enlaces simbólicos
    /// en el componente final de la ruta.
    ///
    /// Comprueba mediante fstat que:
    /// - El objeto abierto es un directorio.
    /// - Tiene permisos privados 700.
    /// - Pertenece al usuario efectivo del proceso.
    /// Para documentos cifrados, usar OpenDocumentDirectory, que aplica
    /// una política distinta sin modificar la de las claves privadas.
    ///
    /// El descriptor devuelto debe cerrarse mediante
    /// Syscall.close() cuando deje de utilizarse.
    /// </summary>
    
    internal static int OpenDirectory(string path) => OpenDirectoryCore(path, requirePrivate: true);

    /// <summary>
    /// Abre el directorio de documentos cifrados sin exigir permisos 700.
    /// Admite, por ejemplo, 700, 750 y 755, siempre que el directorio
    /// pertenezca al usuario efectivo y no sea modificable por grupo u otros.
    /// Conserva la apertura por componentes con O_NOFOLLOW.
    /// No debe utilizarse para directorios que almacenan claves privadas.
    /// </summary>
    internal static int OpenDocumentDirectory(string path) =>
        OpenDirectoryCore(path, requirePrivate: false);

    private static int OpenDirectoryCore(string path, bool requirePrivate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        string[] components = path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (components.Length == 0)
        {
            throw new ArgumentException(
                "La ruta debe contener un directorio.",
                nameof(path));
        }

        foreach (string component in components)
        {
            if (component == "." ||
                component == ".." ||
                component.Contains('\0'))
            {
                throw new ArgumentException(
                    "La ruta contiene componentes no permitidos.",
                    nameof(path));
            }
        }

        OpenFlags flags =
            OpenFlags.O_RDONLY |
            OpenFlags.O_DIRECTORY |
            OpenFlags.O_NOFOLLOW |
            OpenFlags.O_CLOEXEC;

        // Partir de una ubicación conocida.
        string startingDirectory =
            Path.IsPathRooted(path) ? "/" : ".";

        int currentFd = Syscall.open(
            startingDirectory,
            flags);

        if (currentFd == -1)
        {
            Errno error = Stdlib.GetLastError();

            throw new IOException(
                $"No se pudo abrir el directorio inicial. " +
                $"Error POSIX: {error}.");
        }

        try
        {
            foreach (string component in components)
            {
                int nextFd = Syscall.openat(
                    currentFd,
                    component,
                    flags);

                
                if (nextFd == -1)
                {
                    Errno error = Stdlib.GetLastError();

                    if (error == Errno.ELOOP ||
                        error == Errno.ENOTDIR)
                    {
                        // Consultar el componente sin seguir enlaces.
                        // Se utiliza el descriptor del directorio padre,
                        // no una ruta absoluta susceptible de sustitución.
                        if (Syscall.fstatat(
                                currentFd,
                                component,
                                out Stat componentStat,
                                AtFlags.AT_SYMLINK_NOFOLLOW) == 0)
                        {
                            FilePermissions componentType =
                                componentStat.st_mode & FilePermissions.S_IFMT;

                            if (componentType == FilePermissions.S_IFLNK)
                            {
                                throw new UnauthorizedAccessException(
                                    $"No se permiten enlaces simbólicos " +
                                    $"en la ruta de credenciales: '{path}'.");
                            }
                        }
                    }

                    throw new IOException(
                        $"No se pudo abrir el componente " +
                        $"'{component}' de la ruta '{path}'. " +
                        $"Error POSIX: {error}.");
                }


                // El siguiente descriptor sustituye al anterior.
                Syscall.close(currentFd);
                currentFd = nextFd;
            }

            // Validar el directorio final ya abierto.
            if (Syscall.fstat(currentFd, out Stat stat) == -1)
            {
                Errno error = Stdlib.GetLastError();

                throw new IOException(
                    $"No se pudo consultar el directorio '{path}'. " +
                    $"Error POSIX: {error}.");
            }

            FilePermissions fileType =
                stat.st_mode & FilePermissions.S_IFMT;

            if (fileType != FilePermissions.S_IFDIR)
            {
                throw new UnauthorizedAccessException(
                    $"'{path}' no corresponde a un directorio.");
            }

            FilePermissions permissionMask =
                FilePermissions.S_IRWXU |
                FilePermissions.S_IRWXG |
                FilePermissions.S_IRWXO;

            FilePermissions actualPermissions =
                stat.st_mode & permissionMask;

            // Los directorios de claves conservan la política estricta 700.
            // Para documentos .enc se admite lectura/travesía por terceros,
            // pero nunca escritura por grupo u otros (riesgo de sustitución).
            FilePermissions unsafeWrite =
                FilePermissions.S_IWGRP | FilePermissions.S_IWOTH;

            if (requirePrivate && actualPermissions != FilePermissions.S_IRWXU)
            {
                throw new UnauthorizedAccessException(
                    $"El directorio '{path}' no tiene " +
                    "los permisos privados requeridos (700). " +
                    $"Permisos actuales: {actualPermissions}.");
            }

            if (!requirePrivate && (actualPermissions & unsafeWrite) != 0)
            {
                throw new UnauthorizedAccessException(
                    $"El directorio '{path}' permite escritura por grupo " +
                    "u otros usuarios; no es apto para documentos cifrados.");
            }

            uint effectiveUid =
                LinuxUserIdentity.GetEffectiveUserId();

            ValidateDirectoryOwner(
                effectiveUid,
                stat.st_uid);

            // El llamador debe cerrar este descriptor.
            int resultFd = currentFd;
            currentFd = -1;

            return resultFd;
        }
        finally
        {
            if (currentFd >= 0)
                Syscall.close(currentFd);
        }
    }

    /// <summary>
    /// Comprueba que el propietario de un directorio
    /// coincide con el usuario efectivo esperado.
    ///
    /// Permite probar la validación sin modificar
    /// propietarios reales del sistema de archivos.
    /// </summary>
    internal static void ValidateDirectoryOwner(
        uint effectiveUid,
        uint ownerUid)
    {
        if (ownerUid != effectiveUid)
        {
            throw new UnauthorizedAccessException(
                $"El directorio pertenece al UID {ownerUid}, " +
                $"pero el usuario efectivo tiene UID {effectiveUid}.");
        }
    }

    /// <summary>
    /// Abre un archivo privado existente, relativo a un
    /// descriptor de directorio previamente validado.
    ///
    /// Comprueba que:
    /// - El nombre no contiene componentes de ruta.
    /// - No se sigue un enlace simbólico.
    /// - Es un archivo regular.
    /// - Tiene permisos 600.
    /// - Pertenece al usuario efectivo.
    ///
    /// El llamador debe cerrar el descriptor devuelto.
    /// </summary>
    internal static int OpenFileAt(
        int directoryFd,
        string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        // Solo se permiten nombres simples.
        if (fileName == "." ||
            fileName == ".." ||
            fileName.Contains('/') ||
            fileName.Contains('\\') ||
            fileName.Contains('\0'))
        {
            throw new ArgumentException(
                "El nombre del archivo no puede contener " +
                "componentes de ruta.",
                nameof(fileName));
        }

        OpenFlags flags =
            OpenFlags.O_RDONLY |
            OpenFlags.O_NOFOLLOW |
            OpenFlags.O_CLOEXEC |
            OpenFlags.O_NONBLOCK;

        int fd = Syscall.openat(
            directoryFd,
            fileName,
            flags);

        
        if (fd == -1)
        {
            Errno error = Stdlib.GetLastError();

            if (error == Errno.ELOOP)
            {
                throw new UnauthorizedAccessException(
                    $"No se permiten enlaces simbólicos " +
                    $"para archivos de credenciales: '{fileName}'.");
            }

            if (error == Errno.ENOENT)
            {
                throw new FileNotFoundException(
                    $"No existe el archivo de credenciales: '{fileName}'.",
                    fileName);
            }

            throw new IOException(
                $"No se pudo abrir el archivo '{fileName}'. " +
                $"Error POSIX: {error}.");
        }


        try
        {
            // Consultar el archivo realmente abierto.
            if (Syscall.fstat(fd, out Stat stat) == -1)
            {
                Errno error = Stdlib.GetLastError();

                throw new IOException(
                    $"No se pudo consultar el archivo '{fileName}'. " +
                    $"Error POSIX: {error}.");
            }

            // Exigir que sea un archivo regular.
            FilePermissions fileType =
                stat.st_mode & FilePermissions.S_IFMT;

            if (fileType != FilePermissions.S_IFREG)
            {
                throw new UnauthorizedAccessException(
                    $"'{fileName}' no es un archivo regular.");
            }

            // Comprobar permisos 600.
            FilePermissions permissionMask =
                FilePermissions.S_IRWXU |
                FilePermissions.S_IRWXG |
                FilePermissions.S_IRWXO;

            FilePermissions actualPermissions =
                stat.st_mode & permissionMask;

            FilePermissions expectedPermissions =
                FilePermissions.S_IRUSR |
                FilePermissions.S_IWUSR;

            if (actualPermissions != expectedPermissions)
            {
                throw new UnauthorizedAccessException(
                    $"El archivo '{fileName}' no tiene " +
                    "los permisos privados requeridos (600). " +
                    $"Permisos actuales: {actualPermissions}.");
            }

            // Comprobar propietario.
            uint effectiveUid =
                LinuxUserIdentity.GetEffectiveUserId();

            if (stat.st_uid != effectiveUid)
            {
                throw new UnauthorizedAccessException(
                    $"El archivo '{fileName}' pertenece " +
                    $"al UID {stat.st_uid}, pero el usuario " +
                    $"efectivo tiene UID {effectiveUid}.");
            }

            // El llamador asume la responsabilidad
            // de cerrar el descriptor.
            return fd;
        }
        catch
        {
            // Evitar fugas si falla alguna validación.
            Syscall.close(fd);
            throw;
        }
    }

    internal static byte[] ReadAllBytes(
        int fileFd,
        int maxFileSize)
    {
        if (fileFd < 0)
            throw new ArgumentOutOfRangeException(nameof(fileFd));

        if (maxFileSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFileSize));

        byte[] buffer = new byte[
            checked(maxFileSize + 1)];

        int totalRead = 0;

        try
        {
            while (totalRead < buffer.Length)
            {
                long bytesRead;

                unsafe
                {
                    fixed (byte* pointer = buffer)
                    {
                        bytesRead = Syscall.read(
                            fileFd,
                            (IntPtr)(pointer + totalRead),
                            (ulong)(buffer.Length - totalRead));
                    }
                }

                if (bytesRead < 0)
                {
                    Errno error = Stdlib.GetLastError();

                    if (error == Errno.EINTR)
                        continue;

                    throw new IOException(
                        $"Error POSIX al leer credenciales: {error}.");
                }

                if (bytesRead == 0)
                    break;

                totalRead += checked((int)bytesRead);

                if (totalRead > maxFileSize)
                    throw new InvalidDataException(
                        "El archivo cifrado supera el tamaño permitido.");
            }

            byte[] result = new byte[totalRead];

            Buffer.BlockCopy(
                buffer,
                0,
                result,
                0,
                totalRead);

            return result;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations
                .ZeroMemory(buffer);
        }
    }
    


    internal static int CreateFileAt(
        int directoryFd,
        string fileName)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        if (directoryFd < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directoryFd));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // Solo permitimos un nombre de archivo, nunca una ruta.
        // Evitamos también "." y "..".
        if (fileName == "." ||
            fileName == ".." ||
            fileName.Contains('/') ||
            fileName.Contains('\0'))
        {
            throw new ArgumentException(
                "Debe proporcionarse un nombre de archivo válido, " +
                "sin componentes de ruta.",
                nameof(fileName));
        }

        OpenFlags flags =
            OpenFlags.O_WRONLY |
            OpenFlags.O_CREAT |
            OpenFlags.O_EXCL |
            OpenFlags.O_NOFOLLOW |
            OpenFlags.O_CLOEXEC;

        // Permisos iniciales 600.
        FilePermissions permissions =
            FilePermissions.S_IRUSR |
            FilePermissions.S_IWUSR;

        int fileFd = Syscall.openat(
            directoryFd,
            fileName,
            flags,
            permissions);

        if (fileFd < 0)
        {
            Errno error = Stdlib.GetLastError();

            if (error == Errno.EEXIST ||
                error == Errno.ELOOP)
            {
                throw new UnauthorizedAccessException(
                    $"No se puede crear el archivo '{fileName}': " +
                    "el destino ya existe o es un enlace simbólico.");
            }

            throw new IOException(
                $"No se pudo crear el archivo '{fileName}'. " +
                $"Error POSIX: {error}.");
        }

        return fileFd;
    }


    internal static unsafe void WriteAllBytes(
        int fileFd,
        byte[] data)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        if (fileFd < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileFd));
        }

        ArgumentNullException.ThrowIfNull(data);

        int totalWritten = 0;

        fixed (byte* buffer = data)
        {
            while (totalWritten < data.Length)
            {
                long written = Syscall.write(
                    fileFd,
                    buffer + totalWritten,
                    (ulong)(data.Length - totalWritten));

                if (written < 0)
                {
                    Errno error = Stdlib.GetLastError();

                    // Una señal puede interrumpir la escritura.
                    // En ese caso, reintentamos la operación.
                    if (error == Errno.EINTR)
                    {
                        continue;
                    }

                    throw new IOException(
                        "Error al escribir el archivo mediante " +
                        $"el descriptor POSIX. Error: {error}.");
                }

                // Evitar un bucle infinito si no hay progreso.
                if (written == 0)
                {
                    throw new IOException(
                        "La escritura POSIX no ha avanzado.");
                }

                // write() puede escribir menos bytes de los
                // solicitados. Continuamos desde el desplazamiento
                // correspondiente.
                totalWritten += checked((int)written);
            }
        }
    }


    internal static void SyncFile(int fileFd)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        if (fileFd < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileFd));
        }

        while (true)
        {
            int result = Syscall.fsync(fileFd);

            if (result == 0)
            {
                return;
            }

            Errno error = Stdlib.GetLastError();

            // Una señal puede interrumpir fsync().
            // En ese caso, repetimos la operación.
            if (error == Errno.EINTR)
            {
                continue;
            }

            throw new IOException(
                "No se pudieron sincronizar los datos " +
                "del archivo mediante fsync(). " +
                $"Error POSIX: {error}.");
        }
    }


    internal static void RenameFileAt(
        int directoryFd,
        string sourceFileName,
        string destinationFileName)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        if (directoryFd < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directoryFd));
        }

        // Validar el nombre del archivo de origen.
        ValidateFileName(sourceFileName, nameof(sourceFileName));

        // Validar el nombre del archivo de destino.
        ValidateFileName(
            destinationFileName,
            nameof(destinationFileName));

        while (true)
        {
            int result = Syscall.renameat(
                directoryFd,
                sourceFileName,
                directoryFd,
                destinationFileName);

            if (result == 0)
            {
                return;
            }

            Errno error = Stdlib.GetLastError();

            if (error == Errno.EINTR)
            {
                continue;
            }

            if (error == Errno.ENOENT)
            {
                throw new FileNotFoundException(
                    $"No existe el archivo de origen '{sourceFileName}'.",
                    sourceFileName);
            }

            throw new IOException(
                $"No se pudo renombrar '{sourceFileName}' " +
                $"como '{destinationFileName}'. " +
                $"Error POSIX: {error}.");
        }
    }

    private static void ValidateFileName(
        string fileName,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (fileName == "." ||
            fileName == ".." ||
            fileName.Contains('/') ||
            fileName.Contains('\0'))
        {
            throw new ArgumentException(
                "Debe proporcionarse un nombre de archivo válido, " +
                "sin componentes de ruta.",
                parameterName);
        }
    }


    internal static void UnlinkFileAt(
        int directoryFd,
        string fileName)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        if (directoryFd < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directoryFd));
        }

        // Reutilizar la validación que incorporamos
        // anteriormente para RenameFileAt().
        ValidateFileName(fileName, nameof(fileName));

        while (true)
        {
            int result = Syscall.unlinkat(
                directoryFd,
                fileName,
                AtFlags.AT_REMOVEDIR & (AtFlags)0);

            if (result == 0)
            {
                return;
            }

            Errno error = Stdlib.GetLastError();

            // Reintentar si una señal interrumpe
            // la operación.
            if (error == Errno.EINTR)
            {
                continue;
            }

            if (error == Errno.ENOENT)
            {
                throw new FileNotFoundException(
                    $"No existe el archivo '{fileName}'.",
                    fileName);
            }

            throw new IOException(
                $"No se pudo eliminar el archivo '{fileName}'. " +
                $"Error POSIX: {error}.");
        }
    }


    internal static void SyncDirectory(int directoryFd)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        if (directoryFd < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directoryFd));
        }

        // Comprobar que el descriptor corresponde
        // realmente a un directorio.
        if (Syscall.fstat(directoryFd, out Stat stat) != 0)
        {
            Errno error = Stdlib.GetLastError();

            throw new IOException(
                "No se pudo consultar el descriptor " +
                $"del directorio. Error POSIX: {error}.");
        }

        FilePermissions fileType =
            stat.st_mode & FilePermissions.S_IFMT;

        if (fileType != FilePermissions.S_IFDIR)
        {
            throw new IOException(
                "El descriptor proporcionado " +
                "no corresponde a un directorio.");
        }

        // Solicitar la sincronización de los cambios
        // realizados en las entradas del directorio.
        while (true)
        {
            int result = Syscall.fsync(directoryFd);

            if (result == 0)
            {
                return;
            }

            Errno error = Stdlib.GetLastError();

            if (error == Errno.EINTR)
            {
                continue;
            }

            throw new IOException(
                "No se pudo sincronizar el directorio " +
                $"mediante fsync(). Error POSIX: {error}.");
        }
    }

    internal static void ValidateDestinationAt(
        int directoryFd,
        string fileName)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        if (directoryFd < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directoryFd));
        }

        ValidateFileName(fileName, nameof(fileName));

        // Consultar el destino respecto al directorio abierto,
        // sin seguir enlaces simbólicos.
        int result = Syscall.fstatat(
            directoryFd,
            fileName,
            out Stat stat,
            AtFlags.AT_SYMLINK_NOFOLLOW);

        if (result != 0)
        {
            Errno error = Stdlib.GetLastError();

            // Un destino inexistente es válido:
            // SaveAsync podrá crear el archivo.
            if (error == Errno.ENOENT)
            {
                return;
            }

            throw new IOException(
                $"No se pudo consultar el destino '{fileName}'. " +
                $"Error POSIX: {error}.");
        }

        // Exigir que el destino existente sea regular.
        FilePermissions fileType =
            stat.st_mode & FilePermissions.S_IFMT;

        if (fileType != FilePermissions.S_IFREG)
        {
            throw new UnauthorizedAccessException(
                $"El destino '{fileName}' no es un archivo regular.");
        }

        // Validar el propietario.
        uint effectiveUid =
            LinuxUserIdentity.GetEffectiveUserId();

        LinuxFileOwnership.ValidateOwner(
            effectiveUid,
            stat.st_uid);

        // Exigir permisos privados 600.
        FilePermissions permissionMask =
            FilePermissions.S_IRWXU |
            FilePermissions.S_IRWXG |
            FilePermissions.S_IRWXO;

        FilePermissions actualPermissions =
            stat.st_mode & permissionMask;

        FilePermissions expectedPermissions =
            FilePermissions.S_IRUSR |
            FilePermissions.S_IWUSR;

        if (actualPermissions != expectedPermissions)
        {
            throw new UnauthorizedAccessException(
                $"El archivo '{fileName}' no tiene " +
                "los permisos privados requeridos (600). " +
                $"Permisos actuales: {actualPermissions}.");
        }
    }


}
