
using System.Text.Json;
using System.Security.Cryptography;
using Mono.Unix.Native;

namespace SecureCredentials;

// Archivo: CredentialFileStorage.cs
// Proyecto: SecureCredentials
// Descripción: Almacenamiento y lectura de ficheros cifrados.

/// <summary>
/// Guarda y carga documentos cifrados representados por <see cref="EncryptedData"/>.
/// </summary>
/// <remarks>
/// <para>
/// Los documentos se serializan como JSON UTF-8 y se limita su tamaño
/// a 1 MiB. Esta clase no cifra ni descifra el contenido: esas operaciones
/// corresponden a <see cref="CredentialEncryptor"/> y
/// <see cref="CredentialDecryptor"/>.
/// </para>
/// <para>
/// Los documentos .enc pueden guardarse en directorios existentes 755,
/// siempre que pertenezcan al usuario y no permitan escritura por grupo
/// u otros. Los archivos cifrados continúan creándose con permisos 600;
/// los directorios de claves privadas conservan la exigencia 700.
/// </para>
/// <para>
/// En Linux, la lectura y la escritura emplean operaciones seguras
/// basadas en descriptores POSIX y validaciones de rutas y permisos.
/// En otros sistemas se utiliza una implementación alternativa;
/// sus garantías de seguridad deben validarse antes de utilizarla
/// en producción.
/// </para>
/// </remarks>
public static class CredentialFileStorage
{
    private const long MaxFileSize = 1024 * 1024;

    /// <summary>
    /// Serializa y guarda un documento cifrado en la ruta indicada.
    /// </summary>
    /// <param name="path">Ruta del fichero cifrado que se va a crear o sustituir.</param>
    /// <param name="encrypted">Documento cifrado y sus metadatos.</param>
    /// <returns>Una tarea que finaliza al completar la operación de guardado.</returns>
    /// <exception cref="ArgumentException">La ruta es nula, vacía o contiene solo espacios.</exception>
    /// <exception cref="ArgumentNullException">El documento cifrado es nulo.</exception>
    /// <exception cref="InvalidDataException">El JSON serializado supera 1 MiB.</exception>
    /// <exception cref="IOException">Se produce un error de acceso o escritura del fichero.</exception>
    /// <exception cref="UnauthorizedAccessException">No se dispone de permisos suficientes.</exception>
    /// <remarks>
/// <para>
/// En Linux, el guardado utiliza un fichero temporal y operaciones POSIX
    /// para validar el destino, escribir, sincronizar y renombrar el fichero.
/// </para>
/// <para>
    /// El búfer temporal que contiene el JSON serializado se borra de memoria
    /// al finalizar, incluso cuando se produce una excepción.
/// </para>
    /// </remarks>
    public static async Task SaveAsync(
        string path,
        EncryptedData encrypted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(encrypted);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(encrypted);

        // Garantizar la limpieza de memoria aunque falle
        // cualquier validación o la operación de escritura.
        try
        {
            if (json.Length > MaxFileSize)
            {
                throw new InvalidDataException(
                    "El archivo cifrado supera el tamaño permitido.");
            }

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath)!;

            // Los documentos cifrados pueden residir en directorios normales
            // (por ejemplo 755), sin cambiar su modo. Las claves privadas
            // continúan sujetas a CreatePrivate y permisos 700.
            CredentialDirectory.PrepareEncryptedDocumentDirectory(directory);

            // En Linux, escribir usando descriptores POSIX.
            // El metodo valida el destino respecto al directorio abierto.
            if (OperatingSystem.IsLinux())
            {
                SaveLinuxSecure(fullPath, json);
                return;
            }

            // En otros sistemas, conservar la escritura existente.
            if (Directory.Exists(fullPath))
            {
                throw new IOException(
                    $"La ruta de destino '{fullPath}' " +
                    "es un directorio, no un archivo.");
            }

            string tempPath = Path.Combine(
                directory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

            // Garantizar la eliminación del temporal
            // si la operación no termina correctamente.
            try
            {
                var options = new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = FileOptions.Asynchronous
                };

                // Establecer permisos 600 en Linux/macOS.
                if (!OperatingSystem.IsWindows())
                {
                    options.UnixCreateMode =
                        UnixFileMode.UserRead |
                        UnixFileMode.UserWrite;
                }

                await using (var stream =
                    new FileStream(tempPath, options))
                {
                    await stream.WriteAsync(json);
                    await stream.FlushAsync();

                    // Solicitar persistencia de los datos.
                    stream.Flush(flushToDisk: true);
                }

                // Sustituir el archivo de destino.
                File.Move(
                    tempPath,
                    fullPath,
                    overwrite: true);
            }
            finally
            {
                // Eliminar el temporal si todavía existe.
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
        finally
        {
            // Limpiar siempre el JSON serializado de memoria.
            CryptographicOperations.ZeroMemory(json);
        }
    }


    /// <summary>
    /// Ejecuta dos acciones de limpieza sin permitir que sus
    /// excepciones sustituyan a una excepción original activa.
    ///
    /// Debe utilizarse únicamente dentro de un bloque catch.
    /// </summary>
    internal static void ExecuteCleanupPreservingOriginalException(
        Action cleanup,
        Action closeResources)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentNullException.ThrowIfNull(closeResources);

        try
        {
            cleanup();
        }
        catch (Exception)
        {
            // Se conserva la excepción original,
            // que será relanzada por el bloque catch llamador.
        }
        finally
        {
            try
            {
                closeResources();
            }
            catch (Exception)
            {
                // Tampoco se permite que un error de cierre
                // sustituya a la excepción original.
            }
        }
    }



