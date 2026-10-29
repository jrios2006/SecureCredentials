# Guía de preparación de Windows para desarrollar SecureCredentials

**Proyecto:** SecureCredentials  
**Aplicación gráfica:** SecureCredentials.Manager 1.0.1  
**Biblioteca:** SecureCredentials 1.0.0  
**Framework objetivo:** .NET 10 (`net10.0`)  
**Plataforma de validación prevista:** Windows x64  
**Fecha:** 28 de octubre de 2026

## 1. Objetivo

Preparar y documentar un equipo Windows para clonar, compilar, probar y, posteriormente, publicar SecureCredentials. Esta guía **no certifica todavía** el funcionamiento de la biblioteca ni del Manager en Windows: esa validación se hará en una fase posterior.

La preparación debe hacerse con una cuenta de usuario normal siempre que sea posible. La instalación de herramientas puede requerir permisos de administrador, dependiendo de las políticas del equipo.

## 2. Inventario inicial: no instalar nada todavía

Abrir **PowerShell** y ejecutar los siguientes comandos. Guardar sus resultados para el informe de validación de Windows.

### 2.1. Versión de Windows y arquitectura

```powershell
Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion, OsBuildNumber, OsArchitecture
[System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
$PSVersionTable.PSVersion
```

La publicación prevista es `win-x64`, por lo que esperamos arquitectura **X64**. Si el equipo es ARM64, detenerse y decidir si publicar `win-arm64` o probar la compatibilidad de `win-x64` bajo emulación.

### 2.2. .NET: SDK y runtimes existentes

```powershell
Get-Command dotnet -ErrorAction SilentlyContinue
where.exe dotnet
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

Si `dotnet` no existe, los últimos cuatro comandos pueden fallar: eso significa que todavía no está disponible en `PATH`, no necesariamente que no haya ningún archivo de .NET en el equipo.

**Cómo interpretar los resultados:**

- `dotnet --list-sdks` muestra los **SDK instalados**, necesarios para compilar.
- `dotnet --list-runtimes` muestra los **runtimes instalados**, útiles para ejecutar aplicaciones dependientes del runtime.
- `dotnet --info` muestra arquitectura, rutas, versiones y entorno.
- Tener instalado únicamente un runtime de .NET 10 **no basta para compilar**.
- Si hay SDK 8 o 9, **no sustituyen al SDK 10** para este proyecto.
- Si ya existe un SDK 10 adecuado, no es necesario reinstalarlo.

Si hay un archivo `global.json` en la raíz del repositorio, habrá que comprobar si fija una versión específica del SDK o una política de selección (`rollForward`).

### 2.3. Git

```powershell
Get-Command git -ErrorAction SilentlyContinue
git --version
```

Si Git no está disponible, se instalará antes de clonar el repositorio.

### 2.4. Herramientas opcionales

```powershell
Get-Command code -ErrorAction SilentlyContinue
Get-Command winget -ErrorAction SilentlyContinue
```

Visual Studio Code, Visual Studio y `winget` son **opcionales**. Para compilar y probar la solución bastan PowerShell, Git y .NET SDK 10.

## 3. Instalación de los requisitos que falten

### 3.1. Instalar .NET SDK 10

Descargar el **SDK de .NET 10 para Windows x64**, no solo el runtime, desde:

https://dotnet.microsoft.com/download/dotnet/10.0

Seguir el instalador oficial. Si ya existe una versión adecuada del SDK 10, conservarla y evitar instalaciones duplicadas innecesarias.

Después, **cerrar y volver a abrir PowerShell** y comprobar:

```powershell
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

La lista de SDK debe contener una entrada `10.0.xxx` compatible con el proyecto. El SDK suele instalar también runtimes, pero hay que comprobarlo en lugar de darlo por supuesto.

### 3.2. Instalar Git para Windows

Descargar Git para Windows desde:

https://git-scm.com/downloads/win

Después de instalar, abrir una nueva consola y comprobar:

```powershell
git --version
```

Configurar la identidad de Git **solo si se van a realizar commits desde Windows**:

```powershell
git config --global user.name "NOMBRE A MOSTRAR"
git config --global user.email "CORREO_DEL_AUTOR"
```

No introducir contraseñas ni tokens de GitHub en archivos del repositorio. Para un repositorio privado, utilizar el método de autenticación aprobado por el equipo (por ejemplo, Git Credential Manager o SSH).

### 3.3. Editor de código (opcional)

Se puede usar Visual Studio Code con la extensión C# o Visual Studio compatible con .NET 10. No es requisito para la compilación por terminal.

## 4. Preparar la carpeta de trabajo

Una vez creado y publicado el repositorio remoto, abrir PowerShell:

