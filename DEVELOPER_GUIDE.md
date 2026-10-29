# SecureCredentials — Guía del desarrollador

**Estado de referencia:** 28 de octubre de 2026  
**Plataforma principal:** .NET 10 / Linux  
**Verificación comunicada:** compilación correcta, 106/106 pruebas superadas (0 errores, 0 omitidas). Las 7 pruebas específicas de seguridad se ejecutaron sin advertencias.

> Esta guía describe el diseño y las API revisadas durante el desarrollo. Los archivos `SecureCredentialsReader.cs`, `SecureCredentialsWriter.cs` y la configuración de los proyectos no estaban disponibles para una nueva inspección completa al redactarla; los ejemplos de esas dos API se basan en las firmas ya verificadas durante el trabajo conjunto. Antes de publicar una versión, contrastar la guía con el árbol definitivo del repositorio.

## 1. Propósito y alcance

`SecureCredentials` es una biblioteca C# para **cifrar y descifrar texto UTF-8 arbitrario** y almacenar su representación cifrada en archivos. El texto puede contener JSON, XML, cadenas de conexión u otros formatos: la biblioteca **no interpreta ni valida** la estructura del contenido original.

El objetivo es impedir la lectura casual de información sensible almacenada en disco y ofrecer una API sencilla para otras aplicaciones. No es un gestor completo de secretos ni sustituye la gestión segura de claves, permisos del sistema, control de acceso o políticas de rotación.

### Capacidades principales

- Cifrado autenticado **AES-256-GCM** con clave de 32 bytes.
- Nonce aleatorio de **12 bytes** y etiqueta de autenticación de **16 bytes**.
- Serialización de un sobre cifrado en JSON.
- Metadatos autenticados (AAD) en el formato **versión 2**.
- Lectura compatible con sobres de **versión 1**, según la implementación de `CredentialDecryptor`.
- Almacenamiento seguro en Linux con **700** para directorios privados de claves, directorios de documentos propios sin escritura para grupo/otros y **600** para archivos cifrados.
- Operaciones Linux basadas en descriptores POSIX, comprobación de propietario, rechazo de enlaces simbólicos y escritura mediante archivo temporal, renombrado y sincronización.
- Límite de tamaño del archivo cifrado: **1 MiB (1 048 576 bytes)**.

## 2. Organización de la solución

```text
SecureCredentials.slnx
├── SecureCredentials/                 # Biblioteca
│   ├── CredentialEncryptor.cs         # Cifrado AES-GCM
│   ├── CredentialDecryptor.cs         # Descifrado y validación
│   ├── CredentialMetadata.cs          # Versión, algoritmo y AAD
│   ├── CredentialFileStorage.cs       # Persistencia JSON cifrada
│   ├── CredentialDirectory.cs         # Directorios privados
│   ├── IKeyProvider.cs                # Contrato de proveedores de clave
│   ├── EmbeddedKeyProvider.cs         # Clave integrada
│   ├── DevelopmentKeyProvider.cs      # Clave temporal de pruebas
│   ├── LinuxSecretServiceKeyProvider.cs
│   ├── LinuxFileOwnership.cs
│   ├── LinuxUserIdentity.cs
│   ├── LinuxSecureFileOperations.cs
│   ├── LinuxSecureWriteOperations.cs
│   ├── SecureCredentialsReader.cs     # Fachada de lectura
│   └── SecureCredentialsWriter.cs     # Fachada de escritura
├── SecureCredentials.Demo/            # Aplicación de demostración
├── SecureCredentials.Manager/         # Gestor gráfico Avalonia
└── SecureCredentials.Tests/           # Pruebas automatizadas
```

`Class1.cs` / `LibraryInfo` se retiró por no aportar funcionalidad necesaria. Los nombres de archivo anteriores reflejan los componentes revisados, no necesariamente todos los archivos de la solución.

## 3. Requisitos y preparación

