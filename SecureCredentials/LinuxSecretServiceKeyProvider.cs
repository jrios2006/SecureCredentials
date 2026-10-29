
using System.Diagnostics;
using System.Security.Cryptography;

namespace SecureCredentials;

// Archivo: LinuxSecretServiceKeyProvider.cs
// Proyecto: SecureCredentials
// Descripción: Recuperación de claves AES-256 desde
//              el servicio de secretos de Linux.

/// <summary>
/// Proporciona acceso a claves criptográficas almacenadas
/// en el servicio de secretos de Linux.
/// </summary>
/// <remarks>
/// <para>
/// Implementa <see cref="IKeyProvider"/> utilizando
/// la herramienta externa secret-tool para recuperar
/// claves previamente almacenadas.
/// </para>
/// <para>
/// La búsqueda utiliza los atributos application
/// con valor SecureCredentials y key-id con el
/// identificador solicitado.
/// </para>
/// <para>
/// La clave recuperada debe estar codificada en Base64
/// y representar exactamente 32 bytes, correspondientes
/// a una clave AES de 256 bits.
/// </para>
/// <para>
/// Este proveedor requiere Linux, la herramienta
/// /usr/bin/secret-tool y un servicio de secretos
/// accesible desde la sesión del proceso.
/// </para>
/// <para>
/// A diferencia de <see cref="EmbeddedKeyProvider"/>,
/// este proveedor obtiene la clave de un almacén externo
/// en lugar de utilizar una clave integrada en la biblioteca.
/// </para>
/// </remarks>
public sealed class LinuxSecretServiceKeyProvider : IKeyProvider
{
    /// <summary>
    /// Recupera de forma asíncrona una clave AES-256
    /// desde el servicio de secretos de Linux.
    /// </summary>
    /// <param name="keyId">
    /// Identificador de la clave que se desea recuperar.
    /// </param>
    /// <param name="cancellationToken">
    /// Token opcional para cancelar la operación.
    /// </param>
    /// <returns>
    /// Una tarea cuyo resultado contiene la clave
    /// criptográfica de 32 bytes.
    /// </returns>
    /// <exception cref="PlatformNotSupportedException">
    /// El sistema operativo no es Linux.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// El identificador de clave es nulo, vacío
    /// o contiene únicamente espacios.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// La herramienta no puede recuperar la clave
    /// o devuelve un resultado vacío.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// La clave recuperada no contiene un Base64 válido
    /// o no representa exactamente 32 bytes.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// La operación se cancela mediante el token indicado.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Ejecuta secret-tool con los argumentos:
    /// lookup application SecureCredentials key-id
    /// seguido del identificador solicitado.
    /// </para>
    /// <para>
    /// El proceso externo se inicia sin utilizar
    /// un intérprete de comandos.
    /// </para>
    /// <para>
    /// Si se solicita la cancelación durante la ejecución,
    /// se intenta finalizar el proceso externo y
    /// se espera su terminación.
    /// </para>
    /// <para>
    /// El array de bytes devuelto pertenece al llamador,
    /// que debe gestionar su ciclo de vida y borrarlo
    /// de memoria cuando deje de necesitarlo.
    /// </para>
    /// </remarks>
    public async Task<byte[]> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException(
                "Este proveedor solo funciona en Linux.");

        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/secret-tool",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("lookup");
        startInfo.ArgumentList.Add("application");
        startInfo.ArgumentList.Add("SecureCredentials");
        startInfo.ArgumentList.Add("key-id");
        startInfo.ArgumentList.Add(keyId);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        try
        {
            string output = await process.StandardOutput
                .ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 ||
                string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException(
                    "No se pudo recuperar la clave del almacén Linux.");
            }

            byte[] key;

            try
            {
                key = Convert.FromBase64String(output.Trim());
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException(
                    "La clave almacenada no tiene un formato Base64 válido.",
                    ex);
            }

            if (key.Length != 32)
            {
                CryptographicOperations.ZeroMemory(key);

                throw new InvalidDataException(
                    "La clave recuperada debe tener 32 bytes.");
            }

            return key;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            await process.WaitForExitAsync();
            throw;
        }
    }
}
