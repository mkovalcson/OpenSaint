#!/usr/bin/env bash
#
# Headless SAINT Controller AppImage builder.
#
# Produces a .AppImage from the Tauri project, stages it into the
# server's firmware resources tree, and regenerates info.json.
#
# Designed to be invoked from two places without modification:
#
#   1. Inside the linux/amd64 Docker container built from
#      controller/appimage/Dockerfile, driven by build-docker.sh on a
#      developer machine.
#
#   2. Directly on a GitHub Linux runner.
#
# Everything is parameterized via env vars so the same script handles
# both. Defaults assume the Docker layout; CI overrides.
#
#   REPO_ROOT     repo checkout root              (default /work)
#   BUILD_DIR     persistent cache root           (default /build)
#                 holds cargo registry, target/, node_modules, npm cache
#
# Output:
#
#   $REPO_ROOT/server/resources/firmware/controller/
#       saint_firmware_controller_<version>.AppImage
#
#   $REPO_ROOT/server/resources/firmware/controller/info.json
#

set -euo pipefail


# ---------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------

REPO_ROOT="${REPO_ROOT:-/work}"
BUILD_DIR="${BUILD_DIR:-/build}"
CONTROLLER_DIR="$REPO_ROOT/controller"

mkdir -p "$BUILD_DIR"/{cargo,target,node_modules,npm-cache}


# ---------------------------------------------------------------------
# Build caches
# ---------------------------------------------------------------------

export CARGO_HOME="$BUILD_DIR/cargo"
export CARGO_TARGET_DIR="$BUILD_DIR/target"
export npm_config_cache="$BUILD_DIR/npm-cache"

# Prevent interactive prompts in CI/headless environments.
export CI=true
export NG_CLI_ANALYTICS=false

# linuxdeploy / appimagetool are themselves AppImages.
# This avoids requiring FUSE inside Docker/GitHub Actions.
export APPIMAGE_EXTRACT_AND_RUN=1

cd "$CONTROLLER_DIR"


# ---------------------------------------------------------------------
# Node dependencies
# ---------------------------------------------------------------------

# Keep node_modules in the persistent build cache.
if [ ! -L node_modules ]; then
    rm -rf node_modules
    ln -s "$BUILD_DIR/node_modules" node_modules
fi

# npm ci is strict about package-lock.json matching package.json.
# Reuse node_modules if it already matches the lock file.
if [ -f node_modules/.package-lock.json ] \
    && cmp -s package-lock.json node_modules/.package-lock.json; then

    echo "==> node_modules matches package-lock.json — skipping npm ci"

else
    if ! npm ci --no-audit --no-fund --prefer-offline; then

        echo
        echo "================================================================"
        echo "WARN  npm ci failed — package.json and package-lock.json are out"
        echo "      of sync."
        echo
        echo "      Falling back to npm install."
        echo
        echo "      Permanent fix:"
        echo
        echo "          cd controller"
        echo "          npm install"
        echo "          git add package-lock.json"
        echo "          git commit"
        echo "================================================================"
        echo

        npm install --no-audit --no-fund --prefer-offline
    fi
fi


# ---------------------------------------------------------------------
# Build version
# ---------------------------------------------------------------------

# Compute the canonical SAINT version string.
#
# Honor SAINT_BUILD_VERSION if supplied externally.
# Otherwise construct:
#
#   <VERSION>-local.<git-sha>
#
if [ -z "${SAINT_BUILD_VERSION:-}" ]; then

    _base_version=$(tr -d '[:space:]' < "$CONTROLLER_DIR/VERSION")

    _git_sha=$(
        cd "$REPO_ROOT" &&
        git rev-parse --short=7 HEAD 2>/dev/null ||
        echo unknown
    )

    export SAINT_BUILD_VERSION="${_base_version}-local.${_git_sha}"
fi

echo "==> Build version: $SAINT_BUILD_VERSION"


# ---------------------------------------------------------------------
# Force controller application crate rebuild
# ---------------------------------------------------------------------

# Keep dependency cache, but force the SAINT Controller crate itself
# to rebuild so SAINT_BUILD_VERSION cannot be stale.
echo "==> Clearing controller crate from build cache"

cargo clean --release \
    --manifest-path "$CONTROLLER_DIR/src-tauri/Cargo.toml" \
    -p saint-controller 2>/dev/null || true


# ---------------------------------------------------------------------
# Tauri build
# ---------------------------------------------------------------------

echo "==> Running tauri build --bundles appimage"

