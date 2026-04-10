#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$SCRIPT_DIR/flyway-version"
INSTALL_SCRIPT="$SCRIPT_DIR/flyway-install.sh"
SET_CURRENT_SCRIPT="$SCRIPT_DIR/flyway-set-current.sh"

if [[ ! -f "$VERSION_FILE" ]]; then
  echo "❌ Flyway version file not found: $VERSION_FILE"
  exit 1
fi

VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"

if [[ -z "$VERSION" ]]; then
  echo "❌ Flyway version file is empty: $VERSION_FILE"
  exit 1
fi

echo "➡️ Using Flyway version: $VERSION"
echo "📄 Version file: $VERSION_FILE"

"$INSTALL_SCRIPT" "$VERSION"
"$SET_CURRENT_SCRIPT" "$VERSION"

echo "✅ Flyway setup completed for version $VERSION"