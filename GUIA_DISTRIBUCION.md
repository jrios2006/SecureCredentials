# Guía de distribución — SecureCredentials

**Versiones:** biblioteca 1.0.0 · Manager 1.0.1 · formato `.enc` v2.

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

Los paquetes del Manager se publican con `--self-contained true`, por lo que no requieren instalar previamente el runtime .NET como dependencia separada en condiciones normales. La ejecución en **Windows sin .NET instalado sigue sin estar comprobada**. Linux también requiere las bibliotecas nativas y el entorno gráfico compatibles con Avalonia.

## Criterios antes de distribuir

1. Compilación y pruebas completas sin errores.
2. Integridad SHA-256 verificada.
3. Prueba del paquete extraído en otra carpeta.
4. Edición, cifrado, guardado, reapertura y descifrado con datos ficticios.
5. Ausencia de claves reales, archivos `.enc` confidenciales y registros sensibles.
6. Documentación de límites de seguridad por plataforma.

La biblioteca NuGet se versiona independientemente del Manager. El formato `.enc` v2 no cambia con esta distribución. No confundir distribución autocontenida con instalador firmado o con garantías equivalentes de almacenamiento seguro en Windows y Linux.
