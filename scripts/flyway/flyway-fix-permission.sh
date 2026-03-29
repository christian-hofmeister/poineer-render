#!/usr/bin/env bash
set -euo pipefail

BASE_DIR="/opt/flyway"

if [[ ! -d "$BASE_DIR" ]]; then
  echo "❌ Directory not found: $BASE_DIR"
  exit 1
fi

echo "🔧 Fixing permissions in $BASE_DIR"

# Owner setzen
echo "👤 Setting owner root:root"
sudo chown -R root:root "$BASE_DIR"

# Verzeichnisse
echo "📁 Fixing directories (755)"
sudo find "$BASE_DIR" -type d -exec chmod 755 {} \;

# Dateien
echo "📄 Fixing files (644)"
sudo find "$BASE_DIR" -type f -exec chmod 644 {} \;

# Executables explizit korrigieren
echo "🚀 Fixing flyway executables"

sudo find "$BASE_DIR" -type f -name "flyway" -exec chmod 755 {} \;

# optional: Windows .cmd nicht ausführbar machen
sudo find "$BASE_DIR" -type f -name "*.cmd" -exec chmod 644 {} \;

echo "✅ Permissions normalized"

# Debug Output
echo ""
echo "🔍 Current setup:"
ls -l "$BASE_DIR"
echo ""
ls -l /usr/local/bin/flyway 2>/dev/null || true