
using System.Security.Cryptography;
using SecureCredentials;

namespace SecureCredentials.Tests;

/// <summary>
/// Pruebas del proveedor de claves AES-256 integrado.
/// </summary>
public sealed class EmbeddedKeyProviderTests
{
    /// <summary>
    /// Comprueba que la clave integrada tiene 256 bits.
    /// </summary>
    [Fact]
    public async Task GetKeyAsync_Returns32ByteKey()
    {
        var provider = new EmbeddedKeyProvider();

        byte[] key = await provider.GetKeyAsync(
            EmbeddedKeyProvider.DefaultKeyId);

        try
        {
            Assert.Equal(32, key.Length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>
    /// Comprueba que distintas instancias obtienen
    /// la misma clave integrada.
    /// </summary>
    [Fact]
    public async Task GetKeyAsync_ReturnsSameKeyAcrossInstances()
    {
        var firstProvider = new EmbeddedKeyProvider();
        var secondProvider = new EmbeddedKeyProvider();

        byte[] firstKey = await firstProvider.GetKeyAsync(
            EmbeddedKeyProvider.DefaultKeyId);

        byte[] secondKey = Array.Empty<byte>();

        try
        {
            secondKey = await secondProvider.GetKeyAsync(
                EmbeddedKeyProvider.DefaultKeyId);

            Assert.Equal(firstKey, secondKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(firstKey);
            CryptographicOperations.ZeroMemory(secondKey);
        }
    }

    /// <summary>
    /// Comprueba que cada llamada devuelve
    /// un array independiente.
    /// </summary>
    [Fact]
    public async Task GetKeyAsync_ReturnsIndependentArrays()
    {
        var provider = new EmbeddedKeyProvider();

        byte[] firstKey = await provider.GetKeyAsync(
            EmbeddedKeyProvider.DefaultKeyId);

        byte[] secondKey = Array.Empty<byte>();

        try
        {
            secondKey = await provider.GetKeyAsync(
                EmbeddedKeyProvider.DefaultKeyId);

            Assert.NotSame(firstKey, secondKey);
            Assert.Equal(firstKey, secondKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(firstKey);
            CryptographicOperations.ZeroMemory(secondKey);
        }
    }

    /// <summary>
    /// Comprueba que un identificador desconocido
    /// no permite recuperar la clave.
    /// </summary>
    [Fact]
    public async Task GetKeyAsync_RejectsUnknownKeyId()
    {
        var provider = new EmbeddedKeyProvider();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => provider.GetKeyAsync("unknown-key"));
    }

    /// <summary>
    /// Comprueba que se rechazan identificadores vacíos.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetKeyAsync_RejectsEmptyKeyId(string keyId)
    {
        var provider = new EmbeddedKeyProvider();

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.GetKeyAsync(keyId));
    }

    /// <summary>
    /// Comprueba que el proveedor respeta
    /// una cancelación previa.
    /// </summary>
    [Fact]
    public async Task GetKeyAsync_RespectsCancellation()
    {
        var provider = new EmbeddedKeyProvider();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetKeyAsync(
                EmbeddedKeyProvider.DefaultKeyId,
                cancellation.Token));
    }

    /// <summary>
    /// Comprueba que la clave integrada funciona
    /// con el cifrador y descifrador AES-GCM.
    /// </summary>
    [Fact]
    public async Task EmbeddedKey_EncryptsAndDecryptsText()
    {
        var provider = new EmbeddedKeyProvider();

        byte[] key = await provider.GetKeyAsync(
            EmbeddedKeyProvider.DefaultKeyId);

        const string original =
            "Prueba AES-256-GCM\n" +
            "Caracteres: áéíóú ñ €\n" +
            "{\"servidor\":\"localhost\",\"puerto\":5432}";

        try
        {
            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                original,
                key,
                EmbeddedKeyProvider.DefaultKeyId);

            string decrypted = CredentialDecryptor.Decrypt(
                encrypted,
                key);

            Assert.Equal(original, decrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
