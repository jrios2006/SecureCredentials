
using System.Security.Cryptography;
using System.Text;

namespace SecureCredentials;

// Archivo: CredentialDecryptor.cs
// Proyecto: SecureCredentials
// Descripción: Descifrado AES-256-GCM de contenido cifrado.

/// <summary>
/// Proporciona operaciones para descifrar contenido
/// protegido mediante AES-256-GCM.
/// </summary>
/// <remarks>
/// <para>
/// Esta clase recupera el texto original a partir de un objeto
/// <see cref="EncryptedData"/> y una clave AES de 256 bits.
/// </para>
/// <para>
/// Antes del descifrado se comprueba la versión del formato,
/// el algoritmo utilizado y la longitud del nonce y de
/// la etiqueta de autenticación.
/// </para>
/// <para>
/// Se admite la versión 1 del formato cifrado y la versión
/// definida por <see cref="CredentialMetadata.CurrentVersion"/>.
/// Para la versión actual se autentican los metadatos
/// mediante datos adicionales (AAD).
/// </para>
/// <para>
/// Esta clase no accede a ficheros ni interpreta el contenido
/// del texto descifrado. La lectura del fichero corresponde
/// a <see cref="CredentialFileStorage"/>.
/// </para>
/// </remarks>
public static class CredentialDecryptor
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>
    /// Descifra un contenido AES-256-GCM y devuelve
    /// el texto original codificado en UTF-8.
    /// </summary>
    /// <param name="encrypted">
    /// Objeto que contiene el contenido cifrado, el nonce,
    /// la etiqueta de autenticación y los metadatos.
    /// </param>
    /// <param name="key">
    /// Clave AES de 256 bits (32 bytes) utilizada
    /// para descifrar el contenido.
    /// </param>
    /// <returns>
    /// Texto original recuperado mediante el descifrado.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// El objeto cifrado o la clave son nulos.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// La clave no contiene exactamente 32 bytes.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// La versión del formato o el algoritmo de cifrado
    /// no son compatibles.
    /// </exception>
    /// <exception cref="FormatException">
    /// Alguno de los campos binarios codificados en Base64
    /// contiene un formato no válido.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// El nonce o la etiqueta de autenticación
    /// no tienen la longitud esperada.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// La autenticación GCM falla, por ejemplo, porque
    /// la clave es incorrecta o los datos han sido alterados.
    /// </exception>
    /// <remarks>
    /// <para>
    /// El descifrado comprueba la integridad y autenticidad
    /// del contenido mediante la etiqueta GCM.
    /// </para>
    /// <para>
    /// Los buffers temporales del texto descifrado y de
    /// los datos adicionales autenticados se borran
    /// al finalizar la operación.
    /// </para>
    /// <para>
    /// La clave proporcionada no se modifica ni se borra.
    /// Su gestión corresponde al código que llama
    /// a este método.
    /// </para>
    /// </remarks>
    public static string Decrypt(
        EncryptedData encrypted,
        byte[] key)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySize)
            throw new ArgumentException(
                "La clave debe tener 32 bytes.",
                nameof(key));

        if (encrypted.Version != 1 &&
            encrypted.Version != CredentialMetadata.CurrentVersion)
        {
            throw new NotSupportedException(
                "Versión de cifrado no compatible.");
        }

        if (encrypted.Algorithm != CredentialMetadata.Algorithm)
        {
            throw new NotSupportedException(
                "Algoritmo de cifrado no compatible.");
        }

        byte[] nonce = Convert.FromBase64String(encrypted.Nonce);
        byte[] ciphertext = Convert.FromBase64String(encrypted.Ciphertext);
        byte[] tag = Convert.FromBase64String(encrypted.Tag);

        if (nonce.Length != NonceSize || tag.Length != TagSize)
        {
            throw new InvalidDataException(
                "Nonce o etiqueta de autenticación no válidos.");
        }

        byte[] plaintext = new byte[ciphertext.Length];
        byte[] aad = Array.Empty<byte>();

        try
        {
            if (encrypted.Version == CredentialMetadata.CurrentVersion)
            {
                aad = CredentialMetadata.CreateAuthenticatedData(
                    encrypted.Version,
                    encrypted.Algorithm,
                    encrypted.KeyId);
            }

            using var aes = new AesGcm(key, TagSize);

            aes.Decrypt(
                nonce,
                ciphertext,
                tag,
                plaintext,
                aad);

            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(aad);
        }
    }
}
