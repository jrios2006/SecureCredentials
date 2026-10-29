
using System.Text.Json;
using SecureCredentials;

namespace SecureCredentials.Tests;

// Archivo: SecureCredentialsWriterTests.cs
// Proyecto: SecureCredentials.Tests
// Descripción: Pruebas de integración de la API de escritura.

/// <summary>
/// Pruebas de integración de SecureCredentialsWriter.
/// </summary>
public sealed class SecureCredentialsWriterTests
{
    /// <summary>
    /// Comprueba que WriteAsync crea un fichero cifrado
    /// que puede recuperarse mediante ReadAsync.
    /// </summary>
    [Fact]
    public async Task WriteAsync_CreatesReadableEncryptedFile()
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Directorio temporal exclusivo de la prueba.
        string directory = Path.Combine(
            Path.GetTempPath(),
            "securecredentials-writer-" +
            Guid.NewGuid().ToString("N"));

        string filePath = Path.Combine(
            directory,
            "credentials.enc");

        const string originalText =
            "Prueba SecureCredentialsWriter\n" +
            "Caracteres UTF-8: áéíóú ñ €\n" +
            "Contenido genérico: JSON, XML o texto libre\n" +
            "{\"servidor\":\"localhost\",\"puerto\":5432}\n";

        try
        {
            // 1. Cifrar y guardar mediante la API pública.
            await SecureCredentialsWriter.WriteAsync(
                filePath,
                originalText);

            // 2. Comprobar que se ha creado el fichero.
            Assert.True(File.Exists(filePath));

            // 3. Comprobar que no contiene el texto original.
            string fileContent = await File.ReadAllTextAsync(filePath);

            Assert.DoesNotContain(
                originalText,
                fileContent,
                StringComparison.Ordinal);

            // 4. Comprobar que contiene el formato cifrado.
            using (JsonDocument document =
                   JsonDocument.Parse(fileContent))
            {
                JsonElement root = document.RootElement;

                Assert.True(root.TryGetProperty(
                    "Ciphertext", out _));

                Assert.True(root.TryGetProperty(
                    "Nonce", out _));

                Assert.True(root.TryGetProperty(
                    "Tag", out _));

                Assert.Equal(
                    EmbeddedKeyProvider.DefaultKeyId,
                    root.GetProperty("KeyId").GetString());
            }

            // 5. Leer y descifrar mediante la API pública.
            string recoveredText =
                await SecureCredentialsReader.ReadAsync(filePath);

            // 6. Comprobar que el texto se recupera sin cambios.
            Assert.Equal(originalText, recoveredText);
        }
        finally
        {
            // Eliminar los ficheros temporales.
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
