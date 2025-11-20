#!/usr/bin/env bash
set -e

# Change to the project root directory
cd "$(dirname "$0")/.."

# Ensure the output directory exists
mkdir -p output

# Run Flyway migrations using the relative config file path
flyway -configFiles=migrations/flyway-berlin.conf clean migrate

echo "Database migration for Berlin completed."

