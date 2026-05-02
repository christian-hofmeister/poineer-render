#!/usr/bin/env bash
set -e

cd "$(dirname "$0")/../src/POIneer.Render"

echo "Starting POIneer.Render (Production)..."

export DOTNET_ENVIRONMENT=Production

dotnet build -c Release
dotnet run -c Release --no-build