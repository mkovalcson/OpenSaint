#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

INSTALL_DEPS=0
if [[ "${1:-}" == "--install-deps" ]]; then
  INSTALL_DEPS=1
fi

if [[ "$INSTALL_DEPS" == "1" ]]; then
  if command -v apt-get >/dev/null 2>&1; then
    sudo apt-get update
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y \
      build-essential curl wget file pkg-config patchelf \
      libssl-dev libgtk-3-dev libwebkit2gtk-4.1-dev \
      libayatana-appindicator3-dev librsvg2-dev libudev-dev \
      libasound2-dev libxdo-dev
  else
    echo "--install-deps currently supports Debian/Ubuntu only." >&2
    echo "Install the Tauri v2 Linux prerequisites for your distribution, then rerun without --install-deps." >&2
    exit 2
  fi
fi

if ! command -v cargo >/dev/null 2>&1; then
  echo "Rust/Cargo not found. Installing Rust with rustup..."
  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y
  # shellcheck disable=SC1090
  source "$HOME/.cargo/env"
fi

if ! command -v node >/dev/null 2>&1 || ! command -v npm >/dev/null 2>&1; then
  echo "Node.js and npm are required. Install Node.js 20 or newer and rerun." >&2
  exit 3
fi

echo "Node: $(node --version)"
echo "npm:  $(npm --version)"
echo "Rust: $(rustc --version)"
echo "Cargo: $(cargo --version)"

npm ci
npm run tauri:build

OUT_DIR="$ROOT/src-tauri/target/release/bundle/appimage"
APPIMAGE="$(find "$OUT_DIR" -maxdepth 1 -type f -name '*.AppImage' -print -quit 2>/dev/null || true)"

if [[ -z "$APPIMAGE" ]]; then
  echo "Build completed, but no AppImage was found in $OUT_DIR" >&2
  exit 4
fi

mkdir -p "$ROOT/release"
cp -f "$APPIMAGE" "$ROOT/release/SAINT-Controller.AppImage"
chmod +x "$ROOT/release/SAINT-Controller.AppImage"

echo
echo "AppImage created:"
echo "  $ROOT/release/SAINT-Controller.AppImage"
ls -lh "$ROOT/release/SAINT-Controller.AppImage"
