# Guía de administración de SecureCredentials

**Proyecto:** SecureCredentials / SecureCredentials.Manager\
**Plataformas con validación funcional:** Linux (Arch Linux y derivados) y Windows x64; las garantías de almacenamiento seguro documentadas corresponden a Linux\
**Tecnología:** .NET 10, C#, Avalonia\
**Estado:** guía operativa actualizada con las pruebas del 29 de octubre de 2026.

> Esta guía describe el comportamiento conocido y probado. No equivale a
> una auditoría de seguridad ni certifica el funcionamiento en Windows.

## 1. Objetivo y destinatarios

Esta guía está dirigida a quienes preparan, distribuyen, editan o
mantienen documentos cifrados mediante **SecureCredentials.Manager** y a
quienes administran las aplicaciones que los consumen.

La biblioteca `SecureCredentials` cifra y descifra texto UTF-8 mediante
AES-256-GCM. El gestor gráfico permite abrir documentos `.enc`,
modificar el contenido descifrado y guardar una nueva versión cifrada.
**El gestor no interpreta el significado del texto:** puede contener
JSON, XML u otro texto.

La especificación del formato está en [FORMATO_ENC.md](FORMATO_ENC.md).
Para la compilación y API, consultar [README.md](README.md) y
[DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md).

## 2. Componentes y responsabilidades

  -----------------------------------------------------------------------
  Componente                          Responsabilidad
  ----------------------------------- -----------------------------------
  `SecureCredentials`                 Cifrado, descifrado, proveedores de
                                      claves y almacenamiento seguro

  `SecureCredentials.Manager`         Interfaz Avalonia para trabajar con
                                      documentos cifrados

  `SecureCredentials.Demo`            Ejemplo de integración

  `SecureCredentials.Tests`           Pruebas automatizadas y de
                                      seguridad

  Script Python de interoperabilidad  Herramienta de pruebas; no forma
                                      parte del funcionamiento ordinario
                                      del gestor
  -----------------------------------------------------------------------

**Separación de funciones:** quien administra el documento decide su
contenido, ubicación y distribución; quien desarrolla la aplicación
consumidora decide cómo interpretar ese contenido. Un `.enc` no contiene
la clave AES.

## 3. Requisitos y puesta en marcha

Se necesita el SDK de **.NET 10** para compilar y ejecutar el proyecto
desde su código fuente. Las garantías de acceso a archivos descritas en
este manual corresponden a Linux.

Desde la raíz de la solución:

``` bash
dotnet restore SecureCredentials.slnx
dotnet build SecureCredentials.slnx
dotnet run --project SecureCredentials.Manager
```

Para verificar la solución:

``` bash
dotnet test SecureCredentials.slnx
```

No es necesario instalar Python para utilizar la aplicación gráfica.
Python 3 y `cryptography` solo son necesarios para repetir las pruebas
de interoperabilidad con el script correspondiente.

## 4. Operación diaria del gestor

### 4.1. Abrir un documento cifrado

1.  Iniciar `SecureCredentials.Manager`.
2.  Utilizar la función de apertura de documentos y seleccionar el
    archivo `.enc`.
3.  Comprobar que la aplicación puede descifrarlo y muestra su
    contenido.
4.  Revisar el texto antes de modificarlo.

Para abrir correctamente el documento, la aplicación debe disponer de la
**misma clave** utilizada al cifrarlo. Además, el sobre debe ser válido
y superar las comprobaciones de integridad y de almacenamiento.

### 4.2. Editar y guardar

1.  Modificar el texto en el editor.
2.  Revisar especialmente los campos sensibles y la sintaxis, si el
    contenido es JSON o XML.
3.  Guardar el documento cifrado en una ubicación autorizada.
4.  Cuando el cambio sea importante, volver a abrirlo y comprobar que el
    contenido es el esperado.

**Atención:** el gestor cifra texto, pero no garantiza que un JSON de
configuración sea válido para la aplicación que lo consumirá. Esa
validación corresponde al sistema de destino.

### 4.3. Precauciones al manejar texto descifrado

-   Evitar copiar secretos al portapapeles, terminal, chats, incidencias
    o registros.
-   No guardar versiones en claro junto a los archivos `.enc`.
-   Cerrar el editor cuando termine la operación.
-   Distribuir exclusivamente el documento cifrado y solo a los
    destinatarios autorizados.
