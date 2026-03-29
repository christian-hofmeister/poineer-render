#!/usr/bin/env bash
set -euo pipefail

# Usage:
# ./set-flyway-current.sh 12.2.0

VERSION="${1:-}"
INSTALL_DIR="/opt/flyway"
TARGET_DIR="$INSTALL_DIR/flyway-$VERSION"
CURRENT_LINK="$INSTALL_DIR/current"
BIN_LINK="/usr/local/bin/flyway"

if [[ -z "$VERSION" ]]; then
  echo "❌ Usage: $0 <version>"
  exit 1
fi

if [[ ! -d "$TARGET_DIR" ]]; then
  echo "❌ Version not installed: $TARGET_DIR"
  exit 1
fi

echo "➡️ Switching Flyway to $VERSION"

# current symlink
sudo ln -sfn "$TARGET_DIR" "$CURRENT_LINK"

# global binary
sudo ln -sfn "$CURRENT_LINK/flyway" "$BIN_LINK"

echo "✅ Flyway now points to:"
ls -l "$BIN_LINK"

echo "🚀 Version check:"
flyway -v