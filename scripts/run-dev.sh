#!/usr/bin/env bash
set -e

cd "$(dirname "$0")/../src/POIneer.Render"

echo "Starting POIneer.Render (Development)..."

export DOTNET_ENVIRONMENT=Development

dotnet build
dotnet run --no-build