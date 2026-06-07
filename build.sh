#!/usr/bin/env bash
# build.sh — cross-compile MatrixSaver for Windows from Linux
# Requires: .NET 8 SDK  (run: curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0)
set -euo pipefail

export PATH="$HOME/.dotnet:$PATH"

echo "▶ Restoring packages..."
dotnet restore -r win-x64

echo "▶ Publishing (self-contained, single-file, win-x64)..."
dotnet publish \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o dist/

# Rename exe → scr (screensavers are just renamed exes on Windows)
if [ -f dist/MatrixSaver.exe ]; then
  cp dist/MatrixSaver.exe dist/MatrixSaver.scr
  echo "✓ dist/MatrixSaver.exe"
  echo "✓ dist/MatrixSaver.scr"
fi

echo "▶ Checking matrix engine..."
if [ -d dist/matrix ]; then
  echo "✓ dist/matrix/ ($(find dist/matrix -type f | wc -l) files)"
else
  echo "✗ dist/matrix/ missing — copy it manually alongside the exe"
fi

echo ""
echo "─────────────────────────────────────────────────────────────────"
echo "Build complete!  Distribute everything in dist/"
echo ""
echo "On Windows:"
echo "  1. Extract dist/ to any folder (e.g. C:\\MatrixSaver\\)"
echo "  2. Run install.bat as Administrator"
echo "  OR: Right-click MatrixSaver.scr → Install"
echo "─────────────────────────────────────────────────────────────────"