- SDK de **.NET 10** para compilar la solución.
- Linux para disponer de las garantías POSIX descritas en esta guía.
- Dependencia `Mono.Unix.Native` para las operaciones nativas utilizadas por el código Linux; restaurar los paquetes definidos en el proyecto.
- Para `LinuxSecretServiceKeyProvider`: herramienta **`/usr/bin/secret-tool`** y servicio de secretos accesible para el proceso.

Desde la raíz del repositorio:

```bash
dotnet restore SecureCredentials.slnx
dotnet build SecureCredentials.slnx
dotnet test SecureCredentials.slnx
```

Para repetir una validación completa:

```bash
dotnet clean SecureCredentials.slnx
dotnet build SecureCredentials.slnx
dotnet test SecureCredentials.slnx
```

**Referencia de calidad al cierre de esta fase:** 106 pruebas correctas, cero errores y cero omitidas. Las 7 pruebas específicas de seguridad compilaron sin advertencias. Esto no constituye por sí solo una auditoría criptográfica ni una validación de Windows.

## 4. Uso recomendado: API de alto nivel

Las fachadas `SecureCredentialsWriter` y `SecureCredentialsReader` permiten guardar y recuperar texto sin manipular directamente el sobre JSON.

```csharp
using SecureCredentials;

string filePath = "/home/usuario/.config/mi-aplicacion/credentials.enc";
string originalText = "{\"server\":\"db.local\",\"user\":\"app\"}";

await SecureCredentialsWriter.WriteAsync(filePath, originalText);

string recoveredText = await SecureCredentialsReader.ReadAsync(filePath);
Console.WriteLine(recoveredText == originalText);
```

**Importante:** el ejemplo es ilustrativo. El archivo contiene texto cifrado, no JSON en claro. Las fachadas usan el proveedor de clave integrada en el diseño revisado; ambas ejecuciones deben poder obtener **la misma clave**. No imprimir secretos reales en consola ni incluirlos en registros.

### Responsabilidades del llamador

1. Elegir una ubicación privada adecuada para el archivo.
2. Asegurar que la clave configurada permite recuperar datos tras reiniciar la aplicación.
3. Tratar el texto descifrado como información sensible.
4. Gestionar las excepciones y los permisos de acceso sin exponer secretos en mensajes o logs.
5. Definir una estrategia de copia de seguridad, recuperación y eventual rotación de claves.

## 5. API criptográfica de bajo nivel

### 5.1 Cifrar texto

```csharp
using System.Security.Cryptography;
using SecureCredentials;

IKeyProvider provider = new EmbeddedKeyProvider();
byte[] key = await provider.GetKeyAsync(EmbeddedKeyProvider.DefaultKeyId);

try
{
    EncryptedData envelope = CredentialEncryptor.Encrypt(
        "texto confidencial",
        key,
        EmbeddedKeyProvider.DefaultKeyId);

    // Persistir envelope con CredentialFileStorage.SaveAsync(...)
}
finally
{
    CryptographicOperations.ZeroMemory(key);
}
```

`CredentialEncryptor.Encrypt(string plaintext, byte[] key, string keyId)` valida los argumentos, genera un nonce aleatorio, convierte el texto a UTF-8 y produce un `EncryptedData`. Los bytes temporales del texto en claro y los AAD se borran en `finally`. El `string` original, por ser administrado e inmutable, **no puede borrarse de memoria de forma garantizada**.

### 5.2 Descifrar un sobre

```csharp
using System.Security.Cryptography;
using SecureCredentials;

IKeyProvider provider = new EmbeddedKeyProvider();
EncryptedData envelope = await CredentialFileStorage.LoadAsync(filePath);
byte[] key = await provider.GetKeyAsync(envelope.KeyId);

try
{
    string plaintext = CredentialDecryptor.Decrypt(envelope, key);
    // Utilizar plaintext sin registrarlo ni mostrarlo innecesariamente.
}
finally
{
    CryptographicOperations.ZeroMemory(key);
}
```

