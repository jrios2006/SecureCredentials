
using System;
using System.IO;
using System.Text;

namespace SecureCredentials.Manager.Services;


// Archivo: ManagerLogger.cs
// Proyecto: SecureCredentials.Manager
// Descripción: Registro local de actividad y diagnóstico.

/// <summary>
/// Registro local de actividad de SecureCredentials Manager.
/// Se reinicia en cada ejecución.
/// </summary>
/// <summary>
/// Registro local de actividad y diagnóstico.
/// </summary>
/// <remarks>
/// El registro se reinicia al iniciar cada ejecución y se almacena en el perfil del usuario, bajo .local/state/securecredentials-manager/manager.log. No debe utilizarse para registrar secretos.
/// </remarks>
internal static class ManagerLogger
{
    private static readonly object Sync = new();

    private static readonly string LogDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".local",
            "state",
            "securecredentials-manager");

    /// <summary>
    /// Obtiene la ruta completa del archivo de registro.
    /// </summary>
    /// <returns>
    /// Ruta del archivo manager.log.
    /// </returns>
    public static string LogPath =>
        Path.Combine(LogDirectory, "manager.log");

    /// <summary>
    /// Crea el directorio, reinicia el registro y anota el inicio de la aplicación.
    /// </summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(LogDirectory);

        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(
                LogDirectory,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }

        lock (Sync)
        {
            using var stream = new FileStream(
                LogPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);

            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(
                    LogPath,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite);
            }
        }

        Info("Aplicación iniciada.");
    }

    /// <summary>
    /// Escribe un mensaje informativo.
    /// </summary>
    /// <param name="message">
    /// Mensaje de actividad.
    /// </param>
    public static void Info(string message)
    {
        Write("INFO", message);
    }

    /// <summary>
    /// Escribe un aviso de diagnóstico.
    /// </summary>
    /// <param name="message">
    /// Mensaje de advertencia.
    /// </param>
    public static void Warning(string message)
    {
        Write("WARN", message);
    }

    /// <summary>
    /// Registra el tipo y mensaje de una excepción sin incluir la traza completa.
    /// </summary>
    /// <param name="message">
    /// Contexto del error.
    /// </param>
    /// <param name="exception">
    /// Excepción asociada.
    /// </param>
    public static void Error(
        string message,
        Exception exception)
    {
        Write(
            "ERROR",
            $"{message} | " +
            $"Excepción: {exception.GetType().Name} | " +
            $"Mensaje: {Sanitize(exception.Message)}");
    }

    /// <summary>
    /// Añade una entrada con fecha, nivel y texto normalizado.
    /// </summary>
    /// <param name="level">
    /// Nivel del registro.
    /// </param>
    /// <param name="message">
    /// Texto que se anotará.
    /// </param>
    private static void Write(
        string level,
        string message)
    {
        string line =
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} " +
            $"[{level}] {Sanitize(message)}" +
            Environment.NewLine;

        lock (Sync)
        {
            File.AppendAllText(
                LogPath,
                line,
                Encoding.UTF8);
        }
    }

    /// <summary>
    /// Sustituye saltos de línea para mantener una entrada por línea.
    /// </summary>
    /// <param name="Sanitize">
    /// Texto que se normalizará.
    /// </param>
    /// <returns>
    /// Texto sin saltos de línea.
    /// </returns>
    private static string Sanitize(string message)
    {
        return message
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}
