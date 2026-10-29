
using System.Security.Cryptography;
using SecureCredentials;

namespace SecureCredentials.Tests;

/// <summary>
/// Pruebas de integración del circuito completo de cifrado,
/// almacenamiento, lectura y descifrado de credenciales.
/// </summary>
public sealed class CredentialIntegrationTests
{
    /// <summary>
    /// Comprueba que un texto UTF-8 puede cifrarse, guardarse
    /// en un fichero Linux, recuperarse y descifrarse sin cambios.
    /// </summary>
    [Fact]
    public async Task EncryptSaveLoadDecrypt_RestoresOriginalText()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Directorio temporal exclusivo de esta prueba.
        string directory = Path.Combine(
            Path.GetTempPath(),
            "securecredentials-integration-" +
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        const string originalText =
            "Prueba de integración AES-256-GCM\n" +
            "Caracteres: áéíóú ñ €\n" +
            "Línea con símbolos: !@#$%^&*()\n" +
            "{\"servidor\":\"localhost\",\"puerto\":5432}\n";

        byte[]? encryptionKey = null;
        byte[]? decryptionKey = null;

        try
        {
            // 1. Obtener la clave de cifrado.
            var encryptionProvider = new EmbeddedKeyProvider();

            encryptionKey = await encryptionProvider.GetKeyAsync(
                EmbeddedKeyProvider.DefaultKeyId);

            // 2. Cifrar el texto.
            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                originalText,
                encryptionKey,
                EmbeddedKeyProvider.DefaultKeyId);

            // 3. Guardar el fichero cifrado.
            await CredentialFileStorage.SaveAsync(
                filePath,
                encrypted);

            // 4. Comprobar que existe el fichero.
            Assert.True(File.Exists(filePath));

            // 5. Leer el fichero cifrado.
            EncryptedData loaded = await CredentialFileStorage.LoadAsync(
                filePath);

            // 6. Obtener la clave desde otra instancia.
            var decryptionProvider = new EmbeddedKeyProvider();

            decryptionKey = await decryptionProvider.GetKeyAsync(
                loaded.KeyId);

            // 7. Descifrar el contenido recuperado.
            string recoveredText = CredentialDecryptor.Decrypt(
                loaded,
                decryptionKey);

            // 8. Verificar que no se ha perdido información.
            Assert.Equal(originalText, recoveredText);
        }
        finally
        {
            // Borrar las claves temporales de memoria.
            if (encryptionKey is not null)
                CryptographicOperations.ZeroMemory(encryptionKey);

            if (decryptionKey is not null)
                CryptographicOperations.ZeroMemory(decryptionKey);

            // Eliminar los ficheros de prueba.
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
