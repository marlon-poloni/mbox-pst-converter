#!/usr/bin/env bash
# Gera o instalador do Windows a partir do Linux, sem sudo.
# Ferramentas extraídas de pacotes .deb do Ubuntu 24.04 em $XTOOLS (apt download + dpkg -x):
#   lld-18 liblld-18 clang-18 libclang-cpp18 libclang-common-18-dev libclang1-18 nsis nsis-common
# O makensis do Ubuntu tem /usr/share/nsis fixo; usamos uma cópia com esse caminho trocado por
# /tmp/nsis-share (mesmo tamanho), que aponta para os dados extraídos.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
XTOOLS="${XTOOLS:-$HOME/.local/opt/xtools}"
mkdir -p "$XTOOLS/nsis-bin"
if [ ! -x "$XTOOLS/nsis-bin/makensis" ]; then
  python3 - "$XTOOLS" <<'PY'
import sys
x = sys.argv[1]
data = open(f"{x}/root/usr/bin/makensis", "rb").read()
old, new = "/usr/share/nsis".encode("utf-32-le"), "/tmp/nsis-share".encode("utf-32-le")
assert data.count(old) == 1
open(f"{x}/nsis-bin/makensis", "wb").write(data.replace(old, new))
PY
  chmod +x "$XTOOLS/nsis-bin/makensis"
fi
ln -sfn "$XTOOLS/root/usr/share/nsis" /tmp/nsis-share
ln -sf "$XTOOLS/root/usr/lib/llvm-18/bin/clang" "$XTOOLS/root/usr/lib/llvm-18/bin/clang-cl"
export PATH="$XTOOLS/nsis-bin:$XTOOLS/root/usr/lib/llvm-18/bin:$XTOOLS/root/usr/bin:$PATH"
export LD_LIBRARY_PATH="$XTOOLS/root/usr/lib/llvm-18/lib:$XTOOLS/root/usr/lib/x86_64-linux-gnu"
export XWIN_ACCEPT_LICENSE=1
export CARGO_TARGET_DIR="$root/app/src-tauri/target"
"$root/scripts/build-sidecar.sh" win-x64
cd "$root/app"
npx tauri build --runner cargo-xwin --target x86_64-pc-windows-msvc --bundles nsis
