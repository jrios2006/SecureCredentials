#!/usr/bin/env bash
# SecureCredentials: fabrica Manager portable y biblioteca NuGet.
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Permite colocar este script en scripts/ o en la raiz del repositorio.
if [[ ! -f "$ROOT/SecureCredentials.slnx" && -f "$ROOT/../SecureCredentials.slnx" ]]; then ROOT="$(cd "$ROOT/.." && pwd)"; fi
cd "$ROOT"

RID="${1:-linux-x64}"
case "$RID" in linux-x64|linux-arm64|win-x64) ;; *) echo "RID no soportado: $RID" >&2; exit 2;; esac

LIB="SecureCredentials/SecureCredentials.csproj"
MANAGER="SecureCredentials.Manager/SecureCredentials.Manager.csproj"
SOLUTION="SecureCredentials.slnx"
for file in "$LIB" "$MANAGER" "$SOLUTION"; do
  [[ -f "$file" ]] || { echo "Falta: $ROOT/$file" >&2; exit 2; }
done
command -v dotnet >/dev/null || { echo "Falta el SDK dotnet" >&2; exit 2; }
command -v tar >/dev/null || { echo "Falta tar" >&2; exit 2; }
command -v sha256sum >/dev/null || { echo "Falta sha256sum" >&2; exit 2; }

get_version() { dotnet msbuild "$1" -getProperty:Version -nologo | tr -d '\r' | tail -n 1; }
LIB_VERSION="$(get_version "$LIB")"
MANAGER_VERSION="$(get_version "$MANAGER")"
for v in "$LIB_VERSION" "$MANAGER_VERSION"; do
  [[ "$v" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-][A-Za-z0-9.-]+)?$ ]] || {
    echo "Version no valida o ausente: '$v'. Configura <Version>1.0.0</Version> en ambos csproj." >&2
    exit 2
  }
done

DIST="$ROOT/dist"
NUGET="$DIST/nuget"
PUBLISH="$DIST/manager/$RID"
PACKAGES="$DIST/paquetes"
mkdir -p "$NUGET" "$PACKAGES"
# Evitar archivos de versiones anteriores mezclados en el paquete portable.
rm -rf "$PUBLISH"
mkdir -p "$PUBLISH"

echo "==> Compilando y ejecutando pruebas Release"
dotnet restore "$SOLUTION"
dotnet build "$SOLUTION" -c Release --no-restore
dotnet test "$SOLUTION" -c Release --no-build

echo "==> Generando biblioteca NuGet $LIB_VERSION"
dotnet pack "$LIB" -c Release --no-restore -p:PackageVersion="$LIB_VERSION" -o "$NUGET"
NUPKG="$NUGET/SecureCredentials.$LIB_VERSION.nupkg"
[[ -f "$NUPKG" ]] || { echo "No se ha generado $NUPKG" >&2; exit 1; }

echo "==> Publicando Manager $MANAGER_VERSION para $RID"
dotnet publish "$MANAGER" -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=false \
  -p:Version="$MANAGER_VERSION" \
  -o "$PUBLISH"

# Lanzador alternativo para el problema de teclas muertas observado en
# Cinnamon/X11 con IBus. Solo se incluye en las publicaciones Linux.
if [[ "$RID" == linux-* ]]; then
  LAUNCHER_SOURCE="$ROOT/securecredentials-manager.sh"
  if [[ ! -f "$LAUNCHER_SOURCE" ]]; then
    LAUNCHER_SOURCE="$(dirname "${BASH_SOURCE[0]}")/securecredentials-manager.sh"
  fi
  [[ -f "$LAUNCHER_SOURCE" ]] || {
    echo "Falta securecredentials-manager.sh junto al script o en la raiz." >&2
    exit 2
  }
  cp "$LAUNCHER_SOURCE" "$PUBLISH/securecredentials-manager.sh"
  chmod 755 "$PUBLISH/securecredentials-manager.sh"
fi

ARCHIVE="$PACKAGES/SecureCredentials.Manager-$MANAGER_VERSION-$RID.tar.gz"
tar -czf "$ARCHIVE" -C "$PUBLISH" .
CHECKSUMS="$PACKAGES/SHA256SUMS-$RID.txt"
sha256sum "$ARCHIVE" "$NUPKG" > "$CHECKSUMS"

echo
echo "ENTREGABLES GENERADOS:"
echo "  Manager: $ARCHIVE"
echo "  NuGet:   $NUPKG"
echo "  SHA256:  $CHECKSUMS"
echo "  Versiones: Manager $MANAGER_VERSION / Biblioteca $LIB_VERSION"
echo "Nota: validar el Manager en una maquina limpia del sistema destino."
