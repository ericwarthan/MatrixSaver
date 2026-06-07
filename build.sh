#!/usr/bin/env bash
# build.sh — cross-compile MatrixSaver for Windows from Linux/macOS
set -euo pipefail

# Locate dotnet — check PATH first, then the default install location
if ! command -v dotnet &>/dev/null; then
    if [ -x "$HOME/.dotnet/dotnet" ]; then
        export PATH="$HOME/.dotnet:$PATH"
    else
        echo "ERROR: dotnet SDK not found."
        echo "Install with:"
        echo "  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0"
        exit 1
    fi
fi

echo "Using $(dotnet --version) at $(command -v dotnet)"
echo ""

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
  echo ""
  echo "✓ dist/MatrixSaver.exe"
  echo "✓ dist/MatrixSaver.scr"
fi

# Verify the matrix submodule is populated
if [ ! -f matrix/index.html ]; then
  echo ""
  echo "⚠  matrix/index.html not found — populate the submodule first:"
  echo "   git submodule update --init --recursive"
  exit 1
fi

echo ""
echo "─────────────────────────────────────────────────────────────────"
echo "Build complete.  Everything in dist/ is the full distribution."
echo ""
echo "On Windows:"
echo "  1. Copy the dist/ folder to a permanent location"
echo "  2. Run install.bat as Administrator"
echo "  OR: Right-click MatrixSaver.scr → Install"
echo "─────────────────────────────────────────────────────────────────"