-   Verificar que el destinatario utiliza una implementación compatible
    y dispone de la clave apropiada.

### 4.4. Tamaño de letra y atajos

Los botones **A+** y **A−** aumentan y reducen el tamaño de letra del editor. En Windows y Linux se han verificado `Ctrl + +` para aumentar y `Ctrl + -` para reducir. Al dejar el puntero sobre los botones aparecen etiquetas explicativas. El historial del proyecto también menciona `Ctrl + 0` para restablecer el tamaño, pero no consta su verificación específica en Windows.

## 5. Formato de los documentos `.enc`

El formato actual es **v2**, serializado como JSON UTF-8. Su estructura
contiene:

``` json
{
  "Version": 2,
  "Algorithm": "AES-256-GCM",
  "KeyId": "embedded-v1",
  "Nonce": "<base64>",
  "Ciphertext": "<base64>",
  "Tag": "<base64>"
}
```

Los valores anteriores son **ilustrativos**, no constituyen un documento
descifrable.

  Propiedad                   Regla
  --------------------------- ---------------------------------------------
  Clave AES                   32 bytes
  Nonce                       12 bytes, nuevo en cada cifrado
  Etiqueta de autenticación   16 bytes
  `KeyId`                     Identificador de clave, no material secreto
  AAD                         Autentica los metadatos del formato v2
  Texto original              UTF-8

El algoritmo, la construcción exacta del AAD y las reglas de
interoperabilidad se describen en [FORMATO_ENC.md](FORMATO_ENC.md). **No
modificar manualmente los campos criptográficos del sobre.** Una
alteración puede impedir su autenticación.

## 6. Permisos y propietarios en Linux

La política diferencia **directorios privados de claves** de
**directorios que contienen documentos cifrados**.

  -----------------------------------------------------------------------
  Elemento                            Política
  ----------------------------------- -----------------------------------
  Directorio privado de claves        Propietario: usuario efectivo;
                                      permisos exactos `700`

  Directorio final de documentos      Propiedad del usuario efectivo; no
  `.enc`                              puede ser escribible por grupo ni
                                      por otros

  Archivo `.enc`                      Archivo regular, propiedad del
                                      usuario efectivo, permisos `600`

  Enlaces simbólicos                  Rechazados en las operaciones
                                      protegidas

  Escritura                           Temporal seguro, sustitución del
                                      destino, sincronización y limpieza
                                      ante errores
  -----------------------------------------------------------------------

Por tanto, una carpeta propia con permisos `755` puede alojar documentos
cifrados; una carpeta `777` se rechaza. No es necesario convertir todo
`~/Proyectos` a `700`.

### 6.1. Consultar permisos

``` bash
stat -c '%a %U:%G %n' /ruta/a/la/carpeta
stat -c '%a %U:%G %n' /ruta/a/la/carpeta/documento.enc
namei -l /ruta/a/la/carpeta/documento.enc
```

El comando `namei` ayuda a detectar componentes de ruta que sean enlaces
simbólicos o presenten problemas de acceso.

### 6.2. Corregir permisos cuando corresponda

Para un **archivo `.enc` propio**:

``` bash
chmod 600 /ruta/a/documento.enc
```

Para un **directorio privado de claves propio**:

``` bash
chmod 700 /ruta/a/directorio-privado
```

Para una **carpeta de documentos propia** que no necesite escritura
compartida:

``` bash
chmod 700 /ruta/a/carpeta-documentos
```

Los comandos son ejemplos: verificar previamente propietario, propósito
y política del sistema. **No ejecutar `chmod -R` indiscriminadamente**,
no cambiar propietarios para eludir una validación y no modificar
permisos de directorios compartidos sin autorización.

El gestor no debe alterar los permisos de carpetas existentes
simplemente para permitir guardar un archivo. Si la ruta no cumple la
política, elegir otra ubicación o corregirla de manera consciente.

### 6.3. Restricciones adicionales

La implementación Linux utiliza operaciones POSIX basadas en
descriptores, comprobación de propietarios y medidas para evitar seguir
enlaces simbólicos. Los archivos cifrados tienen un límite de tamaño de
**1 MiB** según la implementación documentada.

