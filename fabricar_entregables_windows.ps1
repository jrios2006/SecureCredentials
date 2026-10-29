#Requires -Version 5.1
<#
.SYNOPSIS
  Fabrica el entregable Windows x64 de SecureCredentials.Manager.
.DESCRIPTION
  Restaura, compila, ejecuta pruebas, publica una GUI autocontenida,
  crea un ZIP y genera un archivo SHA256SUMS. No modifica versiones.
.EXAMPLE
  .\fabricar_entregables_windows.ps1
.EXAMPLE
  .\fabricar_entregables_windows.ps1 -ManagerVersion 1.0.1 -RuntimeIdentifier win-x64
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string]$ManagerVersion = '1.0.1',

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier = 'win-x64',

    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$solution = Join-Path $root 'SecureCredentials.slnx'
$managerProject = Join-Path $root 'SecureCredentials.Manager\SecureCredentials.Manager.csproj'
$dist = Join-Path $root 'dist'
$packageDir = Join-Path $dist 'paquetes'
$publishDir = Join-Path $dist ('publish\manager-' + $RuntimeIdentifier)
$zipName = 'SecureCredentials.Manager-{0}-{1}.zip' -f $ManagerVersion, $RuntimeIdentifier
$zipPath = Join-Path $packageDir $zipName
$checksumPath = Join-Path $packageDir ('SHA256SUMS-' + $RuntimeIdentifier + '.txt')

function Invoke-Dotnet {
    param([Parameter(Mandatory=$true)][string[]]$Arguments)
    Write-Host ('> dotnet ' + ($Arguments -join ' ')) -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ('dotnet devolvio codigo ' + $LASTEXITCODE + ': ' + ($Arguments -join ' '))
    }
}

try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'No se encuentra dotnet en PATH. Instale .NET SDK 10.'
    }
    if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
        throw ('No se encuentra la solucion: ' + $solution)
    }
    if (-not (Test-Path -LiteralPath $managerProject -PathType Leaf)) {
        throw ('No se encuentra el proyecto GUI: ' + $managerProject)
    }

    $sdkVersion = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.') {
        throw ('Se requiere un SDK .NET 10 activo; detectado: ' + $sdkVersion)
    }

    Write-Host ('SecureCredentials.Manager ' + $ManagerVersion + ' - ' + $RuntimeIdentifier) -ForegroundColor Green
    Write-Host ('SDK: ' + $sdkVersion)
    Write-Host ('Raiz: ' + $root)

    Invoke-Dotnet -Arguments @('restore', $solution)
    Invoke-Dotnet -Arguments @('build', $solution, '-c', 'Release', '--no-restore')

    if (-not $SkipTests) {
        Invoke-Dotnet -Arguments @('test', $solution, '-c', 'Release', '--no-build')
    } else {
        Write-Warning 'Se omiten pruebas por solicitud explicita. Este paquete NO queda validado por este proceso.'
    }

    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

    Invoke-Dotnet -Arguments @(
        'publish', $managerProject,
        '-c', 'Release',
        '-r', $RuntimeIdentifier,
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:PublishTrimmed=false',
        '-o', $publishDir
    )

    $exe = Join-Path $publishDir 'SecureCredentials.Manager.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        throw ('La publicacion no genero el ejecutable esperado: ' + $exe)
    }

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    $contents = @(Get-ChildItem -LiteralPath $publishDir -Force)
    if ($contents.Count -eq 0) {
        throw 'El directorio de publicacion esta vacio.'
    }
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force

    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    # Formato compatible con sha256sum: HASH, dos espacios, nombre de archivo.
    Set-Content -LiteralPath $checksumPath -Value ($hash + '  ' + $zipName) -Encoding ASCII

    Write-Host ''
    Write-Host 'Fabricacion finalizada correctamente.' -ForegroundColor Green
    Write-Host ('ZIP: ' + $zipPath)
    Write-Host ('SHA256: ' + $checksumPath)
    Write-Host ('Hash: ' + $hash)
    Write-Host 'NOTA: Pruebe el ZIP extraido en una carpeta independiente y, preferiblemente, en otro Windows.'
}
catch {
    Write-Error ('Fabricacion fallida: ' + $_.Exception.Message)
    exit 1
}
