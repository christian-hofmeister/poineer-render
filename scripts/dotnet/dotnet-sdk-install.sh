#!/usr/bin/env bash
set -euo pipefail
# Usage:
#   sudo ./install-dotnet-sdk.sh 9.0.304                 # GA (CDN)
#   sudo ./install-dotnet-sdk.sh 10.0.100-rc.1           # RC (dotnet-install.sh)
#   sudo ./install-dotnet-sdk.sh 10.0.100-preview.7      # Preview (dotnet-install.sh)

VERSION="${1:-}"
if [[ -z "$VERSION" ]]; then
  echo "ERROR: Please pass the SDK version, e.g. 9.0.304 or 10.0.100-rc.1" >&2
  exit 1
fi

ARCH="linux-x64"
DEST="/opt/dotnet/${VERSION}"
mkdir -p /opt/dotnet

is_prerelease=0
quality=""
channel=""

if [[ "$VERSION" =~ -(rc|preview)\.[0-9]+$ ]]; then
  is_prerelease=1
  # Map 'rc' -> 'preview' for dotnet-install.sh
  case "${BASH_REMATCH[1]}" in
    rc)      quality="preview" ;;
    preview) quality="preview" ;;
  esac
  base="${VERSION%%-*}"            # e.g. 10.0.100
  IFS='.' read -r maj min patch <<<"$base"
  channel="${maj}.${min}.1xx"
fi

if [[ "$is_prerelease" -eq 1 ]]; then
  echo ">> Installing prerelease via dotnet-install.sh (channel=${channel}, quality=${quality})"
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
  curl -sSL https://dot.net/v1/dotnet-install.sh -o "${tmp}/dotnet-install.sh"
  chmod +x "${tmp}/dotnet-install.sh"
  if [[ -d "$DEST" ]]; then
    echo ">> ${DEST} already exists; leaving as-is."
  else
    sudo mkdir -p "$DEST"
    sudo bash "${tmp}/dotnet-install.sh" \
      --channel "${channel}" \
      --quality "${quality}" \
      --install-dir "${DEST}" \
      --architecture x64
  fi
  echo ">> Done. Use 'set-dotnet-current ${VERSION}' to switch default."
  exit 0
fi

# GA-/vollqualifizierte Version: direktes CDN
TARBALL="dotnet-sdk-${VERSION}-${ARCH}.tar.gz"
URLS=(
  "https://dotnetcli.azureedge.net/dotnet/Sdk/${VERSION}/${TARBALL}"
)

tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
cd "$tmp"
echo ">> Fetching ${TARBALL} ..."
if ! curl -fL --retry 3 --connect-timeout 10 -o "${TARBALL}" "${URLS[0]}"; then
  echo "ERROR: Could not download ${TARBALL}" >&2
  exit 2
fi
if ! file --mime-type "${TARBALL}" | grep -q 'application/gzip'; then
  echo "ERROR: Downloaded file is not a gzip archive." >&2
  exit 3
fi
if ! gzip -t "${TARBALL}" 2>/dev/null; then
  echo "ERROR: gzip test failed (corrupted download?)." >&2
  exit 4
fi

if [[ -d "$DEST" ]]; then
  echo ">> ${DEST} already exists; leaving as-is."
else
  echo ">> Installing to ${DEST}"
  sudo mkdir -p "$DEST"
  sudo tar -xzf "${TARBALL}" -C "$DEST"
fi
echo ">> Done. Use 'set-dotnet-current ${VERSION}' to switch default."
exit 0