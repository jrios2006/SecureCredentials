# Guía de fabricación de entregables — SecureCredentials

## 1. Objetivo

Generar **dos entregables independientes**, con versiones propias:

1. `SecureCredentials.Manager` — aplicación Avalonia portable, autocontenida, para Linux (o Windows si se publica con su RID).
2. `SecureCredentials` — biblioteca .NET 10 en paquete `.nupkg`, para incorporar mediante NuGet.

El gestor depende de la biblioteca, pero el proyecto que consuma la biblioteca **no necesita instalar el gestor**. La versión del software (`1.0.0`) es distinta de la versión del formato `.enc` (`2`).

## 2. Estado y alcance de esta entrega

Este paquete contiene **el script de fabricación y esta guía**, no ejecutables compilados. Los archivos `.csproj` facilitados inicialmente usan `net10.0`, Avalonia 12.1.4 y Mono.Posix.NETStandard 1.0.0, pero la copia inicial no contiene los metadatos de versión que se añadieron posteriormente. Debes usar tus `.csproj` **actualizados**, con `Version=1.0.0` en cada proyecto. No se dispone aquí del repositorio completo ni del SDK `dotnet`, por lo que la compilación y las pruebas deben realizarse en tu equipo.

## 3. Requisitos

- SDK **.NET 10**, verificado con `dotnet --info`.
- Código completo y solución `SecureCredentials.slnx`.
- Proyectos `SecureCredentials/SecureCredentials.csproj` y `SecureCredentials.Manager/SecureCredentials.Manager.csproj`.
- Carpeta `Assets` del gestor, con el icono `securecredentials.ico` y los recursos necesarios para Avalonia.
- Archivos `AboutWindow.axaml` y `AboutWindow.axaml.cs`, correctamente incorporados al proyecto.
- Herramientas de Linux `bash`, `tar` y `sha256sum`.
- Acceso a dependencias NuGet durante la restauración inicial.

## 4. Versionado

Configurar en **cada proyecto**, con versiones que pueden evolucionar independientemente:

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
<InformationalVersion>1.0.0</InformationalVersion>
```

La biblioteca puede incluir también:

```xml
<PackageId>SecureCredentials</PackageId>
<IsPackable>true</IsPackable>
<Authors>Nombre o equipo responsable</Authors>
<Description>Biblioteca de cifrado AES-256-GCM para .NET.</Description>
```

En el gestor se recomienda `<IsPackable>false</IsPackable>`. La biblioteca conserva su dependencia `Mono.Posix.NETStandard`; el gestor conserva sus paquetes Avalonia y su `ProjectReference` a la biblioteca. **No reemplazar el `.csproj` por los fragmentos anteriores**: integrarlos en el archivo actual.

## 5. Preparación del script

Copiar `fabricar_entregables.sh` en la raíz del repositorio o en `scripts/`. Desde la raíz:

```bash
chmod +x fabricar_entregables.sh
./fabricar_entregables.sh linux-x64
```

Si está en `scripts/`:

```bash
chmod +x scripts/fabricar_entregables.sh
./scripts/fabricar_entregables.sh linux-x64
```

El script se detiene si falla cualquier etapa. Primero ejecuta restauración, compilación Release y pruebas; después empaqueta NuGet y publica el gestor.

## 6. Fabricación manual

### 6.1 Validación

```bash
dotnet restore SecureCredentials.slnx
dotnet build SecureCredentials.slnx -c Release --no-restore
dotnet test SecureCredentials.slnx -c Release --no-build
```

Revisar que el número de pruebas sea el esperado en la revisión actual y que no haya fallos. No asumir que `dotnet test` valida la aplicación gráfica en una máquina diferente.

### 6.2 Biblioteca NuGet

```bash
mkdir -p dist/nuget
dotnet pack SecureCredentials/SecureCredentials.csproj \
  -c Release \
  -p:PackageVersion=1.0.0 \
  -o dist/nuget
```

Resultado previsto: `dist/nuget/SecureCredentials.1.0.0.nupkg`.

Para consumir desde una carpeta local, copiar el `.nupkg` a `/ruta/nuget-local/` y ejecutar en el proyecto cliente:

```bash
dotnet add package SecureCredentials --version 1.0.0 --source /ruta/nuget-local
```

Comprobar la API pública desde una aplicación de prueba **independiente**. No distribuir la carpeta `bin/Release` como sustituto del paquete NuGet.

### 6.3 Gestor portable Linux x64

```bash
dotnet publish SecureCredentials.Manager/SecureCredentials.Manager.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=false \
  -o dist/manager/linux-x64

tar -czf dist/SecureCredentials.Manager-1.0.0-linux-x64.tar.gz \
  -C dist/manager/linux-x64 .
```

La carpeta publicada puede contener archivos adicionales aunque se use `PublishSingleFile`; **distribuir su contenido completo**. `--self-contained` incluye el runtime .NET, pero **no todas las dependencias nativas del sistema** (entorno gráfico, bibliotecas compartidas, etc.).

### 6.4 Otros sistemas

- `linux-arm64`: publicar en un entorno/SDK compatible con ese RID y probar en ARM64.
- `win-x64`: generar un paquete Windows y comprobar el icono, el manifiesto y las ACL. La seguridad de almacenamiento Linux no equivale automáticamente a protección Windows.

El script admite `linux-x64`, `linux-arm64` y `win-x64`, pero las pruebas de integración deben realizarse en el sistema de destino.

## 7. Estructura esperada

```text
dist/
├── manager/
│   └── linux-x64/
│       └── SecureCredentials.Manager  (más posibles recursos)
├── nuget/
│   └── SecureCredentials.1.0.0.nupkg
└── paquetes/
    ├── SecureCredentials.Manager-1.0.0-linux-x64.tar.gz
    └── SHA256SUMS-linux-x64.txt
