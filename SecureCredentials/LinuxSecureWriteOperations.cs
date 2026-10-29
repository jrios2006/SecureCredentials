using Mono.Unix.Native;

namespace SecureCredentials;

/// <summary>
/// Operaciones de escritura Linux sustituibles por una implementacion
/// simulada en pruebas. No forma parte de la API publica.
/// </summary>
internal interface ILinuxSecureWriteOperations
{
    int OpenDirectory(string path);
    void ValidateDestinationAt(int directoryFd, string fileName);
    int CreateFileAt(int directoryFd, string fileName);
    void WriteAllBytes(int fileFd, byte[] content);
    void SyncFile(int fileFd);
    void RenameFileAt(int directoryFd, string sourceFileName, string destinationFileName);
    void UnlinkFileAt(int directoryFd, string fileName);
    void SyncDirectory(int directoryFd);
    void Close(int fd);
}

internal sealed class LinuxSecureWriteOperations : ILinuxSecureWriteOperations
{
    internal static readonly LinuxSecureWriteOperations Instance = new();

    private LinuxSecureWriteOperations() { }

    // La interfaz se utiliza exclusivamente para escribir documentos .enc.
    // Las claves privadas siguen utilizando la política estricta 700.
    public int OpenDirectory(string path) => LinuxSecureFileOperations.OpenDocumentDirectory(path);
    public void ValidateDestinationAt(int directoryFd, string fileName) =>
        LinuxSecureFileOperations.ValidateDestinationAt(directoryFd, fileName);
    public int CreateFileAt(int directoryFd, string fileName) =>
        LinuxSecureFileOperations.CreateFileAt(directoryFd, fileName);
    public void WriteAllBytes(int fileFd, byte[] content) =>
        LinuxSecureFileOperations.WriteAllBytes(fileFd, content);
    public void SyncFile(int fileFd) => LinuxSecureFileOperations.SyncFile(fileFd);
    public void RenameFileAt(int directoryFd, string sourceFileName, string destinationFileName) =>
        LinuxSecureFileOperations.RenameFileAt(directoryFd, sourceFileName, destinationFileName);
    public void UnlinkFileAt(int directoryFd, string fileName) =>
        LinuxSecureFileOperations.UnlinkFileAt(directoryFd, fileName);
    public void SyncDirectory(int directoryFd) => LinuxSecureFileOperations.SyncDirectory(directoryFd);

    public void Close(int fd)
    {
        if (Syscall.close(fd) != 0)
        {
            Errno error = Stdlib.GetLastError();
            throw new IOException($"No se pudo cerrar el descriptor POSIX. Error: {error}.");
        }
    }
}