Estas protecciones no impiden que un usuario con acceso a la clave y al
proceso obtenga el texto descifrado.

## 7. Configuración y registros del gestor

  --------------------------------------------------------------------------------------------
  Ruta                                                     Finalidad
  -------------------------------------------------------- -----------------------------------
  `~/.config/securecredentials-manager/settings.json`      Preferencias locales, incluida la
                                                           última carpeta utilizada

  `~/.local/state/securecredentials-manager/manager.log`   Registro de actividad y diagnóstico
  --------------------------------------------------------------------------------------------

En **Windows x64**, se ha confirmado el registro en `%USERPROFILE%\.local\state\securecredentials-manager\manager.log` (por ejemplo, `C:\Users\jrios\.local\state\securecredentials-manager\manager.log`). No se debe confundir con `%LOCALAPPDATA%`.

Para consultarlo desde PowerShell:

```powershell
Get-Content "$env:USERPROFILE\.local\state\securecredentials-manager\manager.log" -Tail 100
```

Para inspeccionar los archivos en Linux:

``` bash
cat ~/.config/securecredentials-manager/settings.json
tail -n 100 ~/.local/state/securecredentials-manager/manager.log
```

Para seguir el registro durante una incidencia:

``` bash
tail -f ~/.local/state/securecredentials-manager/manager.log
```

**No compartir el registro sin revisarlo antes.** Aunque no debería
contener claves ni texto descifrado, puede incluir rutas, nombres de
archivos u otros datos de diagnóstico. Estos archivos no son un almacén
de claves.

Si la última carpeta guardada ya no existe, seleccionar una ruta válida
desde la aplicación. No editar `settings.json` mientras el gestor esté
abierto, salvo que se conozca su comportamiento de persistencia.

## 8. Gestión de claves y distribución

La solución dispone de proveedores de claves, entre ellos:

-   `EmbeddedKeyProvider`: clave integrada en el código/binario.
-   `DevelopmentKeyProvider`: clave temporal para desarrollo y pruebas.
-   `LinuxSecretServiceKeyProvider`: integración con el servicio de
    secretos de Linux.

Las operaciones de alto nivel descritas en el proyecto utilizan
actualmente el proveedor integrado. **Una clave embebida puede extraerse
mediante ingeniería inversa**: no es una defensa suficiente contra quien
controla el binario o tiene acceso al entorno de ejecución.

Reglas de administración:

1.  No incluir la clave real en el repositorio, documentación,
    incidencias ni scripts distribuidos.
2.  No asumir que `KeyId` permite recuperar una clave; únicamente la
    identifica.
3.  Coordinar la clave entre quien crea y quien consume el documento.
4.  Mantener un inventario de los documentos afectados antes de
    sustituir una clave.
5.  Planificar cualquier rotación como una operación de **descifrado con
    la clave anterior y recifrado con la nueva**, con pruebas y copia de
    seguridad segura. La automatización de este procedimiento no está
    documentada como funcionalidad implementada.
6.  No utilizar `DevelopmentKeyProvider` como mecanismo de persistencia
    de credenciales de producción.

## 9. Copias de seguridad y recuperación

Antes de editar documentos importantes, establecer una copia de
seguridad cifrada y con acceso restringido. No generar copias en claro
como procedimiento habitual.

Ejemplo para un archivo propio:

``` bash
cp -- /ruta/documento.enc /ruta/documento.enc.bak
chmod 600 /ruta/documento.enc.bak
```

**Precaución:** el ejemplo supone una carpeta segura, controlada por el
mismo usuario y compatible con la política de almacenamiento. Verificar
el resultado antes de confiar en la copia. El sufijo `.bak` no cambia la
naturaleza cifrada del contenido.

Para recuperar, conservar primero el archivo problemático para
diagnóstico cuando la política de datos lo permita, restaurar la copia
cifrada y comprobar que puede descifrarse con la clave esperada. Si se
ha perdido la clave AES, disponer del `.enc` no permite recuperar el
texto original.

