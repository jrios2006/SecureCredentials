
namespace SecureCredentials;

// Archivo: CredentialDirectory.cs
// Proyecto: SecureCredentials
// Descripción: Creación y validación de directorios
//              privados para almacenar credenciales.

/// <summary>
/// Proporciona operaciones para crear y validar
/// directorios destinados al almacenamiento de
/// credenciales cifradas. Para documentos .enc en directorios
/// convencionales, véase PrepareEncryptedDocumentDirectory.
/// </summary>
/// <remarks>
/// <para>
/// En Linux, esta clase exige que el directorio
/// de credenciales tenga permisos privados 700
/// y pertenezca al usuario efectivo del proceso.
/// </para>
/// <para>
/// También comprueba que los componentes de la ruta
/// no sean enlaces simbólicos, con el objetivo de
/// reducir riesgos asociados a redirecciones de rutas.
/// </para>
/// <para>
/// En sistemas distintos de Linux, la implementación
/// actual crea el directorio sin aplicar las mismas
/// validaciones POSIX.
/// </para>
/// <para>
/// La implementación para Windows es provisional
/// y deberá revisarse antes de utilizarla como
/// mecanismo de almacenamiento seguro en producción.
/// </para>
/// </remarks>
public static class CredentialDirectory
{
    /// <summary>
    /// Crea un directorio privado para almacenar
    /// archivos de credenciales o valida el existente.
    /// </summary>
    /// <param name="path">
    /// Ruta del directorio que se desea crear o validar.
    /// </param>
    /// <exception cref="ArgumentException">
    /// La ruta es nula, vacía o contiene únicamente espacios.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// En Linux, la ruta contiene enlaces simbólicos,
    /// el directorio no pertenece al usuario efectivo
    /// o sus permisos no son exactamente 700.
    /// También puede producirse cuando el sistema
    /// de archivos deniega el acceso.
    /// </exception>
    /// <exception cref="IOException">
    /// Se produce un error de entrada/salida durante
    /// la creación o validación del directorio.
    /// </exception>
    /// <remarks>
    /// <para>
    /// La ruta se convierte inicialmente en una
    /// ruta absoluta mediante <see cref="Path.GetFullPath(string)"/>.
    /// </para>
    /// <para>
    /// En Linux se realizan las siguientes operaciones:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// Comprobar que los componentes de la ruta
    /// no son enlaces simbólicos.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Crear el directorio con permisos 700
    /// si todavía no existe.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Volver a comprobar la presencia de enlaces simbólicos.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Verificar que el propietario coincide
    /// con el usuario efectivo del proceso.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Verificar que los permisos del directorio
    /// son exactamente 700.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// En sistemas distintos de Linux, únicamente
    /// se solicita la creación del directorio.
    /// </para>
    /// </remarks>
    public static void CreatePrivate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);

        if (OperatingSystem.IsLinux())
        {
            UnixFileMode expectedPermissions =
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute;

            // Comprobar los enlaces simbólicos de toda la ruta.
            ValidateNoSymbolicLinks(fullPath);

            // Crear el directorio si no existe.
            Directory.CreateDirectory(
                fullPath,
                expectedPermissions);

            // Volver a comprobar la ruta.
            ValidateNoSymbolicLinks(fullPath);

            // Verificar que el directorio pertenece
            // al usuario efectivo del proceso.

            uint effectiveUid =
                LinuxUserIdentity.GetEffectiveUserId();

            uint ownerUid =
                LinuxFileOwnership.GetOwnerUserId(fullPath);

            // Validar la propiedad mediante el método común.
            LinuxFileOwnership.ValidateOwner(
                effectiveUid,
                ownerUid);

            // Verificar permisos del directorio final.
            UnixFileMode actualPermissions =
                File.GetUnixFileMode(fullPath);

            if (actualPermissions != expectedPermissions)
            {
                throw new UnauthorizedAccessException(
                    $"El directorio '{fullPath}' no tiene " +
                    "los permisos privados requeridos (700). " +
                    $"Permisos actuales: {actualPermissions}.");
            }

            return;
        }

        // Implementación provisional para Windows.
        Directory.CreateDirectory(fullPath);
    }

    /// <summary>
    /// Prepara un directorio para documentos cifrados .enc.
    /// A diferencia de CreatePrivate, no exige permisos 700 al directorio
    /// existente: acepta 755 si pertenece al usuario efectivo y no permite
    /// escritura a grupo u otros. No modifica permisos preexistentes.
    /// </summary>
    /// <remarks>
    /// Los directorios nuevos se crean con 700. En Linux, la validación
    /// definitiva se realiza sobre un descriptor abierto por componentes
    /// sin seguir enlaces simbólicos. Los archivos .enc siguen siendo 600.
    /// Este método NO sustituye a CreatePrivate para almacenar claves.
    /// En otros sistemas la política POSIX no está implementada.
    /// </remarks>
    public static void PrepareEncryptedDocumentDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);

        if (OperatingSystem.IsLinux())
        {
            // Evitar crear carpetas privadas dentro de rutas redirigidas.
            ValidateNoSymbolicLinks(fullPath);
            Directory.CreateDirectory(
                fullPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);

            // Validación sobre el objeto realmente abierto, sin depender
            // únicamente de comprobaciones previas por nombre de ruta.
            int fd = LinuxSecureFileOperations.OpenDocumentDirectory(fullPath);
            try { }
            finally { Mono.Unix.Native.Syscall.close(fd); }
            return;
        }

        Directory.CreateDirectory(fullPath);
    }

    private static void ValidateNoSymbolicLinks(string fullPath)
    {
        string? current = fullPath;

        while (current is not null)
        {
            var directoryInfo = new DirectoryInfo(current);

            if (directoryInfo.LinkTarget is not null)
            {
                throw new UnauthorizedAccessException(
                    "No se permiten enlaces simbólicos " +
                    $"en la ruta de credenciales: '{current}'.");
            }

            string? parent = Path.GetDirectoryName(current);

            if (parent == current)
                break;

            current = parent;
        }
    }
}
