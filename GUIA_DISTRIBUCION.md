# Guía de distribución — SecureCredentials

**Versiones:** biblioteca 1.0.0 · Manager 1.0.1 · formato `.enc` v2.

## Descargas publicadas

- [Biblioteca SecureCredentials 1.0.0 (.nupkg)](https://github.com/jrios2006/SecureCredentials/releases/download/v1.0.0/SecureCredentials.1.0.0.nupkg) — paquete para integrar en proyectos .NET 10, publicado como adjunto a la Release y no necesariamente en NuGet.org.
- [Manager 1.0.1 para Linux x64](https://github.com/jrios2006/SecureCredentials/releases/download/v1.0.1/SecureCredentials.Manager-1.0.1-linux-x64.tar.gz) y [SHA-256 Linux](https://github.com/jrios2006/SecureCredentials/releases/download/v1.0.1/SHA256SUMS-linux-x64.txt).
- [Manager 1.0.1 para Windows x64](https://github.com/jrios2006/SecureCredentials/releases/download/v1.0.1/SecureCredentials.Manager-1.0.1-win-x64.zip) y [SHA-256 Windows](https://github.com/jrios2006/SecureCredentials/releases/download/v1.0.1/SHA256SUMS-win-x64.txt).

Los archivos compilados se descargan desde GitHub Releases; no es necesario clonar el repositorio para ejecutar el Manager.

### Instalar y ejecutar en Linux x64

Descarga el TAR.GZ y el archivo SHA-256 Linux en el mismo directorio. Comprueba la integridad y extrae el paquete:

```bash
sha256sum -c SHA256SUMS-linux-x64.txt
mkdir -p "$HOME/Aplicaciones/SecureCredentials.Manager"
tar -xzf SecureCredentials.Manager-1.0.1-linux-x64.tar.gz -C "$HOME/Aplicaciones/SecureCredentials.Manager"
```

En el directorio extraído, utiliza el lanzador `securecredentials-manager.sh` siguiendo las instrucciones incluidas en el paquete, o el ejecutable `SecureCredentials.Manager`. El lanzador proporciona una alternativa para el problema de acentos observado en Cinnamon/X11 con IBus. El entorno necesita las bibliotecas nativas y el sistema gráfico compatibles con Avalonia.

### Instalar y ejecutar en Windows x64

Descarga el ZIP y `SHA256SUMS-win-x64.txt` en la misma carpeta. En PowerShell:

```powershell
$zip = '.\SecureCredentials.Manager-1.0.1-win-x64.zip'
$linea = Get-Content '.\SHA256SUMS-win-x64.txt' -TotalCount 1
$hashEsperado = ($linea -split '\s+')[0].ToLowerInvariant()
$hashReal = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hashReal -ne $hashEsperado) { throw 'SHA-256 NO COINCIDE' }
Write-Host 'SHA-256 CORRECTO'
Expand-Archive -LiteralPath $zip -DestinationPath '.\SecureCredentials.Manager-1.0.1' 
```

Ejecuta `SecureCredentials.Manager.exe` **desde la carpeta extraída**, conservando sus DLL y otros archivos auxiliares. Se ha validado el arranque del ZIP extraído en Windows x64 y la batería de 106 pruebas. El paquete es autocontenido y no exige instalar el SDK .NET; la documentación del proyecto también registra una prueba en otro Windows sin runtime .NET 10, aunque con runtimes .NET 6.0.7 presentes. No equivale a una prueba en un Windows totalmente limpio.

## Fabricación Linux

Requisitos: SDK .NET 10, Bash, tar, sha256sum y los archivos completos del proyecto.

```bash
./fabricar_entregables.sh linux-x64
```

El script compila, ejecuta pruebas, empaqueta NuGet y publica el Manager autocontenido. Entrega `dist/paquetes/SecureCredentials.Manager-1.0.1-linux-x64.tar.gz`, `dist/nuget/SecureCredentials.1.0.0.nupkg` y `dist/paquetes/SHA256SUMS-linux-x64.txt`. El tarball incluye el ejecutable y el lanzador `securecredentials-manager.sh` para la incidencia de acentos en Cinnamon/X11 con IBus.

```bash
cd dist/paquetes
sha256sum -c SHA256SUMS-linux-x64.txt
```

## Fabricación Windows

Requisitos: SDK .NET 10, PowerShell 5.1 o posterior y proyecto completo. Desde la raíz:

```powershell
.\fabricar_entregables_windows.ps1 -ManagerVersion 1.0.1 -RuntimeIdentifier win-x64
```

El script restaura, compila, prueba, publica autocontenido sin trimming, comprime **todo** el directorio publicado y genera:

- `dist/paquetes/SecureCredentials.Manager-1.0.1-win-x64.zip`
- `dist/paquetes/SHA256SUMS-win-x64.txt`

Verificación manual:

```powershell
$zip = '.\dist\paquetes\SecureCredentials.Manager-1.0.1-win-x64.zip'
(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Get-Content '.\dist\paquetes\SHA256SUMS-win-x64.txt'
```

Los valores deben coincidir. Extraer el ZIP completo a una carpeta de prueba y ejecutar `SecureCredentials.Manager.exe`. No distribuir solo el EXE sin verificar que la publicación funciona sin archivos auxiliares.

## Requisitos en destino

Los paquetes del Manager se publican con `--self-contained true`, por lo que no requieren instalar previamente el runtime .NET como dependencia separada en condiciones normales. La ejecución en un Windows completamente limpio, sin ningún runtime .NET instalado, sigue sin estar comprobada; se documentó una prueba en otro Windows sin .NET 10, con runtimes .NET 6.0.7 presentes. Linux también requiere las bibliotecas nativas y el entorno gráfico compatibles con Avalonia.

## Criterios antes de distribuir

1. Compilación y pruebas completas sin errores.
2. Integridad SHA-256 verificada.
3. Prueba del paquete extraído en otra carpeta.
4. Edición, cifrado, guardado, reapertura y descifrado con datos ficticios.
5. Ausencia de claves reales, archivos `.enc` confidenciales y registros sensibles.
6. Documentación de límites de seguridad por plataforma.

La biblioteca NuGet se versiona independientemente del Manager. El formato `.enc` v2 no cambia con esta distribución. No confundir distribución autocontenida con instalador firmado o con garantías equivalentes de almacenamiento seguro en Windows y Linux.