```powershell
New-Item -ItemType Directory -Force C:\Proyectos | Out-Null
Set-Location C:\Proyectos
git clone https://github.com/TU_USUARIO/SecureCredentials.git
Set-Location .\SecureCredentials
git status
```

Sustituir `TU_USUARIO` por el propietario real del repositorio. Si es privado, Git solicitará autenticación por el mecanismo configurado.

Antes de compilar, comprobar que están presentes `SecureCredentials.slnx`, los archivos `.csproj` y la documentación. **No copiar al repositorio claves reales, contraseñas ni archivos `.enc` con datos sensibles.**

## 5. Primera compilación y pruebas en Windows

Desde la raíz del repositorio:

```powershell
dotnet restore .\SecureCredentials.slnx
dotnet build .\SecureCredentials.slnx -c Release
dotnet test .\SecureCredentials.slnx -c Release
```

Registrar el resultado de cada comando, la cantidad de pruebas ejecutadas y los errores o pruebas omitidas.

**Referencia Linux:** 106 pruebas automatizadas superadas. No asumir que las 106 son necesariamente aplicables a Windows: algunas pueden depender de servicios o permisos específicos de Linux.

Si falla alguna prueba, guardar la salida completa y clasificarla antes de modificar código.

## 6. Primera ejecución de la GUI desde el código

Si la compilación y la restauración terminan correctamente:

```powershell
dotnet run --project .\SecureCredentials.Manager\SecureCredentials.Manager.csproj -c Release
```

Verificar que se abre la ventana y que la sección «Acerca de» muestra la versión esperada. **No considerar validada la GUI** hasta completar pruebas de edición, teclado, cifrado, descifrado y errores.

El lanzador Bash `securecredentials-manager.sh` es una solución de compatibilidad de teclado para Linux/X11 y **no debe ejecutarse en Windows**.

## 7. Publicación portable Windows x64 (fase posterior)

Solo después de completar la preparación y las pruebas iniciales:

```powershell
dotnet publish .\SecureCredentials.Manager\SecureCredentials.Manager.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o .\dist\manager\win-x64
```

Ejecutar:

```powershell
.\dist\manager\win-x64\SecureCredentials.Manager.exe
```

La publicación `self-contained` incluye el runtime de .NET necesario, pero **no garantiza por sí sola** que todas las dependencias nativas, el proveedor de claves y las operaciones de almacenamiento funcionen en Windows. Eso debe comprobarse.

## 8. Aspectos específicos que habrá que validar

1. **Proveedor de claves:** `LinuxSecretServiceKeyProvider` es específico de Linux. Identificar qué proveedor utiliza el Manager en Windows y si es adecuado para ese sistema; no asumir compatibilidad automática.
2. **Almacenamiento:** comprobar permisos, rutas y operaciones de creación/lectura de archivos `.enc` en Windows.
3. **Interoperabilidad:** comprobar lectura de archivos `.enc` v2 creados en Linux y viceversa, con una **clave de prueba compartida de forma segura**.
4. **Interfaz:** probar acentos, Unicode, tamaño de fuente inicial 16, botones A+/A− y atajos de teclado.
5. **Biblioteca:** validar el paquete NuGet 1.0.0 desde un proyecto consumidor independiente en Windows.
6. **Empaquetado:** verificar el `.exe` publicado, sus archivos auxiliares y las sumas SHA-256 del entregable Windows.

## 9. Registro de preparación del equipo

Completar después de ejecutar los comandos del apartado 2:

| Campo | Valor observado |
|---|---|
| Nombre del equipo | Pendiente |
| Edición y versión de Windows | Pendiente |
| Compilación de Windows (build) | Pendiente |
| Arquitectura | Pendiente |
| PowerShell | Pendiente |
| .NET SDK instalados | Pendiente |
| Runtimes .NET instalados | Pendiente |
| Git | Pendiente |
| Editor/IDE (si procede) | Pendiente |
| Repositorio clonado | Pendiente |
| `dotnet restore` | Pendiente |
| `dotnet build -c Release` | Pendiente |
| `dotnet test -c Release` | Pendiente |
| Primera apertura del Manager | Pendiente |

## 10. Criterio para dar el equipo por preparado

El entorno de desarrollo Windows estará **preparado** cuando:

- Sea compatible con el objetivo de arquitectura elegido.
- `dotnet --info` confirme un SDK 10 adecuado.
- Git esté operativo y el repositorio pueda clonarse.
- La restauración y compilación de la solución terminen correctamente.
- Las pruebas puedan ejecutarse y sus resultados queden registrados, incluso si requieren investigar incidencias.

La **validación funcional del producto en Windows** será un hito independiente y se documentará posteriormente en `VALIDACION_WINDOWS_1.0.1.md`.
