#!/usr/bin/env bash
# Build MightyOaks and create Thunderstore/Nexus upload zip.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
VERSION="$(grep -oP '(?<="version_number": ")[^"]+' "$ROOT/manifest.json")"
BUILD_CONFIG="${BUILD_CONFIG:-Release}"
OUT_DIR="$ROOT/dist"
STAGING="$OUT_DIR/staging"
ZIP="$OUT_DIR/lailoken-MightyOaks-${VERSION}.zip"

dotnet build "$ROOT/MightyOaks.csproj" -c "$BUILD_CONFIG"

rm -rf "$STAGING" "$ZIP"
mkdir -p "$STAGING"

cp "$ROOT/bin/$BUILD_CONFIG/net462/MightyOaks.dll" "$STAGING/"
cp "$ROOT/manifest.json" "$STAGING/"
cp "$ROOT/README.md" "$STAGING/"
cp "$ROOT/icon.png" "$STAGING/"

(
  cd "$STAGING"
  zip -r "$ZIP" .
)

echo "Created $ZIP"
ls -lh "$ZIP"