```

## 8. Verificaciones de aceptación

1. Compilación Release correcta y pruebas superadas.
2. El gestor inicia en un equipo Linux x64 compatible **sin SDK ni runtime .NET instalados**.
3. Icono de ventana visible; `Acerca de` muestra versiones reales del gestor y la biblioteca.
4. Se puede crear, abrir y guardar un `.enc` de prueba; confirmar compatibilidad con la biblioteca.
5. El `.nupkg` se instala en un proyecto .NET 10 separado y sus API se utilizan correctamente.
6. El archivo `SHA256SUMS` permite comprobar integridad (`sha256sum -c ...` desde el entorno adecuado).
7. No se han incluido claves reales, secretos, `.enc` confidenciales, logs ni archivos de configuración privados en los paquetes.
8. Verificar que el proveedor de claves configurado en el gestor y en las aplicaciones consumidoras sea compatible. **El paquete NuGet no resuelve por sí solo la distribución segura de claves**.

## 9. Seguridad y límites

- `EmbeddedKeyProvider` es apropiado para laboratorio, no para distribución general de secretos. Antes de publicar para producción, establecer una estrategia de claves externa y autorizaciones.
- No incluir `KEY_BASE64` real ni `securecredentials_interop.py` con claves en el paquete de producción.
- Un `.enc` cifrado no puede descifrarse sin acceso a la clave correcta y su `KeyId` correspondiente.
- La aplicación gráfica requiere entorno de escritorio compatible con Avalonia; "portable" no significa que funcione en cualquier distribución Linux sin dependencias nativas.
- La publicación no equivale a instalador, firma digital ni paquete del sistema (`.deb`, `.rpm`, AppImage).

## 10. Mantenimiento

Para una corrección compatible, subir la versión del proyecto afectado a `1.0.1`, recompilar y generar nuevos artefactos. Mantener el historial de cambios en `CHANGELOG.md` y conservar los paquetes anteriores. Si solo cambia el gestor, la biblioteca puede continuar en `1.0.0`.

## 11. Entrada de caracteres acentuados en Linux: lanzador alternativo

En una prueba real con **Cinnamon sobre X11**, locale `es_ES.UTF-8` e IBus
(`XMODIFIERS=@im=ibus`), se observó que el editor admitía caracteres Unicode
pegados desde el portapapeles, pero no permitía introducir vocales acentuadas
mediante teclas muertas. Los caracteres directos `ñ`, `Ñ` y `€` sí funcionaban.

Se comprobó que la entrada de acentos funcionaba al iniciar el ejecutable con:

```bash
env XMODIFIERS=@im=none GTK_IM_MODULE=xim QT_IM_MODULE=xim \
  ./SecureCredentials.Manager
```

El kit incorpora `securecredentials-manager.sh`, que aplica **solo al proceso
del gestor** esas tres variables y ejecuta el binario ubicado en la misma
carpeta. No modifica la configuración del sistema ni del usuario.

### Fabricación con el lanzador

Colocar **ambos** scripts en la raíz del repositorio (o juntos en `scripts/`):

```text
SecureCredentials/
├── fabricar_entregables.sh
├── securecredentials-manager.sh
├── SecureCredentials.slnx
├── SecureCredentials/
└── SecureCredentials.Manager/
```

Ejecutar:

```bash
chmod +x fabricar_entregables.sh
./fabricar_entregables.sh linux-x64
```

El script actualizado, tras `dotnet publish`, copia el lanzador a
`dist/manager/linux-x64/`, le asigna permisos de ejecución, genera el `.tar.gz`
y recalcula las sumas SHA-256. **No modifica el `.nupkg`.** Para `win-x64` no
incluye el lanzador Bash.

### Ejecución del paquete extraído

```bash
mkdir -p /tmp/securecredentials-test

tar -xzf dist/paquetes/SecureCredentials.Manager-1.0.0-linux-x64.tar.gz \
  -C /tmp/securecredentials-test

/tmp/securecredentials-test/securecredentials-manager.sh
```

Probar escritura directa de `áéíóú ÁÉÍÓÚ ñÑ üÜ €`, validación de JSON,
guardado y reapertura de un `.enc`. También se puede ejecutar directamente
`/tmp/securecredentials-test/SecureCredentials.Manager` para respetar la
configuración de entrada original del equipo.

**Limitación:** este lanzador es una solución de compatibilidad verificada en
un entorno concreto; desactivar IBus puede afectar a otros métodos de entrada,
como los utilizados para lenguas con composición compleja. No debe considerarse
una corrección universal de Avalonia. En otros escritorios, probar ambos modos.

### Verificar que el lanzador está incluido

```bash
tar -tzf dist/paquetes/SecureCredentials.Manager-1.0.0-linux-x64.tar.gz \
  | grep 'securecredentials-manager.sh'

cd dist/paquetes
sha256sum -c SHA256SUMS-linux-x64.txt
```

Si se han cambiado archivos del paquete tras calcular sus sumas, volver a
fabricarlo para obtener un manifiesto coherente.
