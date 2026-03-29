#!/usr/bin/env bash
set -euo pipefail

echo "🚀 Setting up POIneer dev environment"

./scripts/flyway/flyway-setup.sh

echo "✅ Dev environment ready"