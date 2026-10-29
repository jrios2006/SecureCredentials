#!/usr/bin/env bash
# Lanzador alternativo para sistemas Linux con problemas de teclas muertas
# en Avalonia/X11/IBus. No modifica la configuracion global del usuario.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
export XMODIFIERS='@im=none'
export GTK_IM_MODULE='xim'
export QT_IM_MODULE='xim'
exec "$SCRIPT_DIR/SecureCredentials.Manager" "$@"