# Tauri creates the AppDir for us.
#
# Its final AppImage is not sufficient for stock SteamOS because
# linuxdeploy does not automatically bundle WebKitGTK's helper
# executables.
#
# We therefore treat Tauri's AppImage packaging as best-effort and
# post-process the AppDir ourselves.
set +e
npx tauri build --bundles appimage
TAURI_RESULT=$?
set -e

if [ "$TAURI_RESULT" -ne 0 ]; then
    echo "WARN: Tauri AppImage packaging returned $TAURI_RESULT."
    echo "      Continuing if an AppDir was generated."
fi


# ---------------------------------------------------------------------
# Locate Tauri AppDir
# ---------------------------------------------------------------------

APPDIR=$(
    find "$CARGO_TARGET_DIR/release/bundle/appimage" \
        -maxdepth 1 \
        -name '*.AppDir' \
        -print \
        -quit 2>/dev/null || true
)

if [ -z "$APPDIR" ]; then
    echo "ERROR: Tauri did not produce an AppDir." >&2
    echo "       Expected under:" >&2
    echo "       $CARGO_TARGET_DIR/release/bundle/appimage" >&2
    exit 1
fi

echo "==> AppDir:"
echo "    $APPDIR"


# ---------------------------------------------------------------------
# Verify compiled binary version
# ---------------------------------------------------------------------

APP_BIN="$APPDIR/usr/bin/saint-controller"

if [ ! -f "$APP_BIN" ]; then
    echo "ERROR: saint-controller binary is missing:" >&2
    echo "       $APP_BIN" >&2
    exit 1
fi

if ! grep -aqF "$SAINT_BUILD_VERSION" "$APP_BIN" 2>/dev/null; then

    echo "ERROR: built binary does not embed:" >&2
    echo "       $SAINT_BUILD_VERSION" >&2

    echo "Embedded version candidates:" >&2

    grep -aoE \
        '[0-9]+\.[0-9]+\.[0-9]+-[A-Za-z0-9.+-]*\.[0-9a-f]{7}' \
        "$APP_BIN" 2>/dev/null |
        sort -u |
        tr '\n' ' ' >&2 || true

    echo >&2
    echo "Try a clean rebuild:" >&2
    echo "    controller/appimage/build-docker.sh --clean" >&2

    exit 1
fi

echo "==> Verified binary embeds $SAINT_BUILD_VERSION"


# ---------------------------------------------------------------------
# Verify AppImage build tools
# ---------------------------------------------------------------------

if ! command -v linuxdeploy >/dev/null; then
    echo "ERROR: linuxdeploy not found on PATH." >&2
    exit 1
fi

if ! command -v appimagetool >/dev/null; then
    echo "ERROR: appimagetool not found on PATH." >&2
    exit 1
fi


# =====================================================================
# Steam Deck-specific AppImage processing
# =====================================================================


# ---------------------------------------------------------------------
# 1. Bundle WebKitGTK helper executables
# ---------------------------------------------------------------------

# The main WebKitGTK library launches several helper processes at
# runtime using paths that are not visible through ldd:
#
#   WebKitNetworkProcess
#   WebKitWebProcess
#   WebKitGPUProcess
#
# Stock SteamOS does not contain Ubuntu's webkit2gtk-4.1 directory,
# so those helpers must be included in the AppImage.

WEBKIT_LIBDIR="/usr/lib/x86_64-linux-gnu/webkit2gtk-4.1"

if [ -d "$WEBKIT_LIBDIR" ]; then

    echo "==> Bundling WebKitGTK helper processes"
    echo "    Source: $WEBKIT_LIBDIR"

    mkdir -p "$APPDIR$WEBKIT_LIBDIR"

    cp -a \
        "$WEBKIT_LIBDIR/." \
        "$APPDIR$WEBKIT_LIBDIR/"

else

    echo "ERROR: WebKitGTK helper directory not found:" >&2
    echo "       $WEBKIT_LIBDIR" >&2
    echo >&2
    echo "The resulting AppImage would not run correctly on SteamOS." >&2

    exit 1
fi


# ---------------------------------------------------------------------
# Verify helper binaries before continuing
# ---------------------------------------------------------------------

WEBKIT_WEB_PROCESS="$APPDIR$WEBKIT_LIBDIR/WebKitWebProcess"
WEBKIT_NETWORK_PROCESS="$APPDIR$WEBKIT_LIBDIR/WebKitNetworkProcess"

if [ ! -f "$WEBKIT_WEB_PROCESS" ]; then
    echo "ERROR: WebKitWebProcess was not bundled." >&2
    exit 1
