using Avalonia;
using System;

namespace SecureCredentials.Manager;


// Archivo: Program.cs
// Proyecto: SecureCredentials.Manager
// Descripción: Punto de entrada del gestor gráfico.

/// <summary>

/// Punto de entrada del gestor gráfico.

/// </summary>

/// <remarks>

/// Configura Avalonia y arranca el ciclo de vida de escritorio.

/// </remarks>

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    /// <summary>
    /// Inicia Avalonia con el ciclo de vida de aplicación de escritorio.
    /// </summary>
    /// <param name="args">
    /// Argumentos de la línea de comandos.
    /// </param>
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    /// <summary>
    /// Construye y configura la aplicación Avalonia para la plataforma actual.
    /// </summary>
    /// <returns>
    /// Constructor de la aplicación Avalonia.
    /// </returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
