# Validación Windows x64 — SecureCredentials.Manager 1.0.1

**Fecha de referencia:** 29 de octubre de 2026 (ampliación de pruebas del 28/10)  
**Biblioteca:** SecureCredentials 1.0.0  
**Sistema probado:** Windows x64, compilación de SO 26200  
**SDK:** .NET 10.0.403; **runtime:** 10.0.14  
**Formato:** `.enc` v2, sin cambios.

## Alcance y evidencias

La solución se copió desde Linux mediante ZIP y se probó en `C:\Proyectos\SecureCredentials`, sin utilizar GitHub. Se confirmó compilación Release y ejecución de **106 pruebas automatizadas correctas** después de adaptar una prueba que llamaba a `libc`/`geteuid()` exclusivamente en Linux. La ejecución inicial había obtenido 105 correctas y una fallida por `DllNotFoundException` de `libc` en Windows. La corrección es de portabilidad del test, no del algoritmo criptográfico. Conviene comprobar en el registro final si el caso Linux se marca como omitido o se contabiliza como correcto sin ejecutar la ruta POSIX.

Se comunicaron como satisfactorias las comprobaciones funcionales del gestor Avalonia en Windows: arranque, edición Unicode, zoom, cifrado, guardado, reapertura y descifrado. Se generó un ZIP Windows x64 de aproximadamente **70,9 MB** y un manifiesto `SHA256SUMS-win-x64.txt`. Estas evidencias proceden de las pruebas realizadas; este documento no incorpora logs ni el hash literal.

## Matriz de validación

| Comprobación | Estado |
| --- | --- |
| SDK .NET 10 y entorno Windows x64 | Confirmado |
| Compilación Release | Confirmado |
| Suite xUnit tras ajuste de portabilidad | 106 correctas, según resultado comunicado |
| Arranque de Avalonia desde el proyecto | Confirmado |
| Entrada Unicode y controles de zoom | Confirmado |
| Cifrado, guardado, reapertura y descifrado | Confirmado |
| Generación ZIP `win-x64` y manifiesto SHA-256 | Confirmado |
| Comparación explícita del hash ZIP con el manifiesto | Pendiente de evidencia |
| Ejecución del paquete Windows en equipo distinto del de desarrollo | Confirmada por prueba manual del 29/10 |
| Ejecución sin SDK y sin runtime .NET 10 instalados | Confirmada: equipo Windows x64 con únicamente runtimes .NET 6.0.7 |
| Interoperabilidad Windows → Linux de un `.enc` | Confirmada: cifrado en Windows y apertura/descifrado en Linux |
| Interoperabilidad Linux → Windows de un `.enc` | Pendiente de prueba documentada |
| Atajos `Ctrl + +` y `Ctrl + -` en Windows | Confirmados; también aparecen las etiquetas emergentes de A+ y A− |
| Archivo `manager.log` en Windows | Confirmado en `%USERPROFILE%\.local\state\securecredentials-manager\manager.log` |
| Endurecimiento ACL, propiedad y reparse points | Pendiente |

## Reproducir las pruebas

```powershell
dotnet --info
dotnet test .\SecureCredentials.slnx -c Release
.\fabricar_entregables_windows.ps1 -ManagerVersion 1.0.1 -RuntimeIdentifier win-x64
Get-FileHash .\dist\paquetes\SecureCredentials.Manager-1.0.1-win-x64.zip -Algorithm SHA256
Get-Content .\dist\paquetes\SHA256SUMS-win-x64.txt
```

Para comprobar la distribución, extraer el ZIP en otra carpeta y ejecutar `SecureCredentials.Manager.exe`. Probar con datos ficticios: crear, cifrar, cerrar, reabrir y recuperar exactamente el mismo texto. La ejecución sin .NET 10 ya se ha confirmado en otro equipo Windows.

## Seguridad y dictamen

**Validación funcional satisfactoria en los equipos Windows x64 probados**, incluida la ejecución del paquete autocontenido sin SDK ni runtime .NET 10 instalados. El equipo de la prueba adicional sí disponía de runtimes .NET 6.0.7; por tanto, no se ha demostrado aún la ejecución en un sistema sin ningún runtime .NET. Las garantías POSIX de Linux **no** deben atribuirse a Windows: la ruta de almacenamiento Windows necesita validación de ACL y otras defensas específicas. La clave embebida no protege contra ingeniería inversa.

## Ampliación de validación — 29/10/2026

En un equipo Windows x64 (versión del sistema `10.0.26300.9711`) se comprobó mediante `dotnet --info` que no había SDK y que solo estaban instalados `Microsoft.NETCore.App 6.0.7` y `Microsoft.WindowsDesktop.App 6.0.7`. El gestor publicado se ejecutó correctamente, cifró y descifró un archivo `.enc`. El archivo generado se trasladó a Linux y se abrió y descifró correctamente con `securecredentials-manager.sh` desde `/tmp/securecredentials-1.0.1`.

En Windows también se verificaron `Ctrl + +` y `Ctrl + -` y las ayudas emergentes de los botones A+ y A−. El registro se localizó en `C:\Users\jrios\.local\state\securecredentials-manager\manager.log` (ruta generalizable: `%USERPROFILE%\.local\state\securecredentials-manager\manager.log`).

Las pruebas descritas son manuales y comunicadas por el usuario; no se adjuntan capturas, registros de ejecución ni hashes. `Ctrl + 0` figura en el historial del proyecto, pero no se ha comunicado una prueba específica en Windows de ese atajo.