fi

if [ ! -f "$WEBKIT_NETWORK_PROCESS" ]; then
    echo "ERROR: WebKitNetworkProcess was not bundled." >&2
    exit 1
fi

echo "==> WebKit helpers present:"
echo "    $WEBKIT_WEB_PROCESS"
echo "    $WEBKIT_NETWORK_PROCESS"


# ---------------------------------------------------------------------
# 2. Populate AppDir libraries using linuxdeploy
# ---------------------------------------------------------------------

pushd "$CARGO_TARGET_DIR/release/bundle/appimage" >/dev/null

echo "==> Running linuxdeploy"

linuxdeploy --appdir "$APPDIR"


# ---------------------------------------------------------------------
# 3. Install WebKit path-remapping shim
# ---------------------------------------------------------------------

# Ubuntu WebKitGTK contains a hard-coded helper directory:
#
#   /usr/lib/x86_64-linux-gnu/webkit2gtk-4.1/
#
# SteamOS is Arch-based and does not have this Debian/Ubuntu multiarch
# directory.
#
# The LD_PRELOAD shim remaps WebKit helper-process filesystem calls
# into the mounted AppImage.

SHIM_SRC="$CONTROLLER_DIR/appimage/path-shim.c"
SHIM_SO=""

if [ -f /opt/saint-shim/libpath-shim.so ]; then

    SHIM_SO="/opt/saint-shim/libpath-shim.so"

else

    SHIM_SO="$BUILD_DIR/libpath-shim.so"

    if [ ! -f "$SHIM_SO" ] || [ "$SHIM_SRC" -nt "$SHIM_SO" ]; then

        echo "==> Compiling LD_PRELOAD WebKit path shim"

        gcc \
            -shared \
            -fPIC \
            -O2 \
            -Wall \
            -o "$SHIM_SO" \
            "$SHIM_SRC" \
            -ldl
    fi
fi


if [ ! -f "$SHIM_SO" ]; then
    echo "ERROR: path shim was not generated:" >&2
    echo "       $SHIM_SO" >&2
    exit 1
fi


echo "==> Installing WebKit path shim"

install \
    -Dm644 \
    "$SHIM_SO" \
    "$APPDIR/usr/lib/libpath-shim.so"


if [ ! -f "$APPDIR/usr/lib/libpath-shim.so" ]; then
    echo "ERROR: libpath-shim.so was not installed into AppDir." >&2
    exit 1
fi

echo "==> Path shim installed:"
ls -lh "$APPDIR/usr/lib/libpath-shim.so"


# ---------------------------------------------------------------------
# 4. Replace AppRun
# ---------------------------------------------------------------------

# linuxdeploy can rewrite embedded /usr paths into relative ././ paths.
#
# For example WebKit may attempt:
#
#   ././/lib/x86_64-linux-gnu/webkit2gtk-4.1/WebKitNetworkProcess
#
# Those paths resolve correctly when the application's current
# directory is:
#
#   $APPDIR/usr
#
# The LD_PRELOAD shim remains installed for WebKit paths that retain
# their original absolute /usr form.

echo "==> Installing SteamOS-aware AppRun"

cat > "$APPDIR/AppRun" <<'APPRUN'
#!/bin/bash

set -e

HERE="$(dirname "$(readlink -f "${0}")")"

export APPDIR="$HERE"

export LD_LIBRARY_PATH="$APPDIR/usr/lib:$APPDIR/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

export PATH="$APPDIR/usr/bin:$PATH"

# Redirect WebKitGTK's hard-coded Ubuntu helper-process paths into
# the mounted AppImage.
export LD_PRELOAD="$APPDIR/usr/lib/libpath-shim.so${LD_PRELOAD:+:$LD_PRELOAD}"

export GIO_MODULE_DIR="$APPDIR/usr/lib/x86_64-linux-gnu/gio/modules"

# linuxdeploy patches some embedded /usr paths into ././ paths.
# Resolve those relative to AppDir/usr.
cd "$APPDIR/usr"

exec "$APPDIR/usr/bin/saint-controller" "$@"
APPRUN

chmod +x "$APPDIR/AppRun"


# ---------------------------------------------------------------------
# Verify AppRun before sealing image
# ---------------------------------------------------------------------

if ! grep -Fq 'LD_PRELOAD' "$APPDIR/AppRun"; then
    echo "ERROR: AppRun does not configure LD_PRELOAD." >&2
    exit 1
fi

if ! grep -Fq 'libpath-shim.so' "$APPDIR/AppRun"; then
    echo "ERROR: AppRun does not reference libpath-shim.so." >&2
    exit 1
