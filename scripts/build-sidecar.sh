#!/usr/bin/env bash
set -euo pipefail
rid="${1:?uso: build-sidecar.sh linux-x64|win-x64}"
root="$(cd "$(dirname "$0")/.." && pwd)"
command -v dotnet >/dev/null || export PATH="$HOME/.dotnet:$PATH"
case "$rid" in
  linux-x64) triple="x86_64-unknown-linux-gnu"; ext="" ;;
  win-x64) triple="x86_64-pc-windows-msvc"; ext=".exe" ;;
  *) echo "rid desconhecido: $rid" >&2; exit 1 ;;
esac
out="$root/.build/sidecar/$rid"
dotnet publish "$root/src/MboxPst.Sidecar" -c Release -r "$rid" --self-contained -o "$out" -v quiet -nologo
mkdir -p "$root/app/src-tauri/binaries"
cp "$out/mboxpst-core$ext" "$root/app/src-tauri/binaries/mboxpst-core-$triple$ext"
echo "$root/app/src-tauri/binaries/mboxpst-core-$triple$ext"
