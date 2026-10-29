
using System.Security.Cryptography;
using SecureCredentials;
using Xunit;

namespace SecureCredentials.Tests;

public class CredentialEncryptionTests
{
    // PRUEBA 1: Cifrar y descifrar correctamente.
    [Fact]
    public void EncryptDecrypt_Version2_RestoresOriginalText()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        const string keyId = "test-key-001";
        const string originalText =
            "{\"usuario\":\"prueba\",\"password\":\"ficticia\"}";

        try
        {
            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                originalText,
                key,
                keyId);

            // Comprobar los metadatos.
            Assert.Equal(2, encrypted.Version);
            Assert.Equal("AES-256-GCM", encrypted.Algorithm);
            Assert.Equal(keyId, encrypted.KeyId);

            // Descifrar y verificar el contenido.
            string recovered = CredentialDecryptor.Decrypt(
                encrypted,
                key);

            Assert.Equal(originalText, recovered);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    // PRUEBA 2: Rechazar una clave incorrecta.
    [Fact]
    public void Decrypt_WithWrongKey_ThrowsAuthenticationException()
    {
        byte[] correctKey = RandomNumberGenerator.GetBytes(32);
        byte[] wrongKey = RandomNumberGenerator.GetBytes(32);

        const string originalText = "credenciales de prueba";
        const string keyId = "test-key-001";

        try
        {
            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                originalText,
                correctKey,
                keyId);

            // Intentar descifrar utilizando otra clave.
            Assert.Throws<AuthenticationTagMismatchException>(() =>
            {
                CredentialDecryptor.Decrypt(encrypted, wrongKey);
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(correctKey);
            CryptographicOperations.ZeroMemory(wrongKey);
        }
    }
    // PRUEBA 3: Detectar un KeyId manipulado.
    [Fact]
    public void Decrypt_WithTamperedKeyId_ThrowsAuthenticationException()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        try
        {
            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                "credenciales ficticias",
                key,
                "test-key-001");

            // Copiar los datos y modificar solamente KeyId.
            EncryptedData tampered = new()
            {
                Version = encrypted.Version,
                Algorithm = encrypted.Algorithm,
                KeyId = "test-key-manipulada",
                Nonce = encrypted.Nonce,
                Ciphertext = encrypted.Ciphertext,
                Tag = encrypted.Tag
            };

            Assert.Throws<AuthenticationTagMismatchException>(() =>
            {
                CredentialDecryptor.Decrypt(tampered, key);
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
    // PRUEBA 4: Detectar contenido cifrado manipulado.
    [Fact]
    public void Decrypt_WithTamperedCiphertext_ThrowsAuthenticationException()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        try
        {
            EncryptedData encrypted = CredentialEncryptor.Encrypt(
                "credenciales ficticias",
                key,
                "test-key-001");

            // Modificar un byte del contenido cifrado.
            byte[] modifiedCiphertext =
                Convert.FromBase64String(encrypted.Ciphertext);

            try
            {
                Assert.NotEmpty(modifiedCiphertext);

                modifiedCiphertext[0] ^= 0x01;

                EncryptedData tampered = new()
                {
                    Version = encrypted.Version,
                    Algorithm = encrypted.Algorithm,
                    KeyId = encrypted.KeyId,
                    Nonce = encrypted.Nonce,
                    Ciphertext = Convert.ToBase64String(modifiedCiphertext),
                    Tag = encrypted.Tag
                };

                Assert.Throws<AuthenticationTagMismatchException>(() =>
                {
                    CredentialDecryptor.Decrypt(tampered, key);
                });
            }
            finally
            {
                CryptographicOperations.ZeroMemory(modifiedCiphertext);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
    // PRUEBA 5: Compatibilidad con archivos de versión 1.
    [Fact]
    public void Decrypt_Version1_RestoresOriginalText()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] plaintext = System.Text.Encoding.UTF8.GetBytes(
            "credenciales antiguas de prueba");

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];

        try
        {
            // Reproducir el cifrado original de versión 1, sin AAD.
            using (var aes = new AesGcm(key, 16))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            EncryptedData legacyData = new()
            {
                Version = 1,
                Algorithm = "AES-256-GCM",
                KeyId = "",
                Nonce = Convert.ToBase64String(nonce),
                Ciphertext = Convert.ToBase64String(ciphertext),
                Tag = Convert.ToBase64String(tag)
            };

            // El descifrador actual debe seguir admitiendo versión 1.
            string recovered = CredentialDecryptor.Decrypt(
                legacyData,
                key);

            Assert.Equal(
                "credenciales antiguas de prueba",
                recovered);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }


}