    // ============================================================
    // ESCRITURA SEGURA EN LINUX MEDIANTE DESCRIPTORES POSIX
    // ============================================================

    internal static void SaveLinuxSecure(string path, byte[] content) =>
        SaveLinuxSecure(path, content, LinuxSecureWriteOperations.Instance);

    // Sobrecarga interna: cada prueba puede proporcionar sus propias
    // operaciones sin compartir estado mutable entre ejecuciones.
    internal static void SaveLinuxSecure(
        string path,
        byte[] content,
        ILinuxSecureWriteOperations operations)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(operations);

        if (content.Length > MaxFileSize)
        {
            throw new InvalidDataException(
                "El archivo cifrado supera el tamaño permitido.");
        }

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        string fileName = Path.GetFileName(fullPath);

        int directoryFd = -1;
        int fileFd = -1;
        string? temporaryFileName = null;
        bool temporaryExists = false;

        // La excepción primaria se preserva si también falla la limpieza.
        // Una limpieza fallida sin excepción primaria sigue notificándose.
        bool primaryFailure = false;

        void CloseFile()
        {
            if (fileFd < 0)
                return;

            int fd = fileFd;
            fileFd = -1;
            operations.Close(fd);
        }

        void CleanupTemporary()
        {
            if (!temporaryExists || temporaryFileName is null || directoryFd < 0)
                return;

            try
            {
                operations.UnlinkFileAt(directoryFd, temporaryFileName);
                temporaryExists = false;
            }
            catch (FileNotFoundException)
            {
                temporaryExists = false;
            }
        }

        void CloseDirectory()
        {
            if (directoryFd < 0)
                return;

            int fd = directoryFd;
            directoryFd = -1;
            operations.Close(fd);
        }

