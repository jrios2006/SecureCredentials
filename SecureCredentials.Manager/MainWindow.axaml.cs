
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;

using SecureCredentials;
using SecureCredentials.Manager.Services;

namespace SecureCredentials.Manager;


// Archivo: MainWindow.axaml.cs
// Proyecto: SecureCredentials.Manager
// Descripción: Ventana principal de edición y gestión de documentos cifrados.

/// <summary>

/// Ventana principal de edición y gestión de documentos cifrados.

/// </summary>

/// <remarks>

/// Coordina la interfaz, la configuración de última carpeta, el registro y las operaciones de cifrado/descifrado. El editor maneja texto descifrado en memoria mientras está abierto.

/// </remarks>

public partial class MainWindow : Window
{
    private readonly ManagerSettings _settings;

    private string? _currentFilePath;
    private bool _isModified;
    private bool _isBusy;

    private const double DefaultEditorFontSize = 16;
    private const double MinEditorFontSize = 10;
    private const double MaxEditorFontSize = 32;
    private const double EditorFontStep = 2;
    /// <summary>
    /// Abre la ventana modal de información del producto.
    /// </summary>
    /// <param name="sender">
    /// Control que originó el evento.
    /// </param>
    /// <param name="e">
    /// Argumentos del evento de clic.
    /// </param>
    private async void About_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow();
        await aboutWindow.ShowDialog(this);
    }
    /// <summary>
    /// Inicializa la interfaz, el editor, las preferencias y el registro de eventos.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        Editor.FontSize = DefaultEditorFontSize;
        Editor.KeyDown += Editor_KeyDown;

        _settings = ManagerSettings.Load();

        ManagerLogger.Info(
            "Ventana principal inicializada.");

        if (!string.IsNullOrWhiteSpace(
            _settings.LastDirectory))
        {
            ManagerLogger.Info(
                "Configuración de última carpeta recuperada.");
        }

        Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(Editor.Text))
            {
                _isModified = true;
                UpdateFileName();
            }
        };

        Closed += (_, _) =>
        {
            ManagerLogger.Info(
                "Ventana principal cerrada.");
        };
    }

    /// <summary>
    /// Aumenta el tamaño de letra del editor respetando el máximo.
    /// </summary>
    /// <param name="sender">
    /// Control que originó el evento.
    /// </param>
    /// <param name="e">
    /// Argumentos del evento de clic.
    /// </param>
    private void IncreaseFont_Click(object? sender, RoutedEventArgs e)
    {
        ChangeEditorFontSize(EditorFontStep);
    }

    /// <summary>
    /// Reduce el tamaño de letra del editor respetando el mínimo.
    /// </summary>
    /// <param name="sender">
    /// Control que originó el evento.
    /// </param>
    /// <param name="e">
    /// Argumentos del evento de clic.
    /// </param>
    private void DecreaseFont_Click(object? sender, RoutedEventArgs e)
    {
        ChangeEditorFontSize(-EditorFontStep);
    }

    /// <summary>
    /// Ajusta el tamaño de letra del editor entre 10 y 32 puntos.
    /// </summary>
    /// <param name="delta">
    /// Variación, en puntos, que se aplica al tamaño actual.
    /// </param>
    private void ChangeEditorFontSize(double delta)
    {
        Editor.FontSize = Math.Clamp(
            Editor.FontSize + delta,
            MinEditorFontSize,
            MaxEditorFontSize);
    }

    /// <summary>
    /// Gestiona Ctrl++, Ctrl+- y Ctrl+0 cuando el editor tiene el foco.
    /// </summary>
    /// <param name="sender">
    /// Control que originó el evento.
    /// </param>
    /// <param name="e">
    /// Argumentos del evento de teclado.
    /// </param>
    private void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0)
            return;

        if (e.Key is Key.Add or Key.OemPlus)
        {
            ChangeEditorFontSize(EditorFontStep);
            e.Handled = true;
        }
        else if (e.Key is Key.Subtract or Key.OemMinus)
        {
            ChangeEditorFontSize(-EditorFontStep);
            e.Handled = true;
        }
        else if (e.Key is Key.D0 or Key.NumPad0)
        {
            Editor.FontSize = DefaultEditorFontSize;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Actualiza el nombre visible y señala los cambios sin guardar.
    /// </summary>
    private void UpdateFileName()
    {
        string name = _currentFilePath is null
            ? "Documento nuevo"
            : Path.GetFileName(_currentFilePath);

        FileNameText.Text =
            _isModified ? $"{name} *" : name;
    }

    /// <summary>
    /// Muestra un estado en pantalla y lo registra como información.
    /// </summary>
    /// <param name="message">
    /// Mensaje de estado que se mostrará.
    /// </param>
    private void SetStatus(string message)
    {
        StatusText.Text = message;
        ManagerLogger.Info(message);
    }

    /// <summary>
    /// Recupera una carpeta inicial válida para los selectores de archivos.
    /// </summary>
    /// <returns>
    /// Carpeta de inicio, o null cuando no puede recuperarse.
    /// </returns>
    private async Task<IStorageFolder?> GetStartFolderAsync()
    {
        string? directory = _settings.LastDirectory;

        if (string.IsNullOrWhiteSpace(directory))
            return null;

        if (!Directory.Exists(directory))
        {
            ManagerLogger.Warning(
                "La última carpeta utilizada ya no existe.");

            return null;
        }

        try
        {
            string fullPath = Path.GetFullPath(directory);

            return await StorageProvider
                .TryGetFolderFromPathAsync(
                    new Uri(fullPath + Path.DirectorySeparatorChar));
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            ManagerLogger.Warning(
                "No se pudo utilizar la última carpeta.");

            return null;
        }
    }

    /// <summary>
    /// Solicita confirmación antes de descartar modificaciones.
    /// </summary>
    /// <returns>
    /// True si no hay cambios o el usuario confirma descartarlos.
    /// </returns>
    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!_isModified)
            return true;

        ManagerLogger.Info(
            "Solicitando confirmación para descartar cambios.");

        var dialog = new Window
        {
            Title = "Cambios sin guardar",
            Width = 430,
            Height = 180,
            CanResize = false,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner
        };

        var discardButton = new Button
        {
            Content = "Descartar cambios"
        };

        var cancelButton = new Button
        {
            Content = "Cancelar"
        };

        discardButton.Click += (_, _) =>
            dialog.Close(true);

        cancelButton.Click += (_, _) =>
            dialog.Close(false);

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text =
                        "El documento contiene cambios sin guardar. " +
                        "¿Deseas descartarlos?",
                    TextWrapping =
                        Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation =
                        Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 12,
                    HorizontalAlignment =
                        Avalonia.Layout.HorizontalAlignment.Right,
                    Children =
                    {
                        cancelButton,
                        discardButton
                    }
                }
            }
        };

        bool discard =
            await dialog.ShowDialog<bool>(this);

        if (discard)
        {
            ManagerLogger.Warning(
                "El usuario confirmó descartar cambios.");
        }
        else
        {
            ManagerLogger.Info(
                "El usuario conservó los cambios.");
        }

        return discard;
    }

    /// <summary>
    /// Muestra un cuadro de diálogo modal con un mensaje de error.
    /// </summary>
    /// <param name="title">
    /// Título del cuadro de diálogo.
    /// </param>
    /// <param name="message">
    /// Mensaje explicativo que se mostrará.
    /// </param>
    private async Task ShowErrorAsync(
        string title,
        string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 520,
            Height = 220,
            MinWidth = 380,
            CanResize = false,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner
        };

        var closeButton = new Button
        {
            Content = "Aceptar",
            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Right
        };

        closeButton.Click += (_, _) =>
            dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 16,
            Children =
            {
                new ScrollViewer
                {
                    MaxHeight = 125,
                    Content = new TextBlock
                    {
                        Text = message,
                        TextWrapping =
                            Avalonia.Media.TextWrapping.Wrap
                    }
                },
                closeButton
            }
        };

        await dialog.ShowDialog(this);
    }

    /// <summary>
    /// Prepara un documento vacío, previa confirmación si hay cambios.
    /// </summary>
    /// <param name="sender">
    /// Control que originó el evento.
    /// </param>
    /// <param name="e">
    /// Argumentos del evento de clic.
    /// </param>
    private async void New_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        ManagerLogger.Info(
            "El usuario seleccionó Nuevo documento.");

        _isBusy = true;

        try
        {
            if (!await ConfirmDiscardChangesAsync())
                return;

            Editor.Text = string.Empty;

            _currentFilePath = null;
            _isModified = false;

            UpdateFileName();

            SetStatus("Nuevo documento creado.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// Selecciona, descifra y carga un documento cifrado en el editor.
    /// </summary>
    /// <param name="sender">
    /// Control que originó el evento.
    /// </param>
    /// <param name="e">
    /// Argumentos del evento de clic.
    /// </param>
    private async void Open_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        ManagerLogger.Info(
            "El usuario seleccionó Abrir cifrado.");

        _isBusy = true;

        try
        {
            if (!await ConfirmDiscardChangesAsync())
                return;

            IStorageFolder? startFolder =
                await GetStartFolderAsync();

            var files =
                await StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions
                    {
                        Title = "Abrir archivo cifrado",
                        AllowMultiple = false,
                        SuggestedStartLocation = startFolder,

                        FileTypeFilter =
                        [
                            new FilePickerFileType(
                                "Archivos cifrados SecureCredentials")
                            {
                                Patterns = ["*.enc"]
                            },

                            new FilePickerFileType(
                                "Archivos JSON anteriores")
                            {
                                Patterns = ["*.json"]
                            },

                            new FilePickerFileType(
                                "Todos los archivos")
                            {
                                Patterns = ["*"]
                            }
                        ]
                    });

            if (files.Count == 0)
            {
                SetStatus("Apertura cancelada.");
                return;
            }

            string? path =
                files[0].TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(path))
            {
                ManagerLogger.Warning(
                    "Se seleccionó un archivo no local.");

                await ShowErrorAsync(
                    "Archivo no compatible",
                    "Selecciona un archivo del sistema " +
                    "de archivos local.");

                return;
            }

            string fileName = Path.GetFileName(path);

            SetStatus(
                $"Descifrando archivo: {fileName}");

            string plaintext;

            try
            {
                plaintext =
                    await SecureCredentialsReader.ReadAsync(path);
            }
            catch (KeyNotFoundException)
            {
                ManagerLogger.Warning(
                    $"Clave no disponible para el archivo: {fileName}");

                StatusText.Text =
                    "No se dispone de la clave necesaria.";

                await ShowErrorAsync(
                    "Clave de cifrado no disponible",
                    "El archivo utiliza un identificador " +
                    "de clave que esta aplicación no reconoce.\n\n" +
                    "Para descifrarlo necesitas disponer de " +
                    "la clave original correspondiente.\n\n" +
                    "El archivo no se ha modificado.");

                return;
            }

            Editor.Text = plaintext;

            _currentFilePath = path;
            _isModified = false;

            UpdateFileName();

            _settings.RememberDirectory(path);

            SetStatus(
                $"Archivo descifrado correctamente: {fileName}");
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            CryptographicException or
            JsonException or
            ArgumentException or
            NotSupportedException)
        {
            ManagerLogger.Error(
                "Error durante la apertura del archivo cifrado.",
                ex);

            StatusText.Text =
                "Error al abrir el archivo.";

            await ShowErrorAsync(
                "Error de lectura",
                ex.Message);
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// Cifra el contenido del editor y lo guarda en el destino elegido.
    /// </summary>
    /// <param name="sender">
    /// Control que originó el evento.
    /// </param>
    /// <param name="e">
    /// Argumentos del evento de clic.
    /// </param>
    private async void Save_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        ManagerLogger.Info(
            "El usuario seleccionó Guardar cifrado.");

        _isBusy = true;

        try
        {
            IStorageFolder? startFolder =
                await GetStartFolderAsync();

            var file =
                await StorageProvider.SaveFilePickerAsync(
                    new FilePickerSaveOptions
                    {
                        Title = "Guardar archivo cifrado",

                        SuggestedStartLocation = startFolder,

                        SuggestedFileName =
                            _currentFilePath is null
                                ? "credenciales.enc"
                                : Path.ChangeExtension(
                                    Path.GetFileName(
                                        _currentFilePath),
                                    ".enc"),

                        DefaultExtension = "enc",

                        ShowOverwritePrompt = true,

                        FileTypeChoices =
                        [
                            new FilePickerFileType(
                                "Archivos cifrados SecureCredentials")
                            {
                                Patterns = ["*.enc"]
                            }
                        ]
                    });

            if (file is null)
            {
                SetStatus("Guardado cancelado.");
                return;
            }

            string? path =
                file.TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(path))
            {
                ManagerLogger.Warning(
                    "Se seleccionó un destino no local.");

                await ShowErrorAsync(
                    "Destino no compatible",
                    "Selecciona una ruta del sistema " +
                    "de archivos local.");

                return;
            }

            string fileName = Path.GetFileName(path);

            SetStatus(
                $"Cifrando archivo: {fileName}");

            await SecureCredentialsWriter.WriteAsync(
                path,
                Editor.Text ?? string.Empty);

            _currentFilePath = path;
            _isModified = false;

            UpdateFileName();

            _settings.RememberDirectory(path);

            SetStatus(
                $"Archivo cifrado correctamente: {fileName}");
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            CryptographicException or
            ArgumentException or
            NotSupportedException)
        {
            ManagerLogger.Error(
                "Error durante el guardado cifrado.",
                ex);

            StatusText.Text =
                "Error al guardar el archivo.";

            await ShowErrorAsync(
                "Error de escritura",
                ex.Message);
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// Comprueba si el texto del editor tiene sintaxis JSON válida.
    /// </summary>
    /// <param name="sender">
    /// Control que originó el evento.
    /// </param>
    /// <param name="e">
    /// Argumentos del evento de clic.
    /// </param>
    private void ValidateJson_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        ManagerLogger.Info(
            "El usuario seleccionó Validar JSON.");

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(Editor.Text ?? "");

            SetStatus("JSON válido.");
        }
        catch (JsonException)
        {
            ManagerLogger.Warning(
                "Validación JSON incorrecta.");

            StatusText.Text = "JSON no válido.";
        }
    }
}
