
namespace SecureCredentials;

// Archivo: IKeyProvider.cs
// Proyecto: SecureCredentials
// Descripción: Contrato común para proveedores de claves criptográficas.

/// <summary>
/// Define el contrato para obtener claves criptográficas
/// utilizadas en el cifrado y descifrado de credenciales.
/// </summary>
/// <remarks>
/// Las implementaciones determinan cómo se obtiene
/// y administra cada clave.
///
/// El consumidor debe borrar de memoria la clave
/// recibida cuando ya no sea necesaria.
/// </remarks>
public interface IKeyProvider
{
    /// <summary>
    /// Obtiene la clave criptográfica asociada
    /// al identificador solicitado.
    /// </summary>
    /// <param name="keyId">
    /// Identificador de la clave que se desea obtener.
    /// </param>
    /// <param name="cancellationToken">
    /// Token opcional para cancelar la operación.
    /// </param>
    /// <returns>
    /// Una tarea cuyo resultado contiene la clave
    /// criptográfica en un array de bytes.
    /// </returns>
    Task<byte[]> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}
