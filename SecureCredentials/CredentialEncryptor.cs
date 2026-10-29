
using System.Security.Cryptography;
using System.Text;

namespace SecureCredentials;

// Archivo: CredentialEncryptor.cs
// Proyecto: SecureCredentials
// Descripción: Cifrado AES-256-GCM de texto UTF-8.

/// <summary>
/// Proporciona operaciones para cifrar texto mediante AES-256-GCM.
/// </summary>
/// <remarks>
/// <para>
/// El texto original se convierte a UTF-8 antes del cifrado.
/// Cada operación genera un nonce aleatorio de 12 bytes y
/// utiliza una etiqueta de autenticación de 16 bytes.
/// </para>
/// <para>
/// Los metadatos de versión, algoritmo e identificador de
/// clave se autentican mediante datos adicionales (AAD).
/// </para>
/// <para>
/// Esta clase no guarda ficheros ni interpreta el contenido
/// del texto. El almacenamiento corresponde a
/// <see cref="CredentialFileStorage"/>.
/// </para>
/// </remarks>
public static class CredentialEncryptor
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>
    /// Cifra un texto UTF-8 utilizando AES-256-GCM.
    /// </summary>
    /// <param name="plaintext">
    /// Texto original que se desea cifrar.
    /// </param>
    /// <param name="key">
    /// Clave AES de 256 bits (32 bytes).
    /// </param>
    /// <param name="keyId">
    /// Identificador de la clave utilizada para el cifrado.
    /// </param>
    /// <returns>
    /// Un objeto <see cref="EncryptedData"/> que contiene
    /// el texto cifrado, el nonce, la etiqueta de autenticación
    /// y los metadatos necesarios para su posterior descifrado.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// El texto original o la clave son nulos.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// La clave no contiene exactamente 32 bytes o el
    /// identificador de clave está vacío.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Se produce un error durante la operación criptográfica.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Cada llamada genera un nonce criptográficamente aleatorio.
    /// </para>
    /// <para>
    /// Los buffers temporales del texto original y los datos
    /// adicionales autenticados se borran al finalizar.
    /// La clave recibida no se modifica ni se borra;
    /// su ciclo de vida corresponde al llamador.
    /// </para>
    /// </remarks>
    public static EncryptedData Encrypt(
        string plaintext,
        byte[] key,
        string keyId)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        if (key.Length != 32)
            throw new ArgumentException(
                "La clave debe tener 32 bytes.",
                nameof(key));

        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext = new byte[plainBytes.Length];
        byte[] tag = new byte[16];

        byte[] aad = CredentialMetadata.CreateAuthenticatedData(
            CredentialMetadata.CurrentVersion,
            CredentialMetadata.Algorithm,
            keyId);

        try
        {
            using var aes = new AesGcm(key, 16);

            aes.Encrypt(
                nonce,
                plainBytes,
                ciphertext,
                tag,
                aad);

            return new EncryptedData
            {
                Version = CredentialMetadata.CurrentVersion,
                Algorithm = CredentialMetadata.Algorithm,
                KeyId = keyId,
                Nonce = Convert.ToBase64String(nonce),
                Ciphertext = Convert.ToBase64String(ciphertext),
                Tag = Convert.ToBase64String(tag)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            CryptographicOperations.ZeroMemory(aad);
        }
    }
}

/// <summary>
/// Representa el resultado de una operación de cifrado
/// AES-256-GCM y los metadatos necesarios para descifrarlo.
/// </summary>
/// <remarks>
/// Los valores binarios se representan mediante cadenas Base64
/// para facilitar su serialización y almacenamiento.
/// </remarks>
public sealed class EncryptedData
{
    /// <summary>
    /// Versión del formato de cifrado utilizado.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Nombre del algoritmo criptográfico utilizado.
    /// </summary>
    public string Algorithm { get; init; } = "";

    /// <summary>
    /// Identificador de la clave empleada para cifrar.
    /// </summary>
    public string KeyId { get; init; } = "";

    /// <summary>
    /// Nonce utilizado durante el cifrado, codificado en Base64.
    /// </summary>
    public string Nonce { get; init; } = "";

    /// <summary>
    /// Contenido cifrado, codificado en Base64.
    /// </summary>
    public string Ciphertext { get; init; } = "";

    /// <summary>
    /// Etiqueta de autenticación GCM, codificada en Base64.
    /// </summary>
    public string Tag { get; init; } = "";
}
