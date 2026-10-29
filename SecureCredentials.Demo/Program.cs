using System.Security.Cryptography;
using SecureCredentials;

Console.WriteLine("=== PRUEBA AES-256-GCM VERSION 2 ===");

string filePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "credenciales-demo-v2.enc");

const string keyId = "aes-test-001";

IKeyProvider provider = new LinuxSecretServiceKeyProvider();
byte[] key = await provider.GetKeyAsync(keyId);

try
{
    string credentials = """
    {
        "servidor": "oracle-ejemplo",
        "usuario": "usuario_prueba",
        "password": "clave_ficticia"
    }
    """;

    // 1. Crear archivo cifrado de versión 2.
    EncryptedData encrypted = CredentialEncryptor.Encrypt(
        credentials,
        key,
        keyId);

    await CredentialFileStorage.SaveAsync(filePath, encrypted);

    Console.WriteLine("1. Archivo cifrado creado.");
    Console.WriteLine($"2. Version: {encrypted.Version}");
    Console.WriteLine($"3. KeyId: {encrypted.KeyId}");

    // 2. Cargar y descifrar el archivo.
    EncryptedData loaded =
        await CredentialFileStorage.LoadAsync(filePath);

    string recovered = CredentialDecryptor.Decrypt(loaded, key);

    Console.WriteLine(
        recovered == credentials
            ? "4. CORRECTO: Descifrado version 2."
            : "4. ERROR: Contenido inesperado.");

    // 3. Simular una manipulación del identificador.
    EncryptedData tampered = new()
    {
        Version = loaded.Version,
        Algorithm = loaded.Algorithm,
        KeyId = "clave-manipulada",
        Nonce = loaded.Nonce,
        Ciphertext = loaded.Ciphertext,
        Tag = loaded.Tag
    };

    try
    {
        _ = CredentialDecryptor.Decrypt(tampered, key);

        Console.WriteLine(
            "5. ERROR: Se aceptaron metadatos manipulados.");
    }
    catch (AuthenticationTagMismatchException)
    {
        Console.WriteLine(
            "5. CORRECTO: AAD detecta KeyId manipulado.");
    }

    // 4. Manipular la versión.
    EncryptedData wrongVersion = new()
    {
        Version = 1,
        Algorithm = loaded.Algorithm,
        KeyId = loaded.KeyId,
        Nonce = loaded.Nonce,
        Ciphertext = loaded.Ciphertext,
        Tag = loaded.Tag
    };

    try
    {
        _ = CredentialDecryptor.Decrypt(wrongVersion, key);
        Console.WriteLine("6. ERROR: Version manipulada aceptada.");
    }
    catch (AuthenticationTagMismatchException)
    {
        Console.WriteLine("6. CORRECTO: Version manipulada rechazada.");
    }

    // 5. Manipular el algoritmo.
    EncryptedData wrongAlgorithm = new()
    {
        Version = loaded.Version,
        Algorithm = "AES-128-GCM",
        KeyId = loaded.KeyId,
        Nonce = loaded.Nonce,
        Ciphertext = loaded.Ciphertext,
        Tag = loaded.Tag
    };

    try
    {
        _ = CredentialDecryptor.Decrypt(wrongAlgorithm, key);
        Console.WriteLine("7. ERROR: Algoritmo manipulado aceptado.");
    }
    catch (NotSupportedException)
    {
        Console.WriteLine("7. CORRECTO: Algoritmo manipulado rechazado.");
    }

    // 6. Manipular el contenido cifrado.
    byte[] modifiedCiphertext =
        Convert.FromBase64String(loaded.Ciphertext);

    if (modifiedCiphertext.Length == 0)
        throw new InvalidOperationException(
            "El texto cifrado de prueba esta vacio.");

    modifiedCiphertext[0] ^= 0x01;

    EncryptedData wrongCiphertext = new()
    {
        Version = loaded.Version,
        Algorithm = loaded.Algorithm,
        KeyId = loaded.KeyId,
        Nonce = loaded.Nonce,
        Ciphertext = Convert.ToBase64String(modifiedCiphertext),
        Tag = loaded.Tag
    };

    try
    {
        _ = CredentialDecryptor.Decrypt(wrongCiphertext, key);
        Console.WriteLine("8. ERROR: Contenido manipulado aceptado.");
    }
    catch (AuthenticationTagMismatchException)
    {
        Console.WriteLine("8. CORRECTO: Contenido manipulado rechazado.");
    }
    finally
    {
        CryptographicOperations.ZeroMemory(modifiedCiphertext);
    }

    Console.WriteLine($"Archivo: {filePath}");
}
finally
{
    CryptographicOperations.ZeroMemory(key);
}