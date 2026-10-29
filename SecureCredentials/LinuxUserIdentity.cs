
using System.Runtime.InteropServices;

namespace SecureCredentials;

// Archivo: LinuxUserIdentity.cs
// Proyecto: SecureCredentials
// Descripción: Obtención del identificador de usuario
//              efectivo del proceso en Linux.

/// <summary>
/// Proporciona operaciones para consultar la identidad
/// efectiva del usuario que ejecuta el proceso en Linux.
/// </summary>
/// <remarks>
/// <para>
/// Esta clase utiliza interoperabilidad con la biblioteca
/// nativa libc para obtener el identificador de usuario
/// efectivo (UID) mediante la función POSIX geteuid.
/// </para>
/// <para>
/// El UID efectivo se utiliza en las comprobaciones
/// de propiedad de archivos y directorios privados
/// realizadas por SecureCredentials.
/// </para>
/// <para>
/// Las operaciones de esta clase están disponibles
/// únicamente en Linux.
/// </para>
/// </remarks>
public static partial class LinuxUserIdentity
{
    [LibraryImport("libc", EntryPoint = "geteuid")]
    private static partial uint GetEffectiveUserIdNative();

    /// <summary>
    /// Obtiene el identificador de usuario efectivo
    /// del proceso actual en Linux.
    /// </summary>
    /// <returns>
    /// Identificador numérico de usuario efectivo (UID)
    /// del proceso actual.
    /// </returns>
    /// <exception cref="PlatformNotSupportedException">
    /// El sistema operativo actual no es Linux.
    /// </exception>
    /// <remarks>
    /// <para>
    /// El UID efectivo determina la identidad utilizada
    /// por el sistema operativo para determinadas
    /// comprobaciones de permisos.
    /// </para>
    /// <para>
    /// Este método consulta directamente la función
    /// POSIX geteuid de libc.
    /// </para>
    /// <para>
    /// El valor devuelto puede diferir del UID real
    /// del usuario cuando el proceso utiliza mecanismos
    /// de cambio de identidad.
    /// </para>
    /// </remarks>
    public static uint GetEffectiveUserId()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Esta operación solo está disponible en Linux.");
        }

        return GetEffectiveUserIdNative();
    }
}
