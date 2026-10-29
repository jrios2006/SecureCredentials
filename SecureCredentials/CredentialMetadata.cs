
using System.Text;

namespace SecureCredentials;

// Archivo: CredentialMetadata.cs
// Proyecto: SecureCredentials
// Descripción: Definición y autenticación de los metadatos
//              utilizados en el cifrado AES-256-GCM.

/// <summary>
/// Define los metadatos del formato de cifrado y proporciona
/// operaciones para construir los datos adicionales autenticados.
/// </summary>
/// <remarks>
/// <para>
/// Los datos adicionales autenticados (AAD) permiten proteger
/// la integridad de determinados metadatos sin necesidad
/// de cifrarlos.
/// </para>
/// <para>
/// En la versión actual se autentican la versión del formato,
/// el algoritmo criptográfico y el identificador de clave.
/// </para>
/// <para>
/// Esta clase genera una representación binaria estable
/// de dichos metadatos para utilizarla durante el cifrado
/// y el descifrado AES-256-GCM.
/// </para>
/// <para>
/// El formato binario de los AAD forma parte del formato
/// criptográfico y no debe modificarse sin gestionar
/// explícitamente la compatibilidad con versiones anteriores.
/// </para>
/// </remarks>
public static class CredentialMetadata
{
    /// <summary>
    /// Versión actual del formato de cifrado.
    /// </summary>
    /// <remarks>
    /// La versión 2 incorpora autenticación de metadatos
    /// mediante datos adicionales autenticados (AAD).
    /// </remarks>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Identificador del algoritmo criptográfico utilizado.
    /// </summary>
    /// <remarks>
    /// AES-256-GCM utiliza una clave de 256 bits y proporciona
    /// cifrado autenticado.
    /// </remarks>
    public const string Algorithm = "AES-256-GCM";

    /// <summary>
    /// Construye la representación binaria de los metadatos
    /// que se utilizarán como datos adicionales autenticados.
    /// </summary>
    /// <param name="version">
    /// Versión del formato de cifrado.
    /// Debe coincidir con <see cref="CurrentVersion"/>.
    /// </param>
    /// <param name="algorithm">
    /// Identificador del algoritmo criptográfico.
    /// Debe coincidir con <see cref="Algorithm"/>.
    /// </param>
    /// <param name="keyId">
    /// Identificador de la clave utilizada para el cifrado.
    /// </param>
    /// <returns>
    /// Un array de bytes que contiene la representación
    /// binaria de los metadatos autenticados.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// La versión o el algoritmo indicados no son compatibles.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// El identificador de clave está vacío o su
    /// representación UTF-8 supera los 256 bytes.
    /// </exception>
    /// <remarks>
    /// <para>
    /// La representación binaria contiene:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// Un prefijo ASCII que identifica el formato,
    /// la versión y el algoritmo.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// La longitud del identificador de clave,
    /// expresada como un entero de 32 bits en
    /// orden de bytes big-endian.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// El identificador de clave codificado en UTF-8.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// La longitud explícita del identificador evita
    /// ambigüedades en la representación de los campos.
    /// </para>
    /// </remarks>
    public static byte[] CreateAuthenticatedData(
        int version,
        string algorithm,
        string keyId)
    {
        if (version != CurrentVersion)
            throw new NotSupportedException(
                "Versión de metadatos no compatible.");

        if (algorithm != Algorithm)
            throw new NotSupportedException(
                "Algoritmo no compatible.");

        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        // Representación inequívoca y estable de los metadatos.
        // La longitud evita ambigüedades entre campos.
        byte[] keyIdBytes = Encoding.UTF8.GetBytes(keyId);

        if (keyIdBytes.Length > 256)
            throw new ArgumentException(
                "El identificador de clave es demasiado largo.",
                nameof(keyId));

        byte[] prefix = Encoding.ASCII.GetBytes(
            "SecureCredentials|2|AES-256-GCM|");

        byte[] result = new byte[
            prefix.Length + 4 + keyIdBytes.Length];

        prefix.CopyTo(result, 0);

        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(
            result.AsSpan(prefix.Length, 4),
            keyIdBytes.Length);

        keyIdBytes.CopyTo(result, prefix.Length + 4);

        return result;
    }
}
