#!/usr/bin/env bash
# Build and package script for WatchNixtoons2 Jellyfin plugin

set -e

PLUGIN_NAME="Jellyfin.Plugin.WatchNixtoons2"
VERSION="1.0.0"
BUILD_DIR="publish"
PACKAGE_DIR="package"
REPO_DIR="repo"

echo "Building $PLUGIN_NAME v$VERSION..."

# Clean previous builds
rm -rf "$BUILD_DIR" "$PACKAGE_DIR" "$REPO_DIR"

# Build in Release mode
dotnet publish -c Release -o "$BUILD_DIR"

# Create package directory structure
mkdir -p "$PACKAGE_DIR/$PLUGIN_NAME"
cp -r "$BUILD_DIR"/* "$PACKAGE_DIR/$PLUGIN_NAME/"

# Create zip package
mkdir -p "$REPO_DIR"
cd "$PACKAGE_DIR"
zip -r "../$REPO_DIR/${PLUGIN_NAME}_${VERSION}.zip" "$PLUGIN_NAME"
cd ..

# Generate SHA256 checksum
CHECKSUM=$(sha256sum "$REPO_DIR/${PLUGIN_NAME}_${VERSION}.zip" | cut -d' ' -f1)
echo "SHA256: $CHECKSUM"

# Update manifest.json with actual checksum and version
sed -i "s/\"version\": \"1.0.0\"/\"version\": \"$VERSION\"/" manifest.json
sed -i "s/sha256:PLACEHOLDER_REPLACE_WITH_ACTUAL_HASH/sha256:$CHECKSUM/" manifest.json
sed -i "s|https://your-domain.com/jellyfin-watchnixtoons2/Jellyfin.Plugin.WatchNixtoons2_1.0.0.zip|https://your-domain.com/jellyfin-watchnixtoons2/${PLUGIN_NAME}_${VERSION}.zip|" manifest.json

# Copy manifest to repo directory
cp manifest.json "$REPO_DIR/"

echo ""
echo "=========================================="
echo "Repository package created in $REPO_DIR/"
echo "=========================================="
echo "Files to host:"
echo "  - $REPO_DIR/manifest.json"
echo "  - $REPO_DIR/${PLUGIN_NAME}_${VERSION}.zip"
echo ""
echo "Update manifest.json with your actual domain URL"
echo "Then add repository URL to Jellyfin: https://your-domain.com/jellyfin-watchnixtoons2/manifest.json"