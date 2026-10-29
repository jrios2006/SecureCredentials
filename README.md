# SecureCredentials

**SecureCredentials** es una biblioteca desarrollada en **C# y .NET 10** que permite cifrar, almacenar y recuperar información sensible mediante **AES-256-GCM**.

Está orientada a aplicaciones que necesitan guardar información confidencial en archivos locales, evitando que su contenido pueda leerse directamente. La solución incluye una biblioteca reutilizable, una aplicación de demostración, pruebas automatizadas y una aplicación gráfica de administración basada en Avalonia.

## Características principales

- Cifrado autenticado mediante **AES-256-GCM**.
- Cifrado y descifrado de texto UTF-8.
- Almacenamiento de información cifrada en documentos JSON (habitualmente con extensión `.enc`).
- Comprobación de integridad mediante etiquetas de autenticación.
- Formato cifrado **v2**, con autenticación de metadatos mediante AAD.
- Gestión de claves mediante proveedores intercambiables.
- Escritura y lectura segura de archivos en Linux.
- Validación de propietarios, permisos y enlaces simbólicos.
- Escritura mediante archivos temporales y sustitución del destino.
- API asíncrona para facilitar la integración con otras aplicaciones.
- Interoperabilidad verificada entre **C# y Python** para el formato v2.
- Gestor gráfico **SecureCredentials.Manager** para abrir, editar y guardar documentos cifrados.

La biblioteca no interpreta el contenido que cifra. Puede utilizarse para proteger texto plano, documentos JSON, XML u otros contenidos representables como texto UTF-8.

## Requisitos

- **.NET 10 SDK** para compilar y ejecutar los proyectos desde código fuente.
- **Linux** para las garantías de almacenamiento POSIX descritas en esta documentación; **Windows x64** para la funcionalidad probada, sin garantías equivalentes de seguridad de archivos.
- Un entorno gráfico compatible para ejecutar `SecureCredentials.Manager` (Avalonia).
- **Python 3** y el paquete `cryptography` solamente si se desea utilizar el script independiente de interoperabilidad Python.

La aplicación y la biblioteca han superado pruebas funcionales en **Windows x64** con .NET 10. La distribución del gestor para Windows se genera como ZIP autocontenido. **Esto no acredita equivalencia de seguridad de almacenamiento con Linux**: siguen pendientes las comprobaciones específicas de ACL, enlaces/reparse points y endurecimiento de operaciones de archivo. Se ha probado el paquete en otro Windows x64 sin SDK y sin runtime .NET 10 (solo con runtimes .NET 6.0.7).

## Componentes de la solución

| Componente | Responsabilidad |
|---|---|
| `SecureCredentials` | Biblioteca principal: cifrado, descifrado, proveedores de claves y almacenamiento seguro. |
| `SecureCredentials.Demo` | Aplicación de demostración de la biblioteca. |
| `SecureCredentials.Tests` | Pruebas automatizadas funcionales, criptográficas y de seguridad del almacenamiento. |
| `SecureCredentials.Manager` | Aplicación gráfica Avalonia para trabajar con documentos cifrados. |

## Uso básico de la biblioteca

La biblioteca proporciona dos clases que simplifican las operaciones habituales:

- `SecureCredentialsWriter`: cifra y guarda información.
- `SecureCredentialsReader`: abre y descifra información almacenada.

### Cifrar y guardar información

```csharp
using SecureCredentials;

string filePath = "/home/usuario/.config/miapp/credentials.enc";

string contenido = """
{
    "servidor": "localhost",
    "usuario": "admin",
    "password": "ejemplo"
}
""";

await SecureCredentialsWriter.WriteAsync(
    filePath,
    contenido);
```

El archivo resultante contiene información cifrada, no el JSON original. El nombre `.enc` es una convención: el formato real lo determina el contenido del archivo.

### Leer y descifrar información

