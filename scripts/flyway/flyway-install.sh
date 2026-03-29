#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-}"
INSTALL_DIR="/opt/flyway"
TARGET_DIR="$INSTALL_DIR/flyway-$VERSION"
TMP_DIR="/tmp/flyway-install"

if [[ -z "$VERSION" ]]; then
  echo "❌ Usage: $0 <version>"
  exit 1
fi

ARCHIVE_NAME="flyway-commandline-$VERSION-linux-x64.tar.gz"
BASE_URL="https://repo1.maven.org/maven2/org/flywaydb/flyway-commandline/$VERSION"
DOWNLOAD_URL="$BASE_URL/$ARCHIVE_NAME"

echo "➡️ Installing Flyway $VERSION"

echo "🔎 Checking if version exists..."
if ! curl -fsI "$BASE_URL/" >/dev/null; then
  echo "❌ Flyway version $VERSION not found in Maven Central"
  exit 1
fi

sudo mkdir -p "$INSTALL_DIR"
rm -rf "$TMP_DIR"
mkdir -p "$TMP_DIR"

cd "$TMP_DIR"

echo "⬇️ Downloading $DOWNLOAD_URL"
curl -fL -o "$ARCHIVE_NAME" "$DOWNLOAD_URL"

echo "📦 Extracting..."
tar -xzf "$ARCHIVE_NAME"

EXTRACTED_DIR="flyway-$VERSION"

if [[ ! -d "$EXTRACTED_DIR" ]]; then
  echo "❌ Expected extracted directory not found: $EXTRACTED_DIR"
  ls -la
  exit 1
fi

echo "📁 Moving to $TARGET_DIR"
sudo rm -rf "$TARGET_DIR"
sudo mv "$EXTRACTED_DIR" "$TARGET_DIR"

echo "🔐 Setting permissions"
sudo chown -R root:root "$TARGET_DIR"
sudo find "$TARGET_DIR" -type d -exec chmod 755 {} \;
sudo find "$TARGET_DIR" -type f -exec chmod 644 {} \;
sudo chmod 755 "$TARGET_DIR/flyway"

echo "✅ Flyway $VERSION installed in $TARGET_DIR"