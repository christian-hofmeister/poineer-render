#!/usr/bin/env bash
set -e

# Change into the project root (one level above scripts)
cd "$(dirname "$0")/.."
echo "working directory: $(pwd)"
export DOTNET_ENVIRONMENT=production
export POINEER_ENVIRONMENT=production

dotnet run --project src/POIneer.Render/POIneer.Render.csproj