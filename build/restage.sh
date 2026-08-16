#!/usr/bin/env bash
# Rebuild the mod and restage it into the local Vintage Story Mods folder for manual
# playtesting. Builds src/Mod.csproj, then copies the mod DLL + modinfo.json + assets into
# ~/Library/Application Support/VintagestoryData/Mods/<modid> so the game loads it directly.
#
# Gui.dll (and the game/Harmony/OpenTK DLLs) are NOT staged - the installed `gui` mod and the
# game itself provide them at runtime. Only our own assembly and content ship.
set -euo pipefail

MODID="libguitoolsmithsharpness"
ASSEMBLY="LibGuiToolsmithSharpness"
CONFIG="${1:-Release}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SRC="$ROOT/src"

DATA_DIR="${VINTAGESTORY_DATA:-$HOME/Library/Application Support/VintagestoryData}"
STAGE="$DATA_DIR/Mods/$MODID"

echo "==> Building ($CONFIG)"
dotnet build "$SRC/Mod.csproj" -c "$CONFIG" --nologo

BIN="$SRC/bin/$CONFIG/net10.0"
DLL="$BIN/$ASSEMBLY.dll"
if [[ ! -f "$DLL" ]]; then
  echo "ERROR: built assembly not found at $DLL" >&2
  exit 1
fi

echo "==> Staging into $STAGE"
rm -rf "$STAGE"
mkdir -p "$STAGE"
cp "$DLL" "$STAGE/"
cp "$SRC/modinfo.json" "$STAGE/"
[[ -f "$SRC/modicon.png" ]] && cp "$SRC/modicon.png" "$STAGE/"
cp -R "$SRC/assets" "$STAGE/"

echo "==> Done. Restart Vintage Story (or reload mods) to pick up the change."
