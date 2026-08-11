#!/usr/bin/env bash
set -Eeuo pipefail

APP_NAME="SAINT Controller"
DEFAULT_INSTALL_DIR="$HOME/Applications"
DEFAULT_INSTALL_PATH="$DEFAULT_INSTALL_DIR/SAINT-Controller.AppImage"
DEFAULT_DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
DEFAULT_DESKTOP_PATH="$DEFAULT_DESKTOP_DIR/saint-controller.desktop"

usage() {
  cat <<'USAGE'
SteamController / SAINT Controller AppImage installer for Steam Deck

Usage:
  ./install-steamcontroller.sh [APPIMAGE]
  ./install-steamcontroller.sh --url URL [--sha256 HASH]

Options:
  --install-dir DIR   Install directory (default: ~/Applications)
  --url URL           Download the AppImage from URL instead of a local file
  --sha256 HASH       Verify the AppImage against this SHA-256 before install
  --launch            Launch the controller after installing
  --no-desktop        Do not create the XDG .desktop launcher
  -h, --help          Show this help

If APPIMAGE is omitted, the script searches its own directory for:
  SAINT-Controller.AppImage
  SteamController.AppImage
  saint_firmware_controller_*.AppImage

This installer does not modify SteamOS, disable read-only mode, or install a
compiler/toolchain. The production AppImage is intended to be self-contained.
USAGE
}

SOURCE=""
URL=""
EXPECTED_SHA256=""
INSTALL_DIR="$DEFAULT_INSTALL_DIR"
CREATE_DESKTOP=1
LAUNCH=0

while (($#)); do
  case "$1" in
    --install-dir)
      [[ $# -ge 2 ]] || { echo "ERROR: --install-dir requires a value" >&2; exit 2; }
      INSTALL_DIR="$2"
      shift 2
      ;;
    --url)
      [[ $# -ge 2 ]] || { echo "ERROR: --url requires a value" >&2; exit 2; }
      URL="$2"
      shift 2
      ;;
    --sha256)
      [[ $# -ge 2 ]] || { echo "ERROR: --sha256 requires a value" >&2; exit 2; }
      EXPECTED_SHA256="${2,,}"
      shift 2
      ;;
    --launch)
      LAUNCH=1
      shift
      ;;
    --no-desktop)
      CREATE_DESKTOP=0
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --*)
      echo "ERROR: Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
    *)
      if [[ -n "$SOURCE" ]]; then
        echo "ERROR: Only one AppImage path may be supplied." >&2
        exit 2
      fi
      SOURCE="$1"
      shift
      ;;
  esac
done

if [[ -n "$SOURCE" && -n "$URL" ]]; then
  echo "ERROR: Supply either a local AppImage or --url, not both." >&2
  exit 2
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
TMP_FILE=""
cleanup() {
  [[ -n "$TMP_FILE" && -f "$TMP_FILE" ]] && rm -f -- "$TMP_FILE"
}
trap cleanup EXIT

if [[ -n "$URL" ]]; then
  if ! command -v curl >/dev/null 2>&1; then
    echo "ERROR: curl is required when using --url." >&2
    exit 1
  fi
  TMP_FILE="$(mktemp --suffix=.AppImage)"
  echo "Downloading $APP_NAME..."
  curl --fail --location --show-error --progress-bar "$URL" -o "$TMP_FILE"
  SOURCE="$TMP_FILE"
elif [[ -z "$SOURCE" ]]; then
  shopt -s nullglob
  candidates=(
    "$SCRIPT_DIR/SAINT-Controller.AppImage"
    "$SCRIPT_DIR/SteamController.AppImage"
    "$SCRIPT_DIR"/saint_firmware_controller_*.AppImage
  )
  shopt -u nullglob

  if ((${#candidates[@]} == 0)); then
    echo "ERROR: No AppImage was supplied or found next to this installer." >&2
    echo "Put the built AppImage beside this script or pass its path explicitly." >&2
    exit 1
  fi

  # Prefer the newest matching artifact when multiple versioned builds exist.
  SOURCE="$(ls -1t -- "${candidates[@]}" 2>/dev/null | head -n 1)"
fi

SOURCE="$(realpath -- "$SOURCE")"
[[ -f "$SOURCE" ]] || { echo "ERROR: AppImage not found: $SOURCE" >&2; exit 1; }

# A production Steam Deck build must be an x86-64 ELF/AppImage.
if command -v file >/dev/null 2>&1; then
  FILE_DESC="$(file -b -- "$SOURCE" || true)"
  if [[ "$FILE_DESC" != *"x86-64"* && "$FILE_DESC" != *"x86_64"* ]]; then
    echo "WARNING: This does not appear to be an x86-64 AppImage:" >&2
    echo "         $FILE_DESC" >&2
  fi
fi

if [[ -n "$EXPECTED_SHA256" ]]; then
  command -v sha256sum >/dev/null 2>&1 || {
    echo "ERROR: sha256sum is required for --sha256 verification." >&2
    exit 1
  }
  ACTUAL_SHA256="$(sha256sum -- "$SOURCE" | awk '{print tolower($1)}')"
  if [[ "$ACTUAL_SHA256" != "$EXPECTED_SHA256" ]]; then
    echo "ERROR: SHA-256 verification failed." >&2
    echo "Expected: $EXPECTED_SHA256" >&2
    echo "Actual:   $ACTUAL_SHA256" >&2
    exit 1
  fi
  echo "SHA-256 verified."
fi

mkdir -p -- "$INSTALL_DIR"
INSTALL_PATH="$INSTALL_DIR/SAINT-Controller.AppImage"
STAGING_PATH="$INSTALL_PATH.new"

# Atomic replacement keeps an existing Steam shortcut valid during upgrades.
echo "Installing to: $INSTALL_PATH"
cp -- "$SOURCE" "$STAGING_PATH"
chmod 0755 -- "$STAGING_PATH"
mv -f -- "$STAGING_PATH" "$INSTALL_PATH"

if ((CREATE_DESKTOP)); then
  mkdir -p -- "$DEFAULT_DESKTOP_DIR"
  cat > "$DEFAULT_DESKTOP_PATH" <<EOF_DESKTOP
[Desktop Entry]
Type=Application
Name=SAINT Controller
Comment=Steam Deck controller interface
Exec=$INSTALL_PATH
Terminal=false
Categories=Utility;Game;
StartupNotify=true
EOF_DESKTOP
  chmod 0644 -- "$DEFAULT_DESKTOP_PATH"
  echo "Created launcher: $DEFAULT_DESKTOP_PATH"

  # Refreshing is optional; KDE will discover the entry without this eventually.
  if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$DEFAULT_DESKTOP_DIR" >/dev/null 2>&1 || true
  fi
fi

cat <<EOF_DONE

$APP_NAME installed successfully.

Executable:
  $INSTALL_PATH

To use it in Steam Game Mode:
  1. Open Steam in Desktop Mode.
  2. Games -> Add a Non-Steam Game to My Library.
  3. Browse to $INSTALL_PATH
  4. Rename the Steam entry to "SAINT Controller" if desired.

No Rust, Node.js, npm, GTK/WebKit development packages, or SteamOS root changes
are required to run the production AppImage.
EOF_DONE

if ((LAUNCH)); then
  echo "Launching $APP_NAME..."
  "$INSTALL_PATH" >/dev/null 2>&1 &
fi
