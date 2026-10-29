# Historial de cambios

Todos los cambios relevantes de **SecureCredentials** se documentan en este archivo.

El proyecto utiliza versiones independientes para la biblioteca `SecureCredentials` y la aplicación gráfica `SecureCredentials.Manager`. El número de versión del formato `.enc` es independiente de ambos.

Este historial sigue las convenciones de [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y utiliza categorías como **Añadido**, **Cambiado** y **Corregido**.

## SecureCredentials.Manager

### [1.0.1] - 2026-10-28

#### Añadido
- Controles `A+` y `A−` para aumentar o reducir el tamaño de letra del editor.
- Atajos de teclado para ampliar (`Ctrl + +`), reducir (`Ctrl + -`) y restablecer (`Ctrl + 0`) el tamaño de letra.
- Lanzador alternativo `securecredentials-manager.sh` en la distribución Linux para facilitar la entrada de caracteres acentuados en el entorno Cinnamon/X11 probado.

#### Cambiado
- Tamaño de letra inicial del editor: de 14 a 16.
- Rango de tamaños de letra del editor: de 10 a 32.
- Distribución Linux x64 publicada con el lanzador alternativo, sin alterar la ejecución directa de `SecureCredentials.Manager`.

#### Validación de plataformas
- Se confirmó la compilación y validación funcional de Manager 1.0.1 en Windows x64 con .NET 10; se generó una distribución ZIP autocontenida y manifiesto SHA-256.
- El 29/10/2026 se verificó la ejecución del paquete en otro Windows x64 sin SDK ni runtime .NET 10 (con runtimes .NET 6.0.7), el cifrado/descifrado y la interoperabilidad Windows → Linux. El endurecimiento del almacenamiento específico de Windows sigue pendiente.

- Se comprobó la creación de `manager.log` en Windows bajo `%USERPROFILE%\.local\state\securecredentials-manager\`.

#### Distribución publicada — 2026-10-29
- Publicados los paquetes del Manager `1.0.1` para **Linux x64** (`.tar.gz`) y **Windows x64** (`.zip`) en la misma [Release v1.0.1](https://github.com/jrios2006/SecureCredentials/releases/tag/v1.0.1), cada uno con su archivo de sumas SHA-256.
- La distribución Windows se compiló en modo Release con SDK .NET 10.0.403; **106 pruebas superadas, 0 fallidas y 0 omitidas**.
- Se comprobó la extracción y el arranque del ZIP de Windows fuera del directorio de compilación; su SHA-256 coincide con el digest publicado en GitHub.
- Se actualizaron las instrucciones de descarga, instalación y verificación de los entregables para ambas plataformas.
- No se modifica la versión de la biblioteca (`1.0.0`) ni el formato cifrado (`.enc` v2).

#### Notas de compatibilidad
- El lanzador aplica `XMODIFIERS=@im=none`, `GTK_IM_MODULE=xim` y `QT_IM_MODULE=xim` únicamente al proceso de la aplicación. No modifica la configuración global del sistema.
- La solución de entrada de teclado se comprobó en Cinnamon/X11 con configuración regional española; no se considera una corrección universal para todos los métodos de entrada de Linux.
- Los botones A+ y A−, sus etiquetas emergentes y los atajos `Ctrl + +`, `Ctrl + -` y `Ctrl + 0` se comprobaron en Windows y Linux.
- No se modifica el cifrado ni el formato `.enc` v2.

### [1.0.0] - 2026-10-28

#### Añadido
- Primera distribución versionada del gestor gráfico de documentos cifrados, desarrollado con Avalonia y .NET 10.
- Editor de texto con apertura, edición, validación de JSON y guardado de documentos `.enc`.
- Distribución autónoma para Linux x64 mediante archivo `.tar.gz`.
- Ventana «Acerca de» con información de versión.

## SecureCredentials (biblioteca)

### [1.0.0] - 2026-10-28

#### Añadido
- Primera distribución versionada como paquete NuGet para .NET 10.
- Cifrado y descifrado de texto UTF-8 mediante AES-256-GCM.
- Lectura del formato `.enc` v2 y compatibilidad de lectura con documentos v1.
- Proveedores de claves y mecanismos de almacenamiento seguro contemplados en el proyecto.
- Documentación técnica y de interoperabilidad.

#### Verificación
- Compilación Release satisfactoria y **106 de 106 pruebas automatizadas superadas** durante la fabricación de los entregables del 28 de octubre de 2026.
- Generación de `SecureCredentials.1.0.0.nupkg` y `SecureCredentials.Manager-1.0.1-linux-x64.tar.gz` completada correctamente.

---

**Nota sobre versiones:** la actualización del gestor a `1.0.1` no cambia la versión de la biblioteca NuGet (`1.0.0`) ni la versión del formato de archivo cifrado (`v2`).
