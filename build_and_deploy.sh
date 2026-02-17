#!/bin/bash
set -e

# Project paths
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$PROJECT_DIR/MightyOaks.csproj"
BUILD_DIR="$PROJECT_DIR/bin/Debug/net462"
PLUGIN_DLL="MightyOaks.dll"

# Valheim paths
VALHEIM_DIR="/home/marius/valheim"
BEPINEX_PLUGINS_DIR="$VALHEIM_DIR/BepInEx/plugins/MightyOaks"

echo "Building MightyOaks plugin..."
dotnet build "$PROJECT_FILE"

if [ $? -eq 0 ]; then
    echo "Build successful."
    
    # Ensure destination directory exists
    mkdir -p "$BEPINEX_PLUGINS_DIR"
    
    echo "Copying $PLUGIN_DLL to $BEPINEX_PLUGINS_DIR..."
    cp "$BUILD_DIR/$PLUGIN_DLL" "$BEPINEX_PLUGINS_DIR/$PLUGIN_DLL"
    
    echo "Deployment complete!"
else
    echo "Build failed. Deployment aborted."
    exit 1
fi
