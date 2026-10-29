
using System;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using SecureCredentials;

namespace SecureCredentials.Manager;


// Archivo: AboutWindow.axaml.cs
// Proyecto: SecureCredentials.Manager
// Descripción: Ventana informativa del gestor y de la biblioteca utilizada.

/// <summary>

/// Ventana informativa del gestor y de la biblioteca utilizada.

/// </summary>

/// <remarks>

/// Muestra versiones obtenidas de los ensamblados y metadatos del producto.

/// </remarks>

public partial class AboutWindow : Window
{
    /// <summary>
    /// Inicializa la ventana y presenta los datos del producto.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
        LoadProductInformation();
    }

    /// <summary>
    /// Carga los controles declarados en la vista Avalonia.
    /// </summary>
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Obtiene de los ensamblados las versiones y los metadatos visibles.
    /// </summary>
    private void LoadProductInformation()
    {
        // Ensamblado de la aplicación gráfica.
        Assembly managerAssembly = typeof(AboutWindow).Assembly;

        // Ensamblado de la biblioteca SecureCredentials.
        Assembly libraryAssembly =
            typeof(SecureCredentialsReader).Assembly;

        // Versiones reales compiladas.
        string managerVersion = GetProductVersion(managerAssembly);
        string libraryVersion = GetProductVersion(libraryAssembly);

        // Metadatos configurados en el .csproj del gestor.
        string description =
            managerAssembly
                .GetCustomAttribute<AssemblyDescriptionAttribute>()
                ?.Description
            ?? "Aplicación para administrar documentos cifrados.";

        string company =
            managerAssembly
                .GetCustomAttribute<AssemblyCompanyAttribute>()
                ?.Company
            ?? "No especificado";

        string copyright =
            managerAssembly
                .GetCustomAttribute<AssemblyCopyrightAttribute>()
                ?.Copyright
            ?? "Copyright no especificado";

        // Actualizar los controles definidos en el XAML.
        this.FindControl<TextBlock>("ManagerVersionText")!.Text =
            $"Versión {managerVersion}";

        this.FindControl<TextBlock>("LibraryVersionText")!.Text =
            $"SecureCredentials {libraryVersion}";

        this.FindControl<TextBlock>("DescriptionText")!.Text =
            description;

        this.FindControl<TextBlock>("CompanyText")!.Text =
            company;

        this.FindControl<TextBlock>("CopyrightText")!.Text =
            copyright;
    }

    /// <summary>
    /// Obtiene una versión de tres componentes, priorizando FileVersion.
    /// </summary>
    /// <param name="GetProductVersion">
    /// Ensamblado del que se obtiene la versión.
    /// </param>
    /// <returns>
    /// Versión del producto o «Desconocida» si no está disponible.
    /// </returns>
    private static string GetProductVersion(Assembly assembly)
    {
        // Preferimos FileVersion para mostrar 1.0.0,
        // sin posibles sufijos de commit Git.
        string? fileVersion =
            assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()
                ?.Version;

        if (!string.IsNullOrWhiteSpace(fileVersion))
        {
            Version version = Version.Parse(fileVersion);

            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        // Alternativa si no existe FileVersion.
        Version? assemblyVersion = assembly.GetName().Version;

        return assemblyVersion is null
            ? "Desconocida"
            : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }

    /// <summary>
    /// Cierra la ventana informativa.
    /// </summary>
    /// <param name="sender">
    /// Control que originó el evento.
    /// </param>
    /// <param name="e">
    /// Argumentos del evento de clic.
    /// </param>
    private void Close_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close();
    }
}
