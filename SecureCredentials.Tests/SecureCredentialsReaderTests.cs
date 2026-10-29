
using System.Security.Cryptography;
using SecureCredentials;

namespace SecureCredentials.Tests;

// Archivo: SecureCredentialsReaderTests.cs
// Proyecto: SecureCredentials.Tests
// Descripción: Pruebas de integración de la API de lectura.

/// <summary>
/// Pruebas de integración de SecureCredentialsReader.
/// </summary>
public sealed class SecureCredentialsReaderTests
{
    /// <summary>
    /// Comprueba que ReadAsync recupera exactamente el texto
    /// original de un fichero cifrado mediante AES-256-GCM.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ReturnsOriginalText()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Directorio temporal exclusivo de la prueba.
        string directory = Path.Combine(
            Path.GetTempPath(),
            "securecredentials-reader-" +
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        const string originalText =
            "Prueba SecureCredentialsReader\n" +
            "Caracteres UTF-8: áéíóú ñ €\n" +
            "Saltos de línea y símbolos: !@#$%^&*()\n" +
            "{\"servidor\":\"localhost\",\"puerto\":5432}\n";

        byte[]? key = null;

        try
        {
            // 1. Obtener la clave integrada.
            var provider = new EmbeddedKeyProvider();

            key = await provider.GetKeyAsync(
                EmbeddedKeyProvider.DefaultKeyId);

            // 2. Cifrar el texto original.
            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                originalText,
                key,
                EmbeddedKeyProvider.DefaultKeyId);

            // 3. Guardar el fichero cifrado.
            await CredentialFileStorage.SaveAsync(
                filePath,
                encrypted);

            // 4. Leer y descifrar mediante la API pública.
            string recoveredText =
                await SecureCredentialsReader.ReadAsync(filePath);

            // 5. Comprobar que el texto es idéntico.
            Assert.Equal(originalText, recoveredText);
        }
        finally
        {
            // Borrar la clave temporal.
            if (key is not null)
                CryptographicOperations.ZeroMemory(key);

            // Eliminar el directorio de prueba.
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
