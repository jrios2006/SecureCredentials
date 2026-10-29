
using System;
using System.IO;
using System.Text.Json;

namespace SecureCredentials.Manager.Services;


// Archivo: ManagerSettings.cs
// Proyecto: SecureCredentials.Manager
// Descripción: Preferencias locales del gestor.

/// <summary>

/// Preferencias locales del gestor.

/// </summary>

/// <remarks>

/// Conserva la última carpeta utilizada en .config/securecredentials-manager/settings.json dentro del perfil del usuario.

/// </remarks>

internal sealed class ManagerSettings
{
    /// <summary>
    /// Obtiene o establece la última carpeta utilizada.
    /// </summary>
    public string? LastDirectory { get; set; }

    private static readonly string ConfigDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".config",
            "securecredentials-manager");

    private static readonly string ConfigFile =
        Path.Combine(ConfigDirectory, "settings.json");

    /// <summary>
    /// Lee las preferencias; utiliza valores predeterminados si no están disponibles.
    /// </summary>
    /// <returns>
    /// Preferencias recuperadas o una instancia con valores predeterminados.
    /// </returns>
    public static ManagerSettings Load()
    {
        try
        {
            if (!File.Exists(ConfigFile))
                return new ManagerSettings();

            string json = File.ReadAllText(ConfigFile);

            return JsonSerializer.Deserialize<ManagerSettings>(json)
                ?? new ManagerSettings();
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            ManagerLogger.Warning(
                "No se pudo recuperar la configuración anterior.");

            return new ManagerSettings();
        }
    }

    /// <summary>
    /// Guarda la carpeta del archivo indicado para futuros diálogos.
    /// </summary>
    /// <param name="filePath">
    /// Ruta del archivo abierto o guardado.
    /// </param>
    public void RememberDirectory(string filePath)
    {
        string? directory = Path.GetDirectoryName(
            Path.GetFullPath(filePath));

        if (string.IsNullOrWhiteSpace(directory) ||
            !Directory.Exists(directory))
        {
            return;
        }

        LastDirectory = directory;

        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(
                    ConfigDirectory,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute);
            }

            string json = JsonSerializer.Serialize(
                this,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(ConfigFile, json);

            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(
                    ConfigFile,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite);
            }

            ManagerLogger.Info(
                "Última carpeta utilizada guardada.");
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            ManagerLogger.Warning(
                "No se pudo guardar la última carpeta utilizada.");
        }
    }
}
