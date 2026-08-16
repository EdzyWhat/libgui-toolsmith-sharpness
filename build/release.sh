#!/usr/bin/env bash
# Build a distributable release zip of the mod, ready to load in Vintage Story or upload to the ModDB.
#
# The zip mirrors what the game expects at a mod's root: modinfo.json + the compiled assembly +
# assets/. The version in the file name is read straight from modinfo.json so it can never drift.
#
# Gui.dll and the game/Harmony/OpenTK DLLs are NOT bundled - the installed `gui` mod and the
# game itself provide them at runtime (all our references are <Private>false</Private>). Only our own
# assembly and content ship.
set -euo pipefail

MODID="libguitoolsmithsharpness"
ASSEMBLY="LibGuiToolsmithSharpness"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SRC="$ROOT/src"
DIST="$ROOT/dist"

# Pull the version out of modinfo.json (e.g. "version": "1.0.0").
VERSION="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$SRC/modinfo.json")"
if [[ -z "$VERSION" ]]; then
  echo "ERROR: could not read \"version\" from $SRC/modinfo.json" >&2
  exit 1
fi

echo "==> Building (Release)"
dotnet build "$SRC/Mod.csproj" -c Release --nologo

BIN="$SRC/bin/Release/net10.0"
DLL="$BIN/$ASSEMBLY.dll"
if [[ ! -f "$DLL" ]]; then
  echo "ERROR: built assembly not found at $DLL" >&2
  exit 1
fi

# Assemble the payload in a clean staging dir, then zip its contents at the root.
STAGE="$DIST/_stage"
rm -rf "$STAGE"
mkdir -p "$STAGE"
cp "$DLL" "$STAGE/"
cp "$SRC/modinfo.json" "$STAGE/"
[[ -f "$SRC/modicon.png" ]] && cp "$SRC/modicon.png" "$STAGE/"
cp -R "$SRC/assets" "$STAGE/"

ZIP="$DIST/${MODID}_${VERSION}.zip"
rm -f "$ZIP"
echo "==> Packaging $ZIP"
( cd "$STAGE" && zip -r -q "$ZIP" . )
rm -rf "$STAGE"

echo "==> Done: $ZIP"
unzip -l "$ZIP"
