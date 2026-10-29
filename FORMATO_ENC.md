# Especificación del formato cifrado `.enc` de SecureCredentials

**Estado:** formato v2 implementado y probado · **Proyecto:** SecureCredentials (.NET 10) · **Algoritmo:** AES-256-GCM

## 1. Objetivo y alcance

Este documento describe el contrato criptográfico y de serialización de los documentos cifrados de **SecureCredentials**, para que puedan generarse y descifrarse de forma compatible desde C# y otras implementaciones, como Python.

El formato `.enc` es un **documento JSON UTF-8** que contiene un sobre criptográfico. La extensión `.enc` es una convención de uso: la estructura del contenido, no el nombre del archivo, determina el formato. La biblioteca cifra y descifra **texto UTF-8**; si ese texto es JSON, XML u otro formato, se trata como contenido opaco.

Esta especificación distingue:

- **Formato criptográfico:** campos JSON, AES-GCM, codificación Base64 y AAD.
- **Política de almacenamiento:** tamaño máximo, permisos, propietario, enlaces simbólicos y escritura segura que aplica `CredentialFileStorage` en Linux.
- **Gestión de claves:** responsabilidad de los proveedores de claves y del llamador; la clave **no** se almacena dentro del documento `.enc`.

## 2. Identificación de la versión actual

| Propiedad | Valor de la versión 2 |
|---|---|
| `Version` | Entero JSON `2` |
| `Algorithm` | Cadena exacta `AES-256-GCM` |
| Clave AES | 32 bytes (256 bits) |
| Nonce GCM | 12 bytes (96 bits), generado aleatoriamente en cada cifrado |
| Etiqueta GCM | 16 bytes (128 bits) |
| Codificación del texto original | UTF-8 |
| Codificación de valores binarios del sobre | Base64 estándar |
| Autenticación de metadatos | AAD definido en la sección 4 |
| Serialización | JSON UTF-8 |

`CredentialMetadata.CurrentVersion` define la versión `2` y `CredentialMetadata.Algorithm` define `AES-256-GCM`.

## 3. Estructura JSON

Ejemplo **ilustrativo**, no utilizable para descifrar porque los campos Base64 están abreviados:

```json
{
  "Version": 2,
  "Algorithm": "AES-256-GCM",
  "KeyId": "embedded-v1",
  "Nonce": "<base64-de-12-bytes>",
  "Ciphertext": "<base64-del-texto-cifrado>",
  "Tag": "<base64-de-16-bytes>"
}
```

| Campo | Tipo JSON | Significado | Reglas |
|---|---|---|---|
| `Version` | número entero | Versión del sobre | Para el formato actual, exactamente `2` |
| `Algorithm` | cadena | Algoritmo de cifrado | Exactamente `AES-256-GCM` |
| `KeyId` | cadena | Identificador de la clave | No vacío ni compuesto solo por espacios; representación UTF-8 de hasta 256 bytes |
| `Nonce` | cadena | Nonce del cifrado | Base64 que decodifica exactamente 12 bytes |
| `Ciphertext` | cadena | Texto cifrado | Base64; su longitud binaria coincide con la del texto original en bytes |
| `Tag` | cadena | Etiqueta de autenticación GCM | Base64 que decodifica exactamente 16 bytes |

**Importante:** `KeyId` es un identificador, **no** una contraseña, una clave AES ni una instrucción para obtener una clave de Internet. El ejemplo `embedded-v1` corresponde a una identificación utilizada en el proyecto; no es un valor obligatorio del formato.

La serialización debe conservar los nombres de campo mostrados para interoperar con los lectores existentes. El orden de los campos y los espacios del JSON no participan en el cálculo de la etiqueta GCM; sí participan sus **valores semánticos autenticados**, según la sección siguiente.

## 4. Construcción exacta del AAD en v2

AES-GCM admite *Additional Authenticated Data* (AAD): bytes que **no se cifran**, pero cuya integridad queda protegida por la etiqueta de autenticación.

En SecureCredentials v2, el AAD se construye concatenando, en este orden:

1. El prefijo ASCII literal `SecureCredentials|2|AES-256-GCM|`.
2. La longitud en **bytes UTF-8** de `KeyId`, representada como entero **sin signo de 32 bits, big-endian** (4 bytes). La implementación C# escribe un entero de 32 bits big-endian; el límite de 256 bytes hace equivalentes ambas representaciones.
3. Los bytes UTF-8 de `KeyId`.

Formalmente:

```text
AAD = ASCII("SecureCredentials|2|AES-256-GCM|")
    || UINT32_BE(longitud(UTF8(KeyId)))
    || UTF8(KeyId)
```