        try
        {
            directoryFd = operations.OpenDirectory(directory);
            operations.ValidateDestinationAt(directoryFd, fileName);

            temporaryFileName = $".{fileName}.{Guid.NewGuid():N}.tmp";
            fileFd = operations.CreateFileAt(directoryFd, temporaryFileName);
            temporaryExists = true;

            operations.WriteAllBytes(fileFd, content);
            operations.SyncFile(fileFd);
            CloseFile();

            operations.RenameFileAt(directoryFd, temporaryFileName, fileName);
            temporaryExists = false;
            operations.SyncDirectory(directoryFd);
        }
        catch
        {
            primaryFailure = true;
            throw;
        }
        finally
        {
            if (primaryFailure)
            {
                // Se intenta cada cierre aunque otro paso de limpieza falle.
                ExecuteCleanupPreservingOriginalException(
                    () =>
                    {
                        try { CloseFile(); }
                        finally { CleanupTemporary(); }
                    },
                    CloseDirectory);
            }
            else
            {
                // Sin error primario, no ocultar los errores de limpieza.
                // Intentar los tres pasos incluso si uno falla.
                Exception? cleanupError = null;

                try { CloseFile(); }
                catch (Exception ex) { cleanupError ??= ex; }

                try { CleanupTemporary(); }
                catch (Exception ex) { cleanupError ??= ex; }

                try { CloseDirectory(); }
                catch (Exception ex) { cleanupError ??= ex; }

                if (cleanupError is not null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo
                        .Capture(cleanupError).Throw();
                }
            }
        }
    }

    // ============================================================
    // LECTURA DE CREDENCIALES
    // ============================================================

    /// <summary>
    /// Lee un fichero cifrado y reconstruye su representación <see cref="EncryptedData"/>.
    /// </summary>
    /// <param name="path">Ruta del fichero cifrado que se desea leer.</param>
    /// <returns>Una tarea cuyo resultado contiene el documento cifrado y sus metadatos.</returns>
    /// <exception cref="ArgumentException">La ruta es nula, vacía o contiene solo espacios.</exception>
    /// <exception cref="FileNotFoundException">No existe el fichero solicitado.</exception>
    /// <exception cref="InvalidDataException">El fichero supera 1 MiB o contiene un JSON nulo.</exception>
    /// <exception cref="JsonException">El contenido no es un JSON válido para <see cref="EncryptedData"/>.</exception>
    /// <exception cref="IOException">Se produce un error durante la lectura.</exception>
    /// <exception cref="UnauthorizedAccessException">No se dispone de permisos suficientes.</exception>
    /// <remarks>
/// <para>
    /// En Linux se abre el fichero respecto al descriptor de su directorio,
    /// sin seguir enlaces simbólicos en la apertura del fichero.
/// </para>
/// <para>
    /// Este método no autentica ni descifra el contenido; para recuperar
    /// el texto original debe realizarse el descifrado correspondiente.
    /// El búfer temporal de JSON se borra de memoria después de deserializarlo.
/// </para>
    /// </remarks>
    public static async Task<EncryptedData> LoadAsync(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;

        byte[] json;

        if (OperatingSystem.IsLinux())
        {
            int directoryFd = -1;
            int fileFd = -1;

            try
            {
                // Validar la estructura del directorio
                // y mantener abierto su descriptor.
                directoryFd =
                    LinuxSecureFileOperations.OpenDocumentDirectory(
                        directory);

                // Abrir el archivo respecto al descriptor
                // del directorio, sin seguir enlaces simbólicos.
                fileFd =
                    LinuxSecureFileOperations.OpenFileAt(
                        directoryFd,
                        Path.GetFileName(fullPath));

                // Leer desde el descriptor ya validado.
                // No volver a abrir el archivo por su ruta.
                json =
                    LinuxSecureFileOperations.ReadAllBytes(
                        fileFd,
                        checked((int)MaxFileSize));
            }
            finally
            {
                // Cerrar siempre los descriptores POSIX.
                if (fileFd >= 0)
                {
                    Syscall.close(fileFd);
                }

                if (directoryFd >= 0)
                {
                    Syscall.close(directoryFd);
                }
            }
        }
        else
        {
            // En sistemas distintos de Linux no se aplica la política POSIX.
            // No exigir un directorio privado para leer documentos .enc.
            CredentialDirectory.PrepareEncryptedDocumentDirectory(directory);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    "No existe el archivo de credenciales: " +
                    $"'{fullPath}'.",
                    fullPath);
            }

            var fileInfo = new FileInfo(fullPath);

            // Comprobar tamaño antes de cargar.
            if (fileInfo.Length > MaxFileSize)
            {
                throw new InvalidDataException(
                    "El archivo cifrado supera " +
                    "el tamaño permitido.");
            }

            json = await File.ReadAllBytesAsync(fullPath);
        }

        try
        {
            // Comprobación adicional del tamaño leído.
            if (json.Length > MaxFileSize)
            {
                throw new InvalidDataException(
                    "El archivo cifrado supera " +
                    "el tamaño permitido.");
            }

            return JsonSerializer.Deserialize<EncryptedData>(json)
                ?? throw new InvalidDataException(
                    "El archivo cifrado no contiene datos válidos.");
        }
        finally
        {
            // Limpiar siempre el JSON de memoria.
            CryptographicOperations.ZeroMemory(json);
        }
    }



}