`CredentialDecryptor.Decrypt(EncryptedData encrypted, byte[] key)` valida la longitud de la clave, la versión y el algoritmo; decodifica los campos Base64; comprueba las longitudes de nonce y etiqueta; y realiza el descifrado autenticado. Una clave incorrecta o datos manipulados normalmente provocan un fallo de autenticación criptográfica.

**Compatibilidad:** la versión 2 usa AAD; la versión 1 se descifra sin AAD. El código revisado **genera nuevos sobres únicamente en versión 2**. No asumir que cualquier cambio de formato es retrocompatible.

## 6. Formato del archivo cifrado

`CredentialFileStorage` serializa `EncryptedData` como JSON UTF-8. Estructura orientativa, con valores Base64 abreviados y no operativos:

```json
{
  "Version": 2,
  "Algorithm": "AES-256-GCM",
  "KeyId": "embedded-v1",
  "Nonce": "BASE64_NONCE",
  "Ciphertext": "BASE64_CIPHERTEXT",
  "Tag": "BASE64_TAG"
}
```

| Campo | Significado |
|---|---|
| `Version` | Versión del formato; actualmente 2 para nuevas escrituras. |
| `Algorithm` | Identificador `AES-256-GCM`. |
| `KeyId` | Identificador para localizar la clave adecuada; **no es la clave**. |
| `Nonce` | Nonce de 12 bytes codificado en Base64. |
| `Ciphertext` | Texto UTF-8 cifrado, codificado en Base64. |
| `Tag` | Etiqueta GCM de 16 bytes codificada en Base64. |

Los campos de metadatos son visibles en el JSON. La confidencialidad se aplica al contenido de `Ciphertext`, mientras que la integridad/autenticidad de los metadatos de versión 2 se protege mediante AAD.

### 6.1 Datos adicionales autenticados (AAD)

`CredentialMetadata.CreateAuthenticatedData(version, algorithm, keyId)` construye esta secuencia:

```text
ASCII("SecureCredentials|2|AES-256-GCM|")
+ longitud de KeyId en UTF-8 (entero de 32 bits, big-endian)
+ bytes UTF-8 de KeyId
```

El identificador no puede superar **256 bytes UTF-8**. El prefijo, el orden de los campos y la codificación de longitud son parte del formato: **no modificarlos sin introducir una estrategia explícita de versionado y migración**.

## 7. Proveedores de claves

Todos implementan:

```csharp
public interface IKeyProvider
{
    Task<byte[]> GetKeyAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}
```

| Proveedor | Comportamiento | Uso apropiado |
|---|---|---|
| `EmbeddedKeyProvider` | Recupera una clave AES-256 incorporada al código; identificador `embedded-v1`. | Integración sencilla con riesgo de extracción por ingeniería inversa aceptado. |
| `DevelopmentKeyProvider` | Genera una clave aleatoria por instancia; devuelve copias; la borra en `Dispose()`. | Pruebas y escenarios efímeros. |
| `LinuxSecretServiceKeyProvider` | Ejecuta `/usr/bin/secret-tool lookup application SecureCredentials key-id <id>` y espera Base64 de 32 bytes. | Integraciones Linux con servicio de secretos disponible. |

### 7.1 Clave integrada

La clave integrada debe tener **exactamente 32 bytes**, codificados en Base64. En entornos de laboratorio se puede utilizar un marcador de sustitución; el valor de la clave real no debe incluirse en la documentación. **No documentar, compartir ni confirmar el valor real de esa clave en el repositorio.**

Una clave incrustada puede recuperarse mediante análisis del binario: **no proporciona protección frente a un adversario con acceso al ejecutable y capacidad de ingeniería inversa**. Cambiar la clave sin migrar los archivos antiguos puede hacerlos irrecuperables.

### 7.2 Clave de desarrollo

`DevelopmentKeyProvider` genera una clave nueva al construir cada instancia. El argumento `keyId` se valida, pero no selecciona claves distintas dentro de la misma instancia. `Dispose()` borra la clave interna; el llamador debe borrar también cualquier copia recibida.

### 7.3 Servicio de secretos Linux

