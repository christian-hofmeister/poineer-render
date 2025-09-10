#!/usr/bin/env bash
#
# poineer-fix-perms.sh
# Sets owner/group and permissions for web/artifact directories.
# Suitable for Jenkins post-deploy, render outputs, or manual use.
#
set -euo pipefail

OWNER="root"
GROUP="www-data"
DIR_MODE="755"
FILE_MODE="644"
STRICT_TILES="0"
DRY_RUN="0"
VERBOSE="0"

log()  { echo "[poineer-fix-perms] $*"; }
vlog() { [ "$VERBOSE" = "1" ] && echo "[poineer-fix-perms] $*"; }
err()  { echo "[poineer-fix-perms:ERROR] $*" >&2; }

usage() {
  cat <<EOF
Usage: $(basename "$0") [OPTIONS] <PATH> [<PATH2>...]

Options:
  -o, --owner NAME        File owner (default: $OWNER)
  -g, --group NAME        File group (default: $GROUP)
  -d, --dir-mode MODE     Directory permissions (default: $DIR_MODE)
  -f, --file-mode MODE    File permissions (default: $FILE_MODE)
      --strict-tiles      Additionally sets *.mbtiles to 640
      --dry-run           Only show actions, do not change anything
  -v, --verbose           Verbose output
  -h, --help              Show help

Examples:
  $(basename "$0") /var/www/poineer.app
  $(basename "$0") -o root -g www-data /var/www/poineer.app /srv/tiles
  $(basename "$0") --dry-run -v /srv/tiles
EOF
}

# --- Parse arguments ---
ARGS=()
while (( "$#" )); do
  case "$1" in
    -o|--owner)        OWNER="${2:?}"; shift 2 ;;
    -g|--group)        GROUP="${2:?}"; shift 2 ;;
    -d|--dir-mode)     DIR_MODE="${2:?}"; shift 2 ;;
    -f|--file-mode)    FILE_MODE="${2:?}"; shift 2 ;;
       --strict-tiles) STRICT_TILES="1"; shift 1 ;;
       --dry-run)      DRY_RUN="1"; shift 1 ;;
    -v|--verbose)      VERBOSE="1"; shift 1 ;;
    -h|--help)         usage; exit 0 ;;
    --)                shift; break ;;
    -*)                err "Unknown option: $1"; usage; exit 2 ;;
     *)                ARGS+=("$1"); shift ;;
  esac
done

if [ "${#ARGS[@]}" -lt 1 ]; then usage; exit 2; fi

# Validate owner/group
if ! id -u "$OWNER" >/dev/null 2>&1; then err "Owner '$OWNER' does not exist"; exit 3; fi
if ! getent group "$GROUP" >/dev/null 2>&1; then err "Group '$GROUP' does not exist"; exit 3; fi

apply_chown() {
  local path="$1"
  vlog "chown -R ${OWNER}:${GROUP} '$path'"
  [ "$DRY_RUN" = "1" ] || chown -R "${OWNER}:${GROUP}" "$path"
}

apply_dir_modes() {
  local path="$1"
  vlog "chmod directories $DIR_MODE under '$path'"
  if [ "$DRY_RUN" = "1" ]; then
    find "$path" -type d -print
  else
    # Set directory permissions + setgid (group inheritance)
    find "$path" -type d -print0 | xargs -0 -r chmod "$DIR_MODE"
    vlog "chmod g+s (setgid) on directories under '$path'"
    find "$path" -type d -print0 | xargs -0 -r chmod g+s
  fi
}

apply_file_modes() {
  local path="$1"
  vlog "chmod files $FILE_MODE under '$path'"
  if [ "$DRY_RUN" = "1" ]; then
    find "$path" -type f -print
  else
    find "$path" -type f -print0 | xargs -0 -r chmod "$FILE_MODE"
  fi

  vlog "chmod *.sqlite,*.db -> 640 under '$path'"
  if [ "$DRY_RUN" = "1" ]; then
    find "$path" -type f \( -iname "*.sqlite" -o -iname "*.db" \) -print
  else
    find "$path" -type f \( -iname "*.sqlite" -o -iname "*.db" \) -print0 | xargs -0 -r chmod 640
  fi

  if [ "$STRICT_TILES" = "1" ]; then
    vlog "strict-tiles active: chmod *.mbtiles -> 640 under '$path'"
    if [ "$DRY_RUN" = "1" ]; then
      find "$path" -type f -iname "*.mbtiles" -print
    else
      find "$path" -type f -iname "*.mbtiles" -print0 | xargs -0 -r chmod 640
    fi
  fi
}

# --- Main logic ---
log "Owner=${OWNER} Group=${GROUP} DIR_MODE=${DIR_MODE} FILE_MODE=${FILE_MODE} strict-tiles=${STRICT_TILES} dry-run=${DRY_RUN}"

for path in "${ARGS[@]}"; do
  if [ ! -e "$path" ]; then err "Path does not exist: $path"; exit 4; fi
  log "Processing: $path"
  apply_chown "$path"
  apply_dir_modes "$path"
  apply_file_modes "$path"
done