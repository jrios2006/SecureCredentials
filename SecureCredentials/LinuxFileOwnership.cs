
using Mono.Unix.Native;

namespace SecureCredentials;

// Archivo: LinuxFileOwnership.cs
// Proyecto: SecureCredentials
// Descripción: Consulta y validación del propietario
//              de archivos y directorios en Linux.

/// <summary>
/// Proporciona operaciones para consultar y validar
/// la propiedad de archivos y directorios en Linux.
/// </summary>
/// <remarks>
/// <para>
/// Esta clase utiliza funciones POSIX mediante
/// Mono.Unix.Native para consultar el identificador
/// de usuario (UID) propietario de una entrada
/// del sistema de archivos.
/// </para>
/// <para>
/// La consulta utiliza lstat, por lo que no sigue
/// un enlace simbólico situado en el último componente
/// de la ruta.
/// </para>
/// <para>
/// Estas operaciones forman parte de las comprobaciones
/// de seguridad utilizadas por SecureCredentials
/// para trabajar con archivos y directorios privados.
/// </para>
/// </remarks>
public static class LinuxFileOwnership
{
    /// <summary>
    /// Obtiene el identificador de usuario (UID)
    /// propietario de un archivo o directorio en Linux.
    /// </summary>
    /// <param name="path">
    /// Ruta del archivo o directorio cuyo propietario
    /// se desea consultar.
    /// </param>
    /// <returns>
    /// UID del propietario de la entrada consultada.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// La ruta es nula, vacía o contiene únicamente espacios.
    /// </exception>
    /// <exception cref="PlatformNotSupportedException">
    /// El sistema operativo no es Linux.
    /// </exception>
    /// <exception cref="IOException">
    /// La llamada POSIX lstat no puede consultar
    /// la información de la ruta.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Se utiliza lstat en lugar de stat para consultar
    /// la propia entrada del sistema de archivos,
    /// sin seguir un enlace simbólico final.
    /// </para>
    /// <para>
    /// Si la operación POSIX falla, se genera una
    /// excepción que incluye el código de error POSIX.
    /// </para>
    /// </remarks>
    public static uint GetOwnerUserId(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        string fullPath = Path.GetFullPath(path);

        // lstat consulta la propia entrada del sistema de archivos,
        // sin seguir un enlace simbólico final.
        int result = Syscall.lstat(
            fullPath,
            out Stat fileStatus);

        if (result != 0)
        {
            Errno error = Stdlib.GetLastError();

            throw new IOException(
                $"No se pudo consultar el propietario de " +
                $"'{fullPath}'. Error POSIX: {error}.");
        }

        return fileStatus.st_uid;
    }

    /// <summary>
    /// Comprueba que el UID efectivo del proceso
    /// coincide con el UID propietario indicado.
    /// </summary>
    /// <param name="effectiveUid">
    /// UID efectivo del proceso que realiza la operación.
    /// </param>
    /// <param name="ownerUid">
    /// UID propietario del archivo o directorio
    /// que se desea validar.
    /// </param>
    /// <exception cref="UnauthorizedAccessException">
    /// El UID efectivo del proceso no coincide
    /// con el UID propietario.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Esta operación compara únicamente los dos
    /// identificadores proporcionados.
    /// </para>
    /// <para>
    /// No consulta directamente el sistema operativo
    /// ni obtiene el propietario de una ruta.
    /// </para>
    /// <para>
    /// Cuando los identificadores coinciden,
    /// el método finaliza sin generar excepciones.
    /// </para>
    /// </remarks>
    public static void ValidateOwner(
        uint effectiveUid,
        uint ownerUid)
    {
        if (effectiveUid != ownerUid)
        {
            throw new UnauthorizedAccessException(
                $"El directorio pertenece al UID {ownerUid}, " +
                $"pero el proceso se ejecuta con UID {effectiveUid}.");
        }
    }
}
