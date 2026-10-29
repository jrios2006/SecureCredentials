
using System.Security.Cryptography;

namespace SecureCredentials;

// Archivo: SecureCredentialsReader.cs
// Proyecto: SecureCredentials
// Descripción: API de alto nivel para leer y descifrar
//              ficheros de credenciales.

/// <summary>
/// Proporciona una API simplificada para leer y descifrar
/// ficheros protegidos mediante AES-256-GCM.
/// </summary>
/// <remarks>
/// <para>
/// Utiliza <see cref="CredentialFileStorage"/> para leer
/// el fichero cifrado y <see cref="CredentialDecryptor"/>
/// para recuperar el texto original.
/// </para>
/// <para>
/// La clave criptográfica se obtiene mediante
/// <see cref="EmbeddedKeyProvider"/>.
/// </para>
/// <para>
/// Esta clase no interpreta el contenido descifrado.
/// El resultado se devuelve como texto UTF-8, que puede
/// contener JSON, XML o cualquier otro texto.
/// </para>
/// </remarks>
public static class SecureCredentialsReader
{
    /// <summary>
    /// Lee un fichero cifrado y devuelve su contenido
    /// original como texto.
    /// </summary>
    /// <param name="filePath">
    /// Ruta del fichero cifrado que se desea leer.
    /// </param>
    /// <returns>
    /// Una tarea cuyo resultado contiene el texto
    /// descifrado del fichero.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// La ruta proporcionada es nula, vacía o contiene
    /// únicamente espacios.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// El fichero indicado no existe.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// El identificador de clave del fichero no corresponde
    /// a una clave disponible.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// El contenido cifrado no supera la verificación
    /// de autenticidad o no puede descifrarse.
    /// </exception>
    /// <remarks>
    /// En Linux, la lectura utiliza las comprobaciones
    /// de seguridad implementadas por
    /// <see cref="CredentialFileStorage"/>.
    ///
    /// La clave temporal se borra de memoria al finalizar,
    /// incluso si ocurre una excepción durante el descifrado.
    /// </remarks>
    public static async Task<string> ReadAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // 1. Leer el fichero cifrado.
        EncryptedData encrypted =
            await CredentialFileStorage.LoadAsync(filePath);

        // 2. Obtener la clave correspondiente.
        var provider = new EmbeddedKeyProvider();

        byte[] key = await provider.GetKeyAsync(
            encrypted.KeyId);

        try
        {
            // 3. Descifrar y devolver el texto original.
            return CredentialDecryptor.Decrypt(
                encrypted,
                key);
        }
        finally
        {
            // 4. Borrar la clave temporal de memoria.
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
