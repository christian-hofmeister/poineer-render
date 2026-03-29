#!/usr/bin/env bash
set -euo pipefail
# POIneer helper: Switch /opt/dotnet/current symlink to a given installed SDK version
# Usage:
#   sudo ./set-dotnet-current.sh 10.0.100
# or (when installed to /usr/local/bin):
#   sudo set-dotnet-current 9.0.304

VERSION="${1:-}"
DEST="/opt/dotnet/${VERSION}"
LINK="/opt/dotnet/current"

if [[ -z "$VERSION" ]]; then
  echo "ERROR: Please pass the SDK version you want to make default." >&2
  exit 1
fi
if [[ ! -d "$DEST" ]]; then
  echo "ERROR: ${DEST} does not exist. Install it first (install-dotnet-sdk ${VERSION})." >&2
  exit 2
fi

sudo ln -sfn "$DEST" "$LINK"
echo ">> Now pointing ${LINK} -> ${DEST}"
echo ">> Reopen your shell or 'source /etc/profile.d/dotnet.sh' to pick up changes."
echo ">> 'dotnet --list-sdks' should show the new default first:"
dotnet --list-sdks | head -5    