## 10. Resolución de problemas

  -----------------------------------------------------------------------
  Síntoma                 Posibles causas         Comprobación
                                                  recomendada
  ----------------------- ----------------------- -----------------------
  No abre un `.enc`       Clave distinta,         Verificar origen,
                          documento alterado,     clave, formato y `stat`
                          versión no admitida o   
                          permisos                

  Rechaza archivo `644`   El archivo no cumple la Revisar propietario y
                          política `600`          permisos; corregir solo
                                                  si procede

  No permite guardar en   Directorio escribible   Seleccionar carpeta
  `777`                   por otros               propia y segura

  Rechaza un enlace       Protección frente a     Usar una ruta real
  simbólico               redirección de rutas    autorizada

  No guarda el documento  Permisos, propietario,  Revisar registro y
                          límite de tamaño, E/S o espacio libre; no
                          ruta no válida          publicar contenido
                                                  sensible

  No encuentra la última  Ruta movida, eliminada  Seleccionar otra
  carpeta                 o no accesible          carpeta

  Python no descifra un   Clave, `KeyId`, AAD o   Comparar con
  archivo C#              versión incompatibles   `FORMATO_ENC.md` sin
                                                  divulgar claves

  El documento se         Contenido               Validar JSON/XML y
  descifra pero la        semánticamente          reglas de la aplicación
  aplicación falla        incorrecto              consumidora
  -----------------------------------------------------------------------

Para revisar espacio y permisos:

``` bash
df -h /ruta/a/la/carpeta
stat -c '%a %U:%G %n' /ruta/a/la/carpeta
```

Si aparece una excepción, anotar la operación, ruta (sin datos
sensibles), versión del proyecto y mensaje de error. No adjuntar el
texto descifrado ni material de claves.

## 11. Pruebas y comprobaciones de mantenimiento

Desde la raíz del repositorio:

``` bash
dotnet build SecureCredentials.slnx
dotnet test SecureCredentials.slnx
```

Pruebas específicas de almacenamiento:

``` bash
dotnet test SecureCredentials.slnx \
    --filter FullyQualifiedName~CredentialStorageSecurityTests \
    --logger "console;verbosity=normal"
```

**Resultado comunicado el 28/10/2026:**

-   **106 pruebas totales correctas**, 0 errores y 0 omitidas.
-   **7 pruebas específicas de seguridad correctas**, sin advertencias
    de compilación en esa ejecución.
-   Escenarios cubiertos: enlaces simbólicos, archivo `644`, directorio
    `777`, directorio privado `700`, limpieza ante fallo de validación y
    limpieza/cierre de descriptores ante fallo POSIX simulado.
-   Interoperabilidad probada en ambas direcciones: Python → C# y C# →
    Python.

Estas pruebas acreditan los escenarios ensayados, no una auditoría
integral ni compatibilidad de seguridad equivalente en Windows.

## 12. Lista de verificación para distribuir un documento

-   [ ] El contenido ha sido revisado y no incluye información
    accidental.
-   [ ] El documento está cifrado en formato v2 y se abre correctamente.
-   [ ] La aplicación de destino utiliza la clave correspondiente.
-   [ ] La carpeta y el archivo cumplen las políticas de propiedad y
    permisos.
-   [ ] No se distribuyen archivos en claro ni scripts con claves.
-   [ ] Se dispone de copia de seguridad cifrada cuando es necesaria.
-   [ ] Se ha probado el funcionamiento de la aplicación consumidora.

## 13. Limitaciones y evolución pendiente

-   La política POSIX descrita se ha validado en **Linux**. Windows
    requiere implementación y pruebas adicionales.
-   El proveedor de clave embebida no resiste ingeniería inversa.
-   La rotación y custodia de claves necesitan un procedimiento
    operativo específico antes de un despliegue con mayores exigencias.
-   `SecureCredentials.Configuration`, encargado de interpretar
    configuraciones específicas de aplicaciones, es una evolución
    prevista y no debe confundirse con el gestor genérico.
-   La seguridad del documento cifrado no reemplaza los controles de
    acceso, copias de seguridad, endurecimiento del sistema y gestión de
    secretos del entorno.

## 14. Documentación relacionada

-   [README.md](README.md): descripción general, compilación y uso de la
    solución.
-   [FORMATO_ENC.md](FORMATO_ENC.md): especificación criptográfica del
    formato v2.
-   [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md): arquitectura y API de
    desarrollo.

------------------------------------------------------------------------

**Criterio de administración:** distribuir únicamente los documentos
cifrados necesarios, proteger la clave por separado y comprobar el
funcionamiento con la aplicación destinataria.