```csharp
using SecureCredentials;

string filePath = "/home/usuario/.config/miapp/credentials.enc";

string contenido = await SecureCredentialsReader.ReadAsync(
    filePath);

Console.WriteLine(contenido);
```

El resultado es el mismo texto UTF-8 que se proporcionó originalmente, siempre que se utilice la clave correcta y el archivo no haya sido alterado.

**Precaución:** `Console.WriteLine(contenido)` es un ejemplo didáctico. En aplicaciones reales no deben imprimirse contraseñas ni otros secretos en consola o registros.

## Formato de almacenamiento

La información se almacena en un documento JSON que contiene los datos necesarios para realizar el descifrado:

```json
{
    "Version": 2,
    "Algorithm": "AES-256-GCM",
    "KeyId": "embedded-v1",
    "Nonce": "...",
    "Ciphertext": "...",
    "Tag": "..."
}
```

Este ejemplo es ilustrativo y **no es un archivo cifrado válido**: los valores Base64 están abreviados.

Los campos `Nonce`, `Ciphertext` y `Tag` contienen valores codificados en Base64. El formato actual utiliza la **versión 2**, que incorpora autenticación de metadatos mediante AAD (*Additional Authenticated Data*). `KeyId` identifica la clave utilizada, pero **no contiene la clave**.

La especificación completa, incluida la construcción exacta del AAD, los tamaños de los campos y la interoperabilidad con Python, está en **[FORMATO_ENC.md](FORMATO_ENC.md)**.

## Gestión de claves

SecureCredentials dispone de varios proveedores:

| Proveedor | Finalidad |
|---|---|
| `EmbeddedKeyProvider` | Clave integrada en la biblioteca. |
| `DevelopmentKeyProvider` | Clave temporal para desarrollo y pruebas. |
| `LinuxSecretServiceKeyProvider` | Recuperación de claves desde el servicio de secretos de Linux. |

Las operaciones de alto nivel utilizan actualmente `EmbeddedKeyProvider`.

**Importante:** una clave integrada en el código no puede considerarse secreta frente a alguien que tenga acceso al binario y pueda analizarlo. Este mecanismo protege frente a la lectura casual de archivos, pero no frente a ingeniería inversa. No debe utilizarse como único mecanismo de protección de secretos de alto valor frente a un atacante con acceso al ejecutable.

No publiques la clave AES integrada, su representación Base64 ni volcados de memoria que puedan contenerla.

## Seguridad de archivos en Linux

La implementación incorpora medidas de protección para el almacenamiento local:

- **Directorios privados de claves:** permisos estrictos `700`, gestionados por `CredentialDirectory.CreatePrivate()`.
- **Directorios de documentos `.enc`:** se permiten permisos como `700`, `750` o `755`, siempre que cumplan las comprobaciones de propiedad y no permitan escritura a grupo u otros usuarios. No se modifica automáticamente el modo de directorios existentes para forzarlos a `700`.
- **Archivos cifrados:** permisos `600`; los archivos existentes con permisos más amplios, como `644`, se rechazan en las operaciones protegidas.
- Comprobación del propietario mediante UID efectivo.
- Rechazo de enlaces simbólicos en las operaciones protegidas.
- Operaciones basadas en descriptores POSIX y comprobaciones de componentes de ruta.
- Escritura mediante archivos temporales y sustitución del archivo de destino.
- Sincronización de archivos y directorios mediante `fsync`.
- Límite de tamaño de archivo cifrado de **1 MiB**.

La separación entre **directorios privados** y **directorios de documentos cifrados** permite almacenar archivos `.enc` en rutas habituales del usuario (por ejemplo, dentro de `~/Proyectos`) sin exigir que toda la carpeta tenga permisos `700`.

Estas medidas están orientadas a reducir los riesgos de acceso no autorizado y manipulación de rutas. No sustituyen la protección del sistema operativo ni garantizan seguridad frente a un atacante con control completo de la cuenta o del proceso.

## Aplicación gráfica: SecureCredentials.Manager

`SecureCredentials.Manager` es una aplicación de escritorio desarrollada con **Avalonia**. Permite abrir documentos `.enc`, editar su contenido descifrado y guardar el resultado cifrado desde una interfaz gráfica.

Para iniciarla desde la raíz del repositorio:

```bash
dotnet run --project SecureCredentials.Manager
```

El gestor conserva información de uso local:

| Ruta | Finalidad |
|---|---|
| `~/.config/securecredentials-manager/settings.json` | Preferencias del gestor, incluida la última carpeta utilizada. |
| `~/.local/state/securecredentials-manager/manager.log` | Registro de actividad de la aplicación. |

En Windows x64 se ha localizado el registro en `%USERPROFILE%\.local\state\securecredentials-manager\manager.log`. Los botones A+ y A− muestran ayudas emergentes y funcionan con los atajos `Ctrl + +` y `Ctrl + -`, comprobados tanto en Linux como en Windows.

Estos archivos de configuración y registro **no son el almacén de claves AES**. No deben utilizarse para guardar secretos en texto claro.

También se ha comprobado que un `.enc` creado y cifrado en Windows se abre y descifra en Linux con el gestor 1.0.1.

Se ha comprobado que el gestor puede abrir archivos `.enc` creados en Python y que los archivos guardados desde el gestor pueden descifrarse en Python, utilizando la misma clave y el formato v2.

## Interoperabilidad con Python

El formato v2 se ha probado en ambas direcciones:

1. **Python → C#:** cifrar en Python, abrir y descifrar con `SecureCredentials.Manager`.
2. **C# → Python:** guardar desde el gestor, descifrar con un script Python compatible.

Para ejecutar el script de interoperabilidad utilizado durante las pruebas se requiere Python 3 y la dependencia `cryptography`:

```bash
python -m pip install cryptography
```

El script de prueba no es necesario para compilar ni ejecutar la solución .NET. Para interoperar, ambas implementaciones deben usar la **misma clave AES**, el mismo `KeyId` y el AAD definido en [FORMATO_ENC.md](FORMATO_ENC.md). No se debe incorporar una clave real al repositorio ni distribuirla dentro de un script de ejemplo.

Para los comandos, requisitos, tratamiento de errores y advertencias de seguridad del script, consultar **[GUIA_INTEROPERABILIDAD_PYTHON.md](GUIA_INTEROPERABILIDAD_PYTHON.md)**. La utilidad se incluye como herramienta de laboratorio y no debe distribuirse con una clave AES real integrada.

## Estructura del proyecto

```text
SecureCredentials/
├── SecureCredentials.slnx
├── README.md
├── FORMATO_ENC.md
├── GUIA_ADMINISTRADOR.md
├── GUIA_INTEROPERABILIDAD_PYTHON.md
├── securecredentials_interop.py
├── DEVELOPER_GUIDE.md
│
├── SecureCredentials/
│   └── Biblioteca principal
│
├── SecureCredentials.Demo/
│   └── Aplicación de demostración
│
├── SecureCredentials.Manager/
│   └── Aplicación gráfica Avalonia
│
└── SecureCredentials.Tests/
    └── Pruebas automatizadas
```

El árbol muestra los componentes principales y los documentos de referencia; no pretende enumerar todos los archivos del repositorio.

## Compilación y pruebas

Desde la raíz de la solución:

```bash
dotnet restore SecureCredentials.slnx
dotnet build SecureCredentials.slnx
dotnet test SecureCredentials.slnx
```

Para ejecutar únicamente las pruebas de seguridad del almacenamiento:

```bash
dotnet test SecureCredentials.slnx \
    --filter FullyQualifiedName~CredentialStorageSecurityTests \
    --logger "console;verbosity=normal"
```

**Última verificación comunicada (28 de octubre de 2026):**

