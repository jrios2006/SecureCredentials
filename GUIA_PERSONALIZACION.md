# Guía de personalización de SecureCredentials

**Proyecto:** SecureCredentials · **Biblioteca base:** 1.0.0 · **Manager base:** 1.0.1  
**Destinatarios:** desarrolladores que desean obtener su propia biblioteca NuGet y su propia aplicación gráfica para Linux o Windows.  
**Alcance:** personalización del código fuente y de la clave AES integrada; no es un sistema de cuentas ni de contraseñas de usuario.

## 1. Objetivo

Esta guía explica cómo descargar el proyecto, sustituir la clave de demostración por una clave AES-256 propia, comprobar el cifrado y descifrado, y fabricar entregables personalizados. La biblioteca y el Manager deben utilizar **la misma clave** para intercambiar documentos cifrados.

**Advertencia de seguridad:** una clave incrustada en el código o en un ejecutable puede recuperarse mediante ingeniería inversa. Esta modalidad es útil para demostraciones y escenarios controlados, **no para proteger secretos de alto valor frente a quien pueda obtener el binario**. Para producción se recomienda diseñar un proveedor de claves respaldado por un almacén seguro del sistema o por una infraestructura de gestión de claves.

## 2. Obtener el proyecto

Se requiere Git y el SDK de .NET 10. Para construir la interfaz gráfica se necesitan las dependencias de Avalonia y las bibliotecas nativas correspondientes al sistema de destino.

```bash
git clone https://github.com/jrios2006/SecureCredentials.git
cd SecureCredentials
dotnet restore SecureCredentials.slnx
```

El código de origen es la base para fabricar una variante propia; **no hace falta modificar ni reutilizar los archivos de la Release original**.

## 3. Preparar una copia privada de trabajo

Antes de personalizar la clave, crea una copia privada del repositorio o una rama de trabajo que no publiques. No envíes claves reales a GitHub, ni siquiera temporalmente: eliminarlas de un commit posterior no las elimina necesariamente del historial.

```bash
git switch -c personalizacion-local
```

Comprueba también los archivos de configuración, ejemplos, registros y paquetes antes de distribuirlos. Nunca incluyas archivos `.enc` confidenciales ni contraseñas reales en el código o en los tests.

## 4. Cambiar la clave AES integrada

El proveedor previsto para esta modalidad es `SecureCredentials/EmbeddedKeyProvider.cs`. En una versión del código revisada, contiene:

```csharp
public const string DefaultKeyId = "embedded-v1";
private const string KeyBase64 = "REEMPLAZAR_CON_CLAVE_AES_256_EN_BASE64";
```

En tu clon, **verifica el contenido real del archivo** antes de editarlo. `KeyBase64` debe contener una clave aleatoria de **32 bytes**, codificada en Base64. No introduzcas directamente una frase o contraseña en ese campo: una contraseña de texto no equivale a una clave AES-256 generada aleatoriamente.

### 4.1. Generar una clave en Linux

Ejecuta en un terminal privado:

```bash
openssl rand -base64 32
```

El resultado será una cadena Base64 que representa 32 bytes aleatorios. Trátala como un secreto. No la copies en incidencias, documentación pública, capturas ni conversaciones compartidas.

### 4.2. Incorporar la clave en tu copia privada

Sustituye únicamente el valor de `KeyBase64` por el generado. Si deseas distinguir los documentos de tu variante, puedes cambiar también `DefaultKeyId` por un identificador propio, por ejemplo `mi-organizacion-v1`. **`KeyId` no es una contraseña ni una clave secreta:** es un identificador que debe coincidir con el que espera el proveedor al descifrar.

No cambies el algoritmo, los tamaños de `Nonce` y `Tag`, ni la construcción de AAD del formato `.enc` v2. Consulta [FORMATO_ENC.md](FORMATO_ENC.md).

**Importante:** si utilizas una clave diferente, no podrás abrir documentos cifrados con la clave anterior a menos que conserves un proveedor capaz de recuperarla o realices una migración controlada. Guarda una copia segura de tu clave: perderla puede hacer irrecuperables tus documentos.

### 4.3. Alternativa más segura

Si necesitas distribuir el ejecutable a personas que no deberían conocer la clave, **no incrustes esa clave en el binario**. Implementa o configura un `IKeyProvider` adecuado, con almacenamiento externo y control de acceso. La sustitución por una contraseña de usuario requeriría un diseño adicional de derivación de claves (por ejemplo, Argon2id o PBKDF2 con sal y parámetros apropiados); **no está implementada automáticamente por el cambio de `KeyBase64`**.

## 5. Comprobar la personalización

Desde la raíz del repositorio:

```bash
dotnet restore SecureCredentials.slnx
dotnet build SecureCredentials.slnx -c Release
dotnet test SecureCredentials.slnx -c Release --no-build
```

La versión de referencia superó 106 pruebas, pero tu variante debe pasar las pruebas por sí misma. Comprueba además manualmente el flujo completo con **datos ficticios**:

1. Crear un documento `.enc` nuevo con la biblioteca o el Manager personalizado.
2. Cerrar y volver a abrir el Manager.
3. Abrir y descifrar el documento; comprobar que recupera exactamente el texto original.
4. Modificarlo, guardar y volver a abrirlo.
5. Verificar que la biblioteca personalizada puede descifrar un archivo creado por el Manager y viceversa.
6. Confirmar que un archivo cifrado con otra clave se rechaza, sin sobrescribirlo ni destruirlo.