fi

if ! grep -Fq 'cd "$APPDIR/usr"' "$APPDIR/AppRun"; then
    echo 'ERROR: AppRun is missing: cd "$APPDIR/usr"' >&2
    exit 1
fi

if ! grep -Fq 'exec "$APPDIR/usr/bin/saint-controller"' "$APPDIR/AppRun"; then
    echo "ERROR: AppRun does not launch saint-controller." >&2
    exit 1
fi

echo "==> AppRun verified"


# ---------------------------------------------------------------------
# 5. Seal custom AppDir into final AppImage
# ---------------------------------------------------------------------

echo "==> Removing any AppImage created by Tauri/linuxdeploy"

find . \
    -maxdepth 1 \
    -type f \
    -name '*.AppImage' \
    -delete


FINAL_APPIMAGE="SAINT_Controller-x86_64.AppImage"

echo "==> Creating final Steam Deck AppImage:"
echo "    $FINAL_APPIMAGE"

appimagetool \
    "$APPDIR" \
    "$FINAL_APPIMAGE"


if [ ! -f "$FINAL_APPIMAGE" ]; then
    echo "ERROR: appimagetool did not produce:" >&2
    echo "       $FINAL_APPIMAGE" >&2
    exit 1
fi


# ---------------------------------------------------------------------
# Verify final AppImage exists before leaving build directory
# ---------------------------------------------------------------------

echo "==> Final AppImage:"
ls -lh "$FINAL_APPIMAGE"

popd >/dev/null


# =====================================================================
# Stage AppImage for SAINT firmware distribution
# =====================================================================

VERSION="$SAINT_BUILD_VERSION"

FILENAME="saint_firmware_controller_${VERSION}.AppImage"

DEST_DIR="$REPO_ROOT/server/resources/firmware/controller"

APPIMAGE_SRC="$CARGO_TARGET_DIR/release/bundle/appimage/SAINT_Controller-x86_64.AppImage"


if [ ! -f "$APPIMAGE_SRC" ]; then

    echo "ERROR: custom AppImage was not found:" >&2
    echo "       $APPIMAGE_SRC" >&2

    exit 1
fi


# ---------------------------------------------------------------------
# Create firmware staging directory
# ---------------------------------------------------------------------

mkdir -p "$DEST_DIR"


# Remove older controller AppImages from the staging directory.
find "$DEST_DIR" \
    -maxdepth 1 \
    -type f \
    -name '*.AppImage' \
    -delete


echo "==> Staging AppImage:"
echo "    $APPIMAGE_SRC"
echo " -> $DEST_DIR/$FILENAME"

cp \
    "$APPIMAGE_SRC" \
    "$DEST_DIR/$FILENAME"


# ---------------------------------------------------------------------
# Generate firmware metadata
# ---------------------------------------------------------------------

CHECKSUM=$(
    sha256sum "$DEST_DIR/$FILENAME" |
    awk '{print $1}'
)

SIZE=$(
    wc -c < "$DEST_DIR/$FILENAME" |
    tr -d ' '
)

UPDATED=$(
    date -u +%Y-%m-%dT%H:%M:%SZ
)


cat > "$DEST_DIR/info.json" <<EOF
{
    "type": "controller",
    "latest_version": "${VERSION}",
    "latest_package": "${FILENAME}",
    "latest_checksum": "${CHECKSUM}",
    "updated": "${UPDATED}",
    "packages": [
        {
            "version": "${VERSION}",
            "filename": "${FILENAME}",
            "checksum": "${CHECKSUM}",
            "size": ${SIZE}
        }
    ]
}
EOF


# ---------------------------------------------------------------------
# Final verification
# ---------------------------------------------------------------------

if [ ! -f "$DEST_DIR/$FILENAME" ]; then
    echo "ERROR: staged AppImage is missing." >&2
    exit 1
fi

if [ ! -f "$DEST_DIR/info.json" ]; then
    echo "ERROR: info.json was not generated." >&2
    exit 1
fi


echo
echo "================================================================"
echo "SAINT Controller AppImage build complete"
echo "================================================================"
echo
echo "Version:"
echo "    $VERSION"
echo
echo "AppImage:"
echo "    $DEST_DIR/$FILENAME"
echo
echo "SHA256:"
echo "    $CHECKSUM"
echo
echo "Size:"
echo "    $SIZE bytes"
echo
echo "OTA metadata:"
echo "    $DEST_DIR/info.json"
echo
echo "================================================================"