`LinuxSecretServiceKeyProvider` requiere `secret-tool` en la ruta exacta `/usr/bin/secret-tool`. Ejecuta el programa sin shell y solicita una clave Base64 de 32 bytes. La cancelación intenta terminar el proceso externo. **La existencia del proveedor no implica que las fachadas Reader/Writer lo seleccionen automáticamente**: cualquier integración alternativa debe diseñarse y verificarse explícitamente.

## 8. Almacenamiento y seguridad en Linux

**Cambio de política (octubre de 2026):** los directorios de documentos `.enc` ya no necesitan tener permisos exactos `700`. Se mantiene la política estricta para directorios privados de claves, pero se permite guardar documentos en carpetas propias como `~/Proyectos` con permisos `755`, siempre que no tengan escritura de grupo ni de otros. No se modifica el modo de los directorios existentes para forzar su aceptación.

### 8.1 Directorios

`CredentialDirectory.CreatePrivate(path)`:

- Convierte la ruta a absoluta.
- Comprueba los componentes de la ruta para detectar enlaces simbólicos.
- Crea el directorio, si es necesario, solicitando permisos **0700**.
- Repite la comprobación de enlaces.
- Verifica que el propietario coincide con el **UID efectivo** del proceso.
- Exige permisos finales exactamente **0700**.

`CredentialDirectory.PrepareEncryptedDocumentDirectory(path)` aplica la política de directorios que alojan documentos cifrados:

- Admite directorios existentes propios como `700`, `750` o `755` si no son escribibles por grupo u otros.
- Rechaza permisos como `777` que permiten escritura no autorizada.
- No convierte automáticamente una carpeta existente a `700`.
- Mantiene la validación de propietario y las comprobaciones de rutas correspondientes.

`LinuxSecureFileOperations.OpenDirectory()` conserva la política estricta de directorios privados; `OpenDocumentDirectory()` permite la política específica de documentos `.enc`. `LinuxSecureWriteOperations.OpenDirectory()` utiliza la apertura de directorios de documentos.

La comprobación basada en rutas no elimina por sí sola todas las condiciones de carrera. Las operaciones sensibles de lectura/escritura Linux utilizan además descriptores POSIX.

### 8.2 Lectura

En Linux, `CredentialFileStorage.LoadAsync(path)` abre el directorio de documentos mediante operaciones seguras, abre el archivo con respecto al descriptor del directorio y **sin seguir enlaces simbólicos**, comprueba que es un archivo regular de propiedad y permisos adecuados (**0600**) y lee desde el descriptor validado. El límite de lectura es 1 MiB. Después deserializa el JSON y limpia el buffer de bytes en memoria.

### 8.3 Escritura

`CredentialFileStorage.SaveAsync(path, encrypted)` serializa el sobre JSON y verifica el límite de tamaño. En Linux delega en la escritura segura basada en descriptores POSIX:

1. Prepara y abre el directorio de documentos bajo su política de propiedad y permisos; valida el destino existente.
2. Crea un archivo temporal privado en el mismo directorio.
3. Escribe los bytes y sincroniza el archivo (`fsync`).
4. Cierra el descriptor temporal.
5. Renombra el temporal sobre el destino.
6. Sincroniza el directorio (`fsync`).
7. Intenta limpiar recursos y temporales, preservando las excepciones primarias cuando corresponda.

La operación de renombrado permite una sustitución atómica dentro del mismo sistema de archivos, con las garantías concretas que proporcione este. El código incluye sincronizaciones para mejorar la persistencia ante fallos; no debe interpretarse como garantía absoluta frente a cualquier avería de hardware o sistema de archivos.

### 8.4 Límite de tamaño

El límite se aplica al **archivo JSON cifrado**, no directamente a la longitud del texto original. La codificación Base64 y los metadatos aumentan el tamaño final. Un texto original ligeramente inferior a 1 MiB puede generar un sobre superior al límite.

## 9. Comportamiento en otros sistemas operativos

