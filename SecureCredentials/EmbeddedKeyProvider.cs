
using System.Security.Cryptography;

namespace SecureCredentials;

// Archivo: EmbeddedKeyProvider.cs
// Proyecto: SecureCredentials
// Descripción: Proveedor de clave AES-256 integrada en la aplicación.

/// <summary>
/// Proporciona una clave AES-256 integrada en la distribución
/// de la aplicación.
/// </summary>
/// <remarks>
/// <para>
/// Implementa <see cref="IKeyProvider"/> y permite obtener
/// una clave criptográfica estable entre ejecuciones.
/// </para>
/// <para>
/// La aplicación generadora y las aplicaciones consumidoras
/// deben utilizar la misma clave para poder intercambiar
/// ficheros cifrados.
/// </para>
/// <para>
/// Este mecanismo está destinado a impedir la lectura casual
/// de las credenciales. No protege frente a ingeniería inversa,
/// ya que la clave está incorporada en la aplicación.
/// </para>
/// <para>
/// Para sustituir la clave, debe actualizarse
/// <c>KeyBase64</c> y asignarse un nuevo identificador
/// a <see cref="DefaultKeyId"/>.
/// Los ficheros cifrados con claves anteriores no podrán
/// descifrarse si dichas claves dejan de estar disponibles.
/// </para>
/// </remarks>
public sealed class EmbeddedKeyProvider : IKeyProvider
{
    /// <summary>
    /// Identificador de la clave AES-256 integrada.
    /// </summary>
    /// <remarks>
    /// Debe coincidir con el identificador utilizado
    /// al generar el fichero cifrado.
    /// </remarks>
    public const string DefaultKeyId = "embedded-v1";

    // Clave AES-256 codificada en Base64.
    //
    // Generación:
    //     openssl rand -base64 32
    //
    // Debe contener exactamente 32 bytes una vez decodificada.
    // No publicar la clave definitiva en repositorios públicos.
    //
    // IMPORTANTE:
    // Cambiar la clave requiere volver a cifrar los ficheros
    // existentes o conservar un mecanismo de lectura anterior.

    private const string KeyBase64 =
        "QchNkwoCIxiEPvhATaZNCThqImUIBtE7aNdu6EOYTVo=";

    /// <summary>
    /// Obtiene la clave AES-256 correspondiente al identificador
    /// solicitado.
    /// </summary>
    /// <param name="keyId">
    /// Identificador de la clave que se desea recuperar.
    /// </param>
    /// <param name="cancellationToken">
    /// Token opcional para cancelar la operación.
    /// </param>
    /// <returns>
    /// Una tarea completada con un array independiente
    /// de 32 bytes que contiene la clave AES-256.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// El identificador es nulo, vacío o contiene
    /// únicamente espacios.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// El identificador solicitado no corresponde
    /// a la clave integrada.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// La clave no está configurada, su codificación Base64
    /// es incorrecta o no contiene exactamente 32 bytes.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// La operación ha sido cancelada.
    /// </exception>
    /// <remarks>
    /// El consumidor es responsable de borrar el array
    /// devuelto cuando ya no necesite la clave.
    /// </remarks>
    public Task<byte[]> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(
                keyId,
                DefaultKeyId,
                StringComparison.Ordinal))
        {
            throw new KeyNotFoundException(
                $"Identificador de clave no reconocido: '{keyId}'.");
        }

        if (KeyBase64 ==
            "REEMPLAZAR_CON_CLAVE_AES_256_EN_BASE64")
        {
            throw new InvalidOperationException(
                "La clave integrada no ha sido configurada.");
        }

        byte[] key;

        try
        {
            key = Convert.FromBase64String(KeyBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "La clave integrada no tiene un formato Base64 válido.",
                ex);
        }

        if (key.Length != 32)
        {
            CryptographicOperations.ZeroMemory(key);

            throw new InvalidOperationException(
                "La clave integrada debe contener exactamente 32 bytes.");
        }

        // El consumidor recibe un array independiente.
        return Task.FromResult(key);
    }
}