- Compilación correcta.
- **106 pruebas superadas**, 0 fallidas y 0 omitidas en la ejecución completa.
- **7 pruebas específicas de seguridad del almacenamiento superadas**, con 0 advertencias de compilación en esa ejecución.
- Las pruebas específicas incluyen rechazo de enlaces simbólicos, permisos inseguros y limpieza de temporales y descriptores ante fallos de escritura POSIX simulados.

Estos resultados validan los escenarios cubiertos por las pruebas, pero **no constituyen una auditoría de seguridad integral**.

## Limitaciones actuales

- Las garantías completas de almacenamiento seguro están implementadas y probadas para Linux.
- Windows x64 dispone de validación funcional; el endurecimiento de almacenamiento siguen pendientes; la ejecución sin .NET 10 ya está validada.
- `EmbeddedKeyProvider` no proporciona protección frente a ingeniería inversa.
- `DevelopmentKeyProvider` genera claves temporales que no persisten entre instancias.
- La biblioteca cifra texto UTF-8; no interpreta ni valida el formato del contenido.
- La interoperabilidad requiere una gestión coherente y segura de las claves entre las implementaciones.

## Distribución en Windows x64

Desde PowerShell, en la raíz del repositorio:

```powershell
dotnet test SecureCredentials.slnx -c Release
.\fabricar_entregables_windows.ps1 -ManagerVersion 1.0.1 -RuntimeIdentifier win-x64
```

El script restaura, compila, ejecuta pruebas, publica la GUI con `--self-contained true` y `PublishSingleFile=true`, genera `dist/paquetes/SecureCredentials.Manager-1.0.1-win-x64.zip` y `SHA256SUMS-win-x64.txt`. No se debe utilizar `-SkipTests` para una entrega validada. Extraer **todo** el ZIP antes de ejecutar `SecureCredentials.Manager.exe`; no requiere SDK .NET en el equipo receptor y se espera que no requiera runtime .NET independiente, y su ejecución sin runtime .NET 10 ya se ha comprobado en un equipo Windows x64 con .NET 6.0.7 instalado.

Para verificar el ZIP:

```powershell
Get-FileHash .\dist\paquetes\SecureCredentials.Manager-1.0.1-win-x64.zip -Algorithm SHA256
Get-Content .\dist\paquetes\SHA256SUMS-win-x64.txt
```

Consultar [VALIDACION_WINDOWS_1.0.1.md](VALIDACION_WINDOWS_1.0.1.md) y [GUIA_DISTRIBUCION.md](GUIA_DISTRIBUCION.md).

## Documentación técnica

- **[FORMATO_ENC.md](FORMATO_ENC.md):** contrato del formato cifrado v2, estructura JSON, AES-256-GCM, AAD e interoperabilidad C#/Python.
- **[DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md):** arquitectura, API pública, proveedores de claves, gestión de errores y seguridad.
- **[GUIA_INTEROPERABILIDAD_PYTHON.md](GUIA_INTEROPERABILIDAD_PYTHON.md):** cifrado y descifrado desde Python, ejemplos y precauciones.
- **[GUIA_ADMINISTRADOR.md](GUIA_ADMINISTRADOR.md):** guía de operación del gestor gráfico, permisos y resolución de problemas.

## Evolución prevista

El proyecto contempla su integración con nuevas aplicaciones, entre ellas:

- **SecureCredentials.Configuration:** componente para interpretar configuraciones JSON protegidas mediante SecureCredentials.
- Mejoras futuras en la administración y el almacenamiento seguro de claves.
- Desarrollo y validación de las garantías de almacenamiento en Windows.

`SecureCredentials.Manager` **ya está implementado y probado**; no se incluye entre las funcionalidades pendientes.

> **Nota de seguridad:** utiliza siempre datos ficticios en ejemplos y evita publicar claves AES, archivos de credenciales reales u otra información sensible en el repositorio.