`||` significa concatenación de bytes, no los caracteres `|` del prefijo.

### Ejemplo con `KeyId = "embedded-v1"`

`embedded-v1` contiene 11 bytes ASCII/UTF-8, por lo que la longitud se codifica como `00 00 00 0B` en hexadecimal:

```text
AAD = 53 65 63 75 72 65 43 72 65 64 65 6E 74 69 61 6C 73
      7C 32 7C 41 45 53 2D 32 35 36 2D 47 43 4D 7C
      00 00 00 0B
      65 6D 62 65 64 64 65 64 2D 76 31
```

El AAD **no** es el JSON serializado ni una concatenación de las representaciones Base64. Debe reconstruirse con los valores de `Version`, `Algorithm` y `KeyId` que aparecen en el documento y las reglas anteriores. Para v2, la implementación rechaza versiones o algoritmos diferentes antes de construirlo.

**Consecuencia de seguridad:** modificar `KeyId` en un archivo v2 cambia el AAD y provoca un fallo de autenticación, incluso si no se modifica `Ciphertext`. `Version` y `Algorithm` están vinculados al formato admitido y a su prefijo AAD.

## 5. Proceso de cifrado

La implementación C# de referencia es `CredentialEncryptor.Encrypt(plaintext, key, keyId)`.

1. Validar que el texto, la clave y `KeyId` son aceptables; exigir una clave de **32 bytes**.
2. Generar un nonce aleatorio nuevo de **12 bytes** mediante un generador criptográfico.
3. Convertir `plaintext` a bytes UTF-8.
4. Construir el AAD v2 según la sección 4.
5. Ejecutar AES-256-GCM con clave, nonce, texto original y AAD; obtener `Ciphertext` y una etiqueta `Tag` de **16 bytes**.
6. Codificar `Nonce`, `Ciphertext` y `Tag` por separado en Base64 y construir el objeto `EncryptedData` con los seis campos.
7. Borrar los buffers temporales de texto original y AAD que controla la implementación. La clave recibida pertenece al llamador y no se borra automáticamente.

**Regla crítica:** nunca reutilizar el mismo nonce con la misma clave AES-GCM. El cifrador genera un nonce aleatorio por operación; para un sistema con un volumen de cifrados muy elevado se debe revisar también la estrategia de gestión y rotación de claves y los límites operativos de GCM.

## 6. Proceso de descifrado

La implementación C# de referencia es `CredentialDecryptor.Decrypt(encrypted, key)`.

1. Validar que la clave tiene 32 bytes.
2. Comprobar que la versión y el algoritmo son compatibles.
3. Decodificar los campos Base64.
4. Exigir nonce de 12 bytes y etiqueta de 16 bytes.
5. Para v2, reconstruir el AAD a partir de `Version`, `Algorithm` y `KeyId`.
6. Ejecutar AES-GCM con clave, nonce, `Ciphertext`, `Tag` y AAD.
7. **Solo si la autenticación es correcta**, devolver el texto UTF-8 recuperado.
8. Borrar los buffers temporales de texto descifrado y AAD que controla la implementación.

Una etiqueta inválida, una clave equivocada o una modificación del contenido autenticado hacen fallar el descifrado con una excepción criptográfica. **No se debe utilizar ni mostrar texto descifrado parcial tras un fallo de autenticación.**

La función de descifrado no selecciona por sí misma la clave a partir de `KeyId`: recibe los 32 bytes de clave del código llamador. El proveedor de claves debe garantizar que se usa la clave correspondiente.

## 7. Compatibilidad con la versión 1

El código actual de `CredentialDecryptor` **admite lectura de versiones 1 y 2**, siempre que el algoritmo sea `AES-256-GCM` y se satisfagan las validaciones correspondientes.

| Característica | Versión 1 | Versión 2 |
|---|---|---|
| Descifrado en el código C# actual | Sí, por compatibilidad | Sí |
| Generación mediante `CredentialEncryptor` actual | No | Sí |
| AAD para GCM | Vacío | Prefijo + longitud + `KeyId` |
| Protección criptográfica del identificador `KeyId` mediante AAD | No | Sí |

En la ruta de compatibilidad v1 se usa **AAD vacío**. Por ello, no debe atribuirse a v1 la autenticación de metadatos que ofrece v2. La utilidad Python de interoperabilidad examinada para este documento **solo admite v2**.

No se recomienda crear nuevos archivos v1. Para migrar un documento heredado, descifrarlo con la clave adecuada y volver a cifrarlo como v2, generando un nonce nuevo y guardando el nuevo documento de forma segura. Esta operación requiere conocer y proteger la clave correcta.

## 8. Interoperabilidad C# y Python

Las bibliotecas criptográficas pueden representar la salida GCM de forma diferente:

- **C# (`AesGcm`)** recibe y devuelve `ciphertext` y `tag` en buffers separados.
- **Python (`cryptography.hazmat.primitives.ciphers.aead.AESGCM`)** devuelve `ciphertext || tag` en un único resultado al cifrar y espera esa concatenación al descifrar.

Por tanto, una implementación Python compatible debe dividir los **últimos 16 bytes** de la salida de `AESGCM.encrypt()` para almacenarlos en `Tag`, y concatenar `Ciphertext + Tag` antes de llamar a `AESGCM.decrypt()`.

Ejemplo mínimo del contrato criptográfico, usando una **clave de laboratorio proporcionada por el llamador**:

```python
import base64
import secrets
import struct
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

PREFIX = b"SecureCredentials|2|AES-256-GCM|"

def build_aad(key_id: str) -> bytes:
    if not key_id or not key_id.strip():
        raise ValueError("KeyId no puede estar vacio")
    raw = key_id.encode("utf-8")
    if len(raw) > 256:
        raise ValueError("KeyId supera 256 bytes UTF-8")
    return PREFIX + struct.pack(">I", len(raw)) + raw

def encrypt_text(plaintext: str, key: bytes, key_id: str) -> dict:
    if len(key) != 32:
        raise ValueError("Se requieren 32 bytes de clave")
    nonce = secrets.token_bytes(12)
    combined = AESGCM(key).encrypt(
        nonce, plaintext.encode("utf-8"), build_aad(key_id)
    )
    return {
        "Version": 2,
        "Algorithm": "AES-256-GCM",
        "KeyId": key_id,
        "Nonce": base64.b64encode(nonce).decode("ascii"),
        "Ciphertext": base64.b64encode(combined[:-16]).decode("ascii"),
        "Tag": base64.b64encode(combined[-16:]).decode("ascii"),
    }

def decrypt_text(envelope: dict, key: bytes) -> str:
    if len(key) != 32:
        raise ValueError("Se requieren 32 bytes de clave")
    if envelope["Version"] != 2 or envelope["Algorithm"] != "AES-256-GCM":
        raise ValueError("Formato no compatible")
    nonce = base64.b64decode(envelope["Nonce"], validate=True)
    ciphertext = base64.b64decode(envelope["Ciphertext"], validate=True)
    tag = base64.b64decode(envelope["Tag"], validate=True)
    if len(nonce) != 12 or len(tag) != 16:
        raise ValueError("Nonce o Tag invalidos")
    plain = AESGCM(key).decrypt(
        nonce, ciphertext + tag, build_aad(envelope["KeyId"])
    )
    return plain.decode("utf-8")
```

Este ejemplo explica el formato; **no implementa por sí solo** las protecciones de almacenamiento POSIX, gestión de claves, límite de tamaño o limpieza de memoria de la biblioteca C#. No debe considerarse un sustituto directo de `CredentialFileStorage`.

### Comprobaciones de interoperabilidad realizadas

Durante el desarrollo se han comprobado los dos sentidos: cifrado en Python y lectura desde C#, y cifrado desde la aplicación C# y descifrado en Python. También se ha comprobado el descifrado de archivos Python mediante Python y la igualdad del contenido recuperado cuando no se modifica el texto.

Para repetir las comprobaciones debe emplearse **la misma clave AES de prueba** en ambos extremos, el mismo `KeyId` y las reglas exactas de AAD. No incluir claves reales ni contraseñas en repositorios, registros o ejemplos.

## 9. Almacenamiento de archivos `.enc` en Linux

El formato JSON es independiente de la ubicación del archivo, pero `CredentialFileStorage` aplica controles adicionales en Linux:

- **Tamaño máximo del documento serializado:** 1 MiB (`1024 * 1024` bytes).
- **Permisos del archivo cifrado:** `0600` (lectura y escritura del propietario); se rechazan archivos existentes con permisos más amplios.
- **Directorio de documentos:** puede ser un directorio existente `0700`, `0750` o `0755`, entre otros, si cumple las validaciones de propietario y **no permite escritura por grupo ni por otros**. La biblioteca no cambia los permisos de un directorio existente simplemente por guardar un `.enc`.
- **Directorio de claves privadas:** mantiene una política distinta y estricta de `0700` mediante `CredentialDirectory.CreatePrivate()`.
- **Enlaces simbólicos:** las operaciones seguras de Linux rechazan los casos protegidos por la validación de rutas y descriptores POSIX; las pruebas cubren específicamente enlaces simbólicos en lectura y escritura.
- **Escritura:** se utiliza un temporal en el directorio de destino, sincronización y sustitución del archivo, con limpieza del temporal ante fallos.
- **Errores de escritura:** se ha probado la eliminación del temporal y el cierre de descriptores cuando se simula un fallo de escritura POSIX.

Estos controles protegen el acceso al archivo y ciertas operaciones del sistema de archivos; **no sustituyen** la autenticación GCM ni protegen contra un usuario o proceso que ya tenga acceso legítimo a la clave.

**Portabilidad:** la implementación alternativa para sistemas no Linux no ha recibido en esta fase la misma validación de seguridad POSIX. No extrapolar estas garantías a Windows o macOS sin revisión específica.

## 10. Modelo de seguridad y limitaciones

AES-256-GCM proporciona **confidencialidad** del texto y **detección de alteraciones** del contenido autenticado cuando la clave permanece secreta, el nonce se usa correctamente y se verifica la etiqueta.

El formato por sí solo **no** proporciona:

- Gestión segura, rotación o recuperación automática de claves.
- Protección contra un atacante que conozca la clave AES.
- Protección frente a borrado, sustitución por otro archivo `.enc` válido, restauración de una versión anterior (*rollback*) o pérdida de disponibilidad. GCM autentica cada sobre, pero no establece una secuencia histórica de archivos.
- Vinculación automática a una ruta, equipo, usuario o nombre de archivo: estos valores no forman parte del AAD v2.
- Garantía de que el texto descifrado tenga una estructura JSON concreta o cumpla reglas de negocio.
- Garantía de confidencialidad si la aplicación registra, imprime o exporta el texto en claro.

**Advertencia sobre claves integradas:** una clave incluida en el binario de una aplicación distribuida puede ser recuperable por quien tenga acceso a ese binario. Para proteger secretos reales, evaluar un proveedor de claves adecuado al entorno y al modelo de amenazas. No publicar la clave integrada ni reutilizar claves de pruebas en producción.

## 11. Errores y diagnóstico

| Situación | Comportamiento esperado |
|---|---|
| Versión desconocida | Rechazo por formato no compatible |
| `Algorithm` distinto de `AES-256-GCM` | Rechazo por algoritmo no compatible |
| Base64 inválido | Error de formato |
| Nonce o etiqueta con longitud incorrecta | Rechazo del documento |
| Clave de longitud diferente de 32 bytes | Error de argumento |
| Clave incorrecta o contenido/AAD alterado | Fallo de autenticación GCM |
| Documento mayor de 1 MiB al guardarlo con `CredentialFileStorage` | Rechazo por tamaño |
| Archivo Linux con permisos `0644` | Rechazo por política de almacenamiento |
| Directorio Linux con permisos `0777` | Rechazo por política de almacenamiento |

No registrar claves, contenido descifrado, datos sensibles ni volcados completos de los documentos durante el diagnóstico.

## 12. Verificación del proyecto

En la solución `SecureCredentials.slnx`, ejecutar:

```bash
dotnet test SecureCredentials.slnx \
    --filter FullyQualifiedName~CredentialStorageSecurityTests \
    --logger "console;verbosity=normal"

dotnet test SecureCredentials.slnx
```

**Última ejecución comunicada (28 de octubre de 2026):** 7 de 7 pruebas específicas de seguridad correctas; batería completa de **106 de 106** pruebas correctas, **0 errores** y **0 omitidas**. Estos resultados acreditan el alcance de las pruebas realizadas, no una auditoría formal de seguridad.

## 13. Archivos de referencia en el código

- `CredentialMetadata.cs`: constantes y construcción binaria del AAD.
- `CredentialEncryptor.cs`: cifrado AES-256-GCM y modelo `EncryptedData`.
- `CredentialDecryptor.cs`: descifrado, validaciones y compatibilidad v1/v2.
- `CredentialFileStorage.cs`: serialización JSON, límite de tamaño y almacenamiento.
- `CredentialDirectory.cs`: política de directorios de documentos y claves.
- `LinuxSecureFileOperations.cs` y `LinuxSecureWriteOperations.cs`: controles POSIX de Linux.
- `CredentialStorageSecurityTests.cs`: pruebas específicas de seguridad de almacenamiento.

## 14. Reglas de evolución del formato

Cualquier modificación de la versión, el algoritmo, la composición binaria del AAD, los tamaños criptográficos o los nombres/semántica de los campos requiere **evaluar una nueva versión del formato**, pruebas de interoperabilidad y una estrategia explícita de lectura de documentos anteriores.

No modificar silenciosamente el prefijo AAD ni la codificación de longitud: hacerlo impediría autenticar los archivos v2 existentes.
