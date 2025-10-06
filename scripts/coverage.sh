#!/usr/bin/env bash
set -euo pipefail
dotnet test -c Release --no-build \
  -p:CollectCoverage=true \
  -p:CoverletOutputFormat=cobertura \
  -p:CoverletOutput=./TestResults/Coverage/
dotnet tool restore || true
dotnet tool run reportgenerator \
  -reports:'**/TestResults/Coverage/coverage.cobertura.xml' \
  -targetdir:coverage-report \
  -reporttypes:'Html;HtmlSummary;TextSummary'
echo "Report: coverage-report/index.html"