# Guía de interoperabilidad Python ↔ SecureCredentials

**Utilidad:** `securecredentials_interop.py`  
**Formato:** SecureCredentials v2 (`AES-256-GCM`)  
**Uso previsto:** pruebas y operaciones controladas de laboratorio.

## 1. Finalidad

El script permite **cifrar archivos de texto UTF-8** en documentos `.enc` compatibles con SecureCredentials y **descifrar documentos `.enc` v2** para recuperar su contenido textual. No valida la estructura semántica de JSON, XML u otros formatos.

Se ha comprobado la interoperabilidad en ambas direcciones: archivos cifrados por Python abiertos en el gestor C#, y archivos guardados por `SecureCredentials.Manager` descifrados con Python, utilizando la misma clave y los mismos parámetros criptográficos.

## 2. Requisitos

- Python 3.
- Paquete Python `cryptography`.
- Una clave AES-256 de **32 bytes** que coincida con la utilizada por la implementación C#.
- Identificador de clave (`KEY_ID`) coherente con el documento y con C#.

En un entorno virtual recomendado:

```bash
python -m venv .venv
source .venv/bin/activate
python -m pip install cryptography
```

Comprobar la ayuda:

```bash
python securecredentials_interop.py --help
python securecredentials_interop.py encrypt --help
python securecredentials_interop.py decrypt --help
```

## 3. Configuración de la clave

El script distribuido como ejemplo contiene:

```python
KEY_BASE64 = "XXXX"
KEY_ID = "embedded-v1"
```

`XXXX` **no es una clave válida**. Para una prueba local, el valor de `KEY_BASE64` debe ser Base64 de una clave AES de exactamente 32 bytes y debe coincidir con la clave utilizada por la aplicación C#. `KEY_ID` debe corresponder al identificador esperado, pero **no es la clave**.

**Seguridad:** no subir el script modificado con una clave real a Git, no enviar la clave por correo o chat y no pegarla en incidencias. Este diseño con clave integrada es una herramienta de laboratorio, no un mecanismo recomendado de distribución o custodia de secretos.

## 4. Cifrar un archivo

Preparar un archivo de prueba `entrada.json` con datos ficticios:

```json
{
  "servidor": "localhost",
  "usuario": "usuario_prueba",
  "entorno": "desarrollo"
}
```

Ejecutar:

```bash
python securecredentials_interop.py encrypt entrada.json salida.enc
```

Resultado esperado: un archivo nuevo `salida.enc` que contiene un **sobre JSON cifrado**, con campos `Version`, `Algorithm`, `KeyId`, `Nonce`, `Ciphertext` y `Tag`. El script imprime la ruta, el identificador, el algoritmo y la versión; no imprime el contenido cifrado ni la clave.

En Linux, la creación solicita permisos `600` para el archivo de salida. La carpeta de destino debe estar previamente preparada y protegida.

## 5. Descifrar un archivo

```bash
python securecredentials_interop.py decrypt salida.enc recuperado.json
```

Si la clave, el `KeyId`, el AAD y la autenticación son correctos, se crea `recuperado.json` con el **texto original sin cifrar**.

Para comparar el original con el recuperado:

```bash
cmp entrada.json recuperado.json
```

`cmp` termina sin salida si los archivos son idénticos. Para JSON, también puede validarse su sintaxis mediante `python -m json.tool recuperado.json`, pero el script de cifrado no exige que el contenido sea JSON: únicamente UTF-8.

**Importante:** `recuperado.json` contiene texto claro. Evitar su exposición, limitar su conservación y eliminarlo cuando termine la prueba.

## 6. Reglas de funcionamiento

| Característica | Comportamiento |
|---|---|
| Formato admitido | Versión 2; el script no implementa descifrado v1 |
| Cifrado | AES-256-GCM, clave de 32 bytes |
| Nonce | 12 bytes aleatorios por operación |
| Tag | 16 bytes |
| AAD | Prefijo `SecureCredentials\|2\|AES-256-GCM\|` + longitud UTF-8 de `KeyId` en 4 bytes big-endian + bytes UTF-8 de `KeyId` |
| Entrada a cifrar | Archivo UTF-8 |
| Salida cifrada | JSON UTF-8 con campos binarios en Base64 |
| Salida descifrada | Texto UTF-8 sin cifrar |
| Sobrescritura | **No**: falla si el destino existe (`O_EXCL`) |
| Salida del programa | Código `0` si tiene éxito, `1` ante errores operativos; `argparse` usa su propio código para errores de argumentos |

Para el contrato exacto, consultar [FORMATO_ENC.md](FORMATO_ENC.md).

## 7. Compatibilidad con el gestor C#

**Python → C#:** cifrar un archivo con `encrypt`, abrir el `.enc` en `SecureCredentials.Manager` y verificar el texto mostrado.

**C# → Python:** guardar un documento `.enc` desde `SecureCredentials.Manager`, descifrarlo con `decrypt` y comprobar el texto resultante.

La interoperabilidad requiere **la misma clave AES**, `KeyId` coherente y el AAD del formato v2. La extensión del archivo por sí sola no garantiza compatibilidad.

## 8. Errores habituales

| Error | Motivo posible | Acción |
|---|---|---|
| `KEY_BASE64` inválido o clave de longitud incorrecta | Marcador `XXXX` sin sustituir o clave mal configurada | Revisar localmente que la clave Base64 represente 32 bytes |
| `FileExistsError` | Ya existe el archivo de salida | Usar otra ruta o gestionar explícitamente el archivo anterior |
| `UnicodeDecodeError` | Entrada o texto recuperado no válido en UTF-8 | Verificar codificación; no usar directamente con archivos binarios |
| `KeyId no reconocido` | Identificador diferente del configurado | Comprobar `KEY_ID` y el origen del documento |
| `Version no compatible` | Archivo distinto de v2 | Utilizar una implementación compatible con esa versión |
| `InvalidTag` | Clave incorrecta, datos alterados o AAD distinto | Verificar origen y parámetros sin divulgar claves |
| `PermissionError` | Carpeta o archivo sin acceso | Revisar permisos y propietario |

Los errores se muestran en `stderr`. No compartir volcados que puedan contener rutas sensibles sin revisarlos.

## 9. Limitaciones de seguridad

El script crea archivos nuevos con permisos solicitados `0600`, utiliza `O_EXCL` y añade `O_NOFOLLOW` cuando el sistema lo ofrece. **No realiza todas las verificaciones de propietario, componentes de ruta, escritura temporal atómica y `fsync` de la biblioteca C# en Linux.**

Además, si la escritura falla después de crear el archivo, puede quedar un destino parcial: no se implementa limpieza de temporales ni sustitución atómica. Por ello se recomienda utilizarlo para **pruebas controladas** y no como reemplazo directo del almacenamiento seguro de producción.

El script conserva una clave integrada en memoria y no incorpora rotación de claves, gestor de secretos ni limpieza garantizada de todos los buffers de Python.

## 10. Archivos relacionados

- [`securecredentials_interop.py`](securecredentials_interop.py): utilidad Python.
- [FORMATO_ENC.md](FORMATO_ENC.md): especificación v2.
- [README.md](README.md): presentación de SecureCredentials.
- [GUIA_ADMINISTRADOR.md](GUIA_ADMINISTRADOR.md): uso del gestor Avalonia y permisos Linux.
- [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md): arquitectura y API C#.