Ten presente que algunas pruebas existentes pueden depender de valores o archivos de prueba concretos: revisa cualquier fallo antes de atribuirlo a la personalización.

## 6. Fabricar los entregables en Linux x64

Con la clave ya configurada en tu copia privada:

```bash
./fabricar_entregables.sh linux-x64
```

El script de distribución revisado compila, ejecuta pruebas, empaqueta la biblioteca y publica el Manager. Los archivos previstos son:

```text
dist/nuget/SecureCredentials.1.0.0.nupkg
dist/paquetes/SecureCredentials.Manager-1.0.1-linux-x64.tar.gz
dist/paquetes/SHA256SUMS-linux-x64.txt
```

Verifica los hashes desde el directorio de paquetes, teniendo en cuenta que el manifiesto puede incluir rutas relativas:

```bash
cd dist/paquetes
sha256sum -c SHA256SUMS-linux-x64.txt
```

Si la verificación falla por una ruta relativa al paquete NuGet, ejecuta el comando desde el directorio esperado por el manifiesto o calcula y compara el SHA-256 de cada archivo. Extrae el `.tar.gz` completo en una carpeta nueva y prueba el ejecutable o el lanzador `securecredentials-manager.sh`.

## 7. Fabricar los entregables en Windows x64

En PowerShell, desde la raíz de la copia personalizada:

```powershell
dotnet restore .\SecureCredentials.slnx
dotnet build .\SecureCredentials.slnx -c Release
dotnet test .\SecureCredentials.slnx -c Release --no-build
.\fabricar_entregables_windows.ps1 -ManagerVersion 1.0.1 -RuntimeIdentifier win-x64
```

Los entregables previstos son:

```text
dist\paquetes\SecureCredentials.Manager-1.0.1-win-x64.zip
dist\paquetes\SHA256SUMS-win-x64.txt
```

Comprueba el hash:

```powershell
$zip = '.\dist\paquetes\SecureCredentials.Manager-1.0.1-win-x64.zip'
$manifest = '.\dist\paquetes\SHA256SUMS-win-x64.txt'
$actual = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$expected = ((Get-Content $manifest -TotalCount 1) -split '\s+')[0].ToLowerInvariant()
if ($actual -ne $expected) { throw 'SHA-256 incorrecto' }
'SHA-256 correcto'
```

Extrae **todo** el ZIP antes de ejecutar `SecureCredentials.Manager.exe`. La publicación es autocontenida, pero ello no equivale a un instalador firmado ni acredita garantías de almacenamiento seguro equivalentes a Linux.

## 8. Utilizar el paquete NuGet personalizado

El `.nupkg` generado es un paquete local. **No queda publicado automáticamente en NuGet.org**. Puedes incorporarlo a una aplicación .NET 10 independiente desde una carpeta de paquetes local:

```bash
mkdir -p /ruta/mi-feed-local
cp dist/nuget/SecureCredentials.1.0.0.nupkg /ruta/mi-feed-local/
dotnet add /ruta/a/MiAplicacion.csproj package SecureCredentials --version 1.0.0 --source /ruta/mi-feed-local
```

Adapta las rutas al sistema. Si existe otra versión del paquete con el mismo identificador y número, evita confusiones utilizando una versión propia en el `.csproj` antes de fabricar los entregables. Comprueba también la API de la aplicación consumidora y que el proveedor de claves utilizado sea el esperado.

## 9. Versionado y distribución responsable

Si vas a distribuir tu variante a terceros, es recomendable asignarle una **versión distinta** y un nombre o identificador de paquete que evite confusiones con la distribución original. Revisa los archivos `.csproj`, las referencias entre proyectos, la documentación, las notas de cambios y la licencia del repositorio antes de redistribuir.

No publiques el paquete o el Manager personalizado si contienen una clave que deba permanecer secreta. Publicar el código fuente no implica que sea seguro distribuir una clave integrada en los binarios.

## 10. Lista final de comprobación

- [ ] El proyecto se ha descargado y restaurado correctamente.
- [ ] La clave es aleatoria, tiene 32 bytes y se ha almacenado de forma controlada.
- [ ] La clave no se ha incorporado a commits públicos ni a documentación.
- [ ] Biblioteca y Manager utilizan el proveedor y el `KeyId` esperados.
- [ ] Compilación y pruebas completadas satisfactoriamente.
- [ ] Cifrado, guardado, reapertura y descifrado verificados con datos ficticios.
- [ ] Paquetes NuGet y Manager generados y hashes comprobados.
- [ ] Distribución Linux/Windows probada tras extraer los archivos.
- [ ] Versiones, licencia y advertencias de seguridad revisadas.

## 11. Documentación relacionada

- [README.md](README.md): presentación y uso de la solución.
- [GUIA_DISTRIBUCION.md](GUIA_DISTRIBUCION.md): fabricación, instalación y verificación de paquetes.
- [GUIA_FABRICACION_ENTREGABLES.md](GUIA_FABRICACION_ENTREGABLES.md): procedimientos de fabricación.
- [FORMATO_ENC.md](FORMATO_ENC.md): contrato del formato cifrado.
- [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md): arquitectura y proveedores de claves.

---

**Resumen:** personalizar SecureCredentials consiste en fabricar una **variante propia** del proyecto con una clave y una distribución coherentes. No convierte por sí solo la clave integrada en una contraseña segura ni sustituye la gestión de claves requerida en producción.
