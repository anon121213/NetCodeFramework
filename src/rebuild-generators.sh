#!/usr/bin/env bash
# Rebuild Skynet.SourceGenerators.dll and drop it into Unity's Assets folder.
# Run from anywhere: `bash src/rebuild-generators.sh`
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
if [ ! -x "$DOTNET" ]; then
  DOTNET="$(command -v dotnet || true)"
fi
if [ -z "$DOTNET" ]; then
  echo "dotnet CLI not found. Install .NET SDK or set DOTNET env var." >&2
  exit 1
fi

echo "==> building Skynet.SourceGenerators (Release)"
"$DOTNET" build "$SCRIPT_DIR/Skynet.SourceGenerators/Skynet.SourceGenerators.csproj" -c Release --nologo -v minimal

SRC_DLL="$SCRIPT_DIR/Skynet.SourceGenerators/bin/Release/netstandard2.0/Skynet.SourceGenerators.dll"
DST_DIR="$REPO_ROOT/Assets/Skynet/Runtime/Generators"
DST_DLL="$DST_DIR/Skynet.SourceGenerators.dll"

if [ ! -f "$SRC_DLL" ]; then
  echo "Built DLL not found at $SRC_DLL" >&2
  exit 1
fi

cp "$SRC_DLL" "$DST_DLL"
echo "==> copied to $DST_DLL"
echo "Return to Unity — it will reimport the analyzer on next focus."