En sistemas distintos de Linux, `CredentialDirectory.CreatePrivate()` utiliza `Directory.CreateDirectory()` sin las mismas comprobaciones POSIX. `CredentialFileStorage` dispone de una ruta alternativa de lectura y escritura con archivos temporales y `File.Move`, pero **no ofrece todavía garantías equivalentes a las de Linux**.

**Windows queda como plataforma pendiente de endurecimiento y pruebas específicas.** No presentar la implementación actual como un almacén de secretos seguro para Windows hasta definir ACL, validación de enlaces/reparse points, propiedad, sustitución segura y pruebas de concurrencia.

## 10. Gestión de errores

Las excepciones exactas dependen del punto de fallo. Casos relevantes:

| Situación | Excepciones posibles |
|---|---|
| Ruta o identificador inválido | `ArgumentException`, `ArgumentNullException` |
| Archivo ausente | `FileNotFoundException` |
| Permisos, propietario o enlace no admitido | `UnauthorizedAccessException` |
| Error de E/S o POSIX | `IOException` |
| Sobre demasiado grande o estructura inválida | `InvalidDataException` |
| Versión o algoritmo incompatible | `NotSupportedException` |
| Base64 mal formado | `FormatException` o excepción encapsulada por el proveedor |
| Clave AES de longitud incorrecta | `ArgumentException`, `InvalidDataException` o `InvalidOperationException`, según API |
| Fallo de autenticación GCM | `CryptographicException` |
| Operación cancelada | `OperationCanceledException` |
| Proveedor no disponible en la plataforma | `PlatformNotSupportedException` |

Ejemplo de tratamiento en una aplicación consumidora:

```csharp
using System.Security.Cryptography;
using SecureCredentials;

try
{
    string value = await SecureCredentialsReader.ReadAsync(filePath);
    // Procesar value sin incluirlo en logs.
}
catch (FileNotFoundException)
{
    // El archivo todavía no existe.
}
catch (UnauthorizedAccessException)
{
    // Revisar permisos, propietario o enlaces simbólicos.
}
catch (CryptographicException)
{
    // Clave incorrecta, archivo alterado o autenticación fallida.
}
catch (InvalidDataException)
{
    // Archivo cifrado inválido o demasiado grande.
}
```

No usar un `catch` genérico para continuar con valores por defecto silenciosos cuando se espera una credencial válida. No registrar contenido descifrado ni claves.

## 11. Pruebas y mantenimiento

La solución dispone de pruebas unitarias y de integración, incluidas comprobaciones de operaciones Linux y de las fachadas de lectura/escritura. El estado comunicado tras las últimas pruebas fue:

```text
Compilación: correcta
Pruebas completas: 106 correctas, 0 errores, 0 omitidas
Pruebas específicas de almacenamiento: 7 correctas, 0 errores
Advertencias en la ejecución de seguridad: 0
```

Para ejecutar exclusivamente las pruebas de almacenamiento:

```bash
dotnet test SecureCredentials.slnx \
    --filter FullyQualifiedName~CredentialStorageSecurityTests \
    --logger "console;verbosity=normal"
```

Los siete escenarios específicos son: rechazo de enlace simbólico al leer, rechazo de enlace simbólico al guardar, rechazo de archivo `644`, rechazo de directorio `777`, política estricta `700` para directorio privado, ausencia de temporales tras fallo de validación y limpieza/cierre de descriptores tras fallo POSIX simulado.

La última prueba utiliza una simulación controlada de fallo durante la escritura; no implica haber reproducido todos los fallos reales de disco o del sistema de archivos.

Antes de integrar cambios:

```bash
dotnet clean SecureCredentials.slnx
dotnet build SecureCredentials.slnx
dotnet test SecureCredentials.slnx
```

Mantener `<GenerateDocumentationFile>true</GenerateDocumentationFile>` y documentar cualquier miembro público nuevo. Evitar suprimir `CS1591` para ocultar documentación pendiente.

### Cambios que requieren especial atención

