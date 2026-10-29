
using System.Security.Cryptography;

namespace SecureCredentials;

// Archivo: DevelopmentKeyProvider.cs
// Proyecto: SecureCredentials
// Descripción: Proveedor de claves AES-256 temporales
//              para desarrollo y pruebas.

/// <summary>
/// Proporciona una clave criptográfica AES-256 temporal
/// generada aleatoriamente para desarrollo y pruebas.
/// </summary>
/// <remarks>
/// <para>
/// Al crear una instancia, este proveedor genera una
/// clave aleatoria de 32 bytes mediante un generador
/// criptográficamente seguro.
/// </para>
/// <para>
/// La misma instancia conserva su clave durante
/// toda su vida útil. Las distintas instancias
/// generan claves independientes.
/// </para>
/// <para>
/// Este proveedor no almacena la clave de forma persistente.
/// Por tanto, no debe utilizarse para cifrar información
/// que deba recuperarse después de destruir la instancia
/// o finalizar el proceso.
/// </para>
/// <para>
/// Implementa <see cref="IDisposable"/> para borrar
/// la clave interna de memoria cuando deja de utilizarse.
/// </para>
/// </remarks>
public sealed class DevelopmentKeyProvider : IKeyProvider, IDisposable
{
    private readonly byte[] _key =
        RandomNumberGenerator.GetBytes(32);

    private bool _disposed;

    /// <summary>
    /// Obtiene una copia de la clave AES-256 temporal
    /// administrada por esta instancia.
    /// </summary>
    /// <param name="keyId">
    /// Identificador de clave solicitado.
    /// Debe contener un valor no vacío.
    /// </param>
    /// <param name="cancellationToken">
    /// Token opcional para cancelar la operación.
    /// </param>
    /// <returns>
    /// Una tarea completada cuyo resultado contiene
    /// una copia independiente de la clave de 32 bytes.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// La instancia ya ha sido liberada.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// El identificador de clave es nulo, vacío
    /// o contiene únicamente espacios.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Se ha solicitado la cancelación de la operación.
    /// </exception>
    /// <remarks>
    /// <para>
    /// El parámetro <paramref name="keyId"/> se valida,
    /// pero no se utiliza para seleccionar distintas claves.
    /// Todas las solicitudes válidas realizadas a una misma
    /// instancia devuelven copias de la misma clave.
    /// </para>
    /// <para>
    /// El llamador recibe una copia para evitar que pueda
    /// modificar directamente la clave interna del proveedor.
    /// </para>
    /// <para>
    /// El llamador es responsable de borrar la copia
    /// cuando deje de necesitarla.
    /// </para>
    /// </remarks>
    public Task<byte[]> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        cancellationToken.ThrowIfCancellationRequested();

        // Se devuelve una copia para no exponer
        // la clave interna del proveedor.
        return Task.FromResult(
            (byte[])_key.Clone());
    }

    /// <summary>
    /// Libera los recursos sensibles administrados
    /// por este proveedor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sobrescribe con ceros la clave interna utilizando
    /// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/>.
    /// </para>
    /// <para>
    /// La operación es idempotente: las llamadas posteriores
    /// no realizan ninguna acción adicional.
    /// </para>
    /// <para>
    /// Este método no borra las copias de la clave
    /// entregadas previamente a los llamadores.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        CryptographicOperations.ZeroMemory(_key);
        _disposed = true;
    }
}
