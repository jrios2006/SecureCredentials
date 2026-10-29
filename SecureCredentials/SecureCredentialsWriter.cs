
using System.Security.Cryptography;

namespace SecureCredentials;

// Archivo: SecureCredentialsWriter.cs
// Proyecto: SecureCredentials
// Descripción: API de alto nivel para cifrar y guardar
//              ficheros de credenciales.

/// <summary>
/// Proporciona una API simplificada para cifrar y guardar
/// texto mediante AES-256-GCM.
/// </summary>
/// <remarks>
/// <para>
/// Utiliza <see cref="EmbeddedKeyProvider"/> para obtener
/// la clave criptográfica y <see cref="CredentialEncryptor"/>
/// para cifrar el texto.
/// </para>
/// <para>
/// El resultado cifrado se guarda mediante
/// <see cref="CredentialFileStorage"/>, reutilizando las
/// operaciones seguras implementadas para Linux.
/// </para>
/// <para>
/// Esta clase no interpreta el contenido del texto.
/// Puede utilizarse para proteger JSON, XML, configuraciones
/// o cualquier otro contenido textual UTF-8.
/// </para>
/// <para>
/// La clave integrada debe coincidir con la utilizada
/// por las aplicaciones que descifren el fichero.
/// </para>
/// </remarks>
public static class SecureCredentialsWriter
{
    /// <summary>
    /// Cifra un texto y guarda el resultado en el fichero
    /// especificado.
    /// </summary>
    /// <param name="filePath">
    /// Ruta del fichero cifrado que se desea crear o actualizar.
    /// </param>
    /// <param name="plaintext">
    /// Texto original que se desea cifrar.
    /// </param>
    /// <returns>
    /// Una tarea que finaliza cuando el fichero cifrado
    /// se ha guardado correctamente.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// La ruta es nula, vacía o contiene únicamente espacios.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// El texto original es nulo.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Se produce un error durante el cifrado.
    /// </exception>
    /// <exception cref="IOException">
    /// Se produce un error durante la escritura del fichero.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// No se dispone de permisos para escribir en la ruta.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Cada operación de cifrado genera un nonce aleatorio
    /// nuevo mediante <see cref="CredentialEncryptor"/>.
    /// </para>
    /// <para>
    /// La copia temporal de la clave AES se borra de memoria
    /// al finalizar la operación, incluso si se produce
    /// una excepción.
    /// </para>
    /// <para>
    /// En Linux, el almacenamiento utiliza las medidas
    /// de seguridad de <see cref="CredentialFileStorage"/>,
    /// incluyendo las comprobaciones de permisos y las
    /// operaciones de escritura segura.
    /// </para>
    /// </remarks>
    public static async Task WriteAsync(
        string filePath,
        string plaintext)
    {
        // 1. Validar los argumentos.
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(plaintext);

        // 2. Obtener la clave AES-256 integrada.
        var provider = new EmbeddedKeyProvider();

        byte[] key = await provider.GetKeyAsync(
            EmbeddedKeyProvider.DefaultKeyId);

        try
        {
            // 3. Cifrar el texto original.
            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                plaintext,
                key,
                EmbeddedKeyProvider.DefaultKeyId);

            // 4. Guardar el fichero cifrado.
            await CredentialFileStorage.SaveAsync(
                filePath,
                encrypted);
        }
        finally
        {
            // 5. Borrar la clave temporal de memoria.
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