- **Formato/AAD:** crear pruebas de compatibilidad con archivos existentes antes de modificar versión, prefijo, campos o codificación.
- **Claves:** no sustituir claves persistentes sin procedimiento de migración y recuperación.
- **Permisos:** probar propietarios, modo privado `700`, directorios de documentos `700`/`750`/`755` frente a `777`, archivos `600` frente a `644`, enlaces simbólicos, archivos especiales y errores POSIX.
- **Escritura:** probar fallos parciales, limpieza de temporales, sincronización y sustitución del destino.
- **Portabilidad:** añadir pruebas propias de cada sistema operativo antes de afirmar equivalencia de seguridad.

## 12. Recomendaciones para aplicaciones consumidoras

- Usar `SecureCredentialsWriter` y `SecureCredentialsReader` para el caso habitual de guardar/leer texto cifrado.
- Utilizar la API de bajo nivel solo cuando sea necesario controlar explícitamente el proveedor de clave o el sobre.
- No colocar secretos reales en código de ejemplo, pruebas compartidas, documentación ni logs.
- No considerar el identificador `KeyId` como material secreto.
- Mantener copias de seguridad de los archivos cifrados y un procedimiento seguro de conservación de claves.
- Minimizar el tiempo de vida del texto descifrado y borrar arrays de claves con `CryptographicOperations.ZeroMemory` cuando corresponda.
- Evitar usar `DevelopmentKeyProvider` para datos que deban sobrevivir al proceso.
- No interpretar las 106 pruebas superadas como sustituto de una revisión de seguridad independiente.

## 13. Trabajo futuro

1. Revisar y validar esta guía frente a los archivos finales de las fachadas Reader/Writer y el `.csproj`.
2. Endurecer la implementación de Windows y añadir su batería de pruebas.
3. Definir un mecanismo de selección configurable de `IKeyProvider` para las API de alto nivel, si se requiere.
4. Diseñar rotación de claves, migración de sobres y recuperación ante pérdida de claves.
5. Mantener la documentación de `SecureCredentials.Manager`, ya implementado y probado, y documentar posteriormente `SecureCredentials.Configuration` cuando se desarrolle.
6. Mantener pruebas de compatibilidad de formato entre versiones.

## 14. SecureCredentials.Manager: aplicación gráfica

`SecureCredentials.Manager` es la interfaz de escritorio Avalonia para abrir documentos `.enc`, editar su contenido descifrado y guardar de nuevo el documento cifrado. La interfaz **no interpreta** el JSON o XML que pueda contener el texto.

Ejecución desde la raíz de la solución:

```bash
dotnet run --project SecureCredentials.Manager
```

Ubicaciones de estado del gestor en Linux:

| Ruta | Contenido |
|---|---|
| `~/.config/securecredentials-manager/settings.json` | Preferencias, incluida la última carpeta utilizada |
| `~/.local/state/securecredentials-manager/manager.log` | Registro de actividad y diagnóstico |

No incluir claves ni texto descifrado en los registros. El gestor y el script Python han sido probados de forma interoperable en ambos sentidos: **Python → C#** y **C# → Python**, usando la misma clave AES, `KeyId` y AAD del formato v2. No se debe publicar la clave integrada ni copiarla a documentación o scripts distribuidos.

Para los procedimientos de operación y recuperación, consultar [GUIA_ADMINISTRADOR.md](GUIA_ADMINISTRADOR.md).

## 15. Documentación relacionada

- [README.md](README.md): presentación, estructura y comandos de la solución.
- [FORMATO_ENC.md](FORMATO_ENC.md): contrato criptográfico y de serialización v2.
- [GUIA_ADMINISTRADOR.md](GUIA_ADMINISTRADOR.md): operación del gestor, permisos, diagnóstico y distribución de documentos.
- [GUIA_INTEROPERABILIDAD_PYTHON.md](GUIA_INTEROPERABILIDAD_PYTHON.md): uso de la utilidad Python y compatibilidad con el formato v2.

---

**Principio de diseño:** `SecureCredentials` cifra y descifra **texto**, no interpreta su contenido. Las aplicaciones consumidoras son responsables de validar el JSON, XML u otro formato una vez recuperado.
