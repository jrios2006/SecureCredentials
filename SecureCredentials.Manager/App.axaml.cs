using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using SecureCredentials.Manager.Services;

namespace SecureCredentials.Manager;


// Archivo: App.axaml.cs
// Proyecto: SecureCredentials.Manager
// Descripción: Aplicación Avalonia y configuración del ciclo de vida de escritorio.

/// <summary>

/// Aplicación Avalonia y configuración del ciclo de vida de escritorio.

/// </summary>

/// <remarks>

/// Inicializa el registro local antes de crear la ventana principal.

/// </remarks>

public partial class App : Application
{
    /// <summary>
    /// Carga los recursos y estilos declarados en App.axaml.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Inicializa el registro y crea la ventana principal al finalizar el arranque.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        ManagerLogger.Initialize();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}