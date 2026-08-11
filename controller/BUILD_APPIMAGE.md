# Building the SAINT Controller AppImage

The production Linux package is a single AppImage. You only need the development toolchain on the machine that builds it; the Steam Deck that runs the resulting AppImage does not need Rust, Node.js, or the Tauri development packages.

## Easiest: GitHub Actions

1. Put this `controller` project in a GitHub repository.
2. Open **Actions** in GitHub.
3. Select **Build Steam Controller AppImage**.
4. Choose **Run workflow**.
5. When the job completes, download the `SAINT-Controller-AppImage` artifact.
6. It contains:
   - `SAINT-Controller.AppImage`
   - `SAINT-Controller.AppImage.sha256`

The workflow also runs automatically for tags beginning with `v`, such as `v0.5.1`.

## Build on Debian/Ubuntu

From the project directory:

```bash
chmod +x scripts/build-appimage.sh
./scripts/build-appimage.sh --install-deps
```

The result is copied to:

```text
release/SAINT-Controller.AppImage
```

On later builds, after the dependencies have already been installed:

```bash
./scripts/build-appimage.sh
```

## Install on the Steam Deck

Copy `SAINT-Controller.AppImage` and `install-steamcontroller.sh` to the Deck, then run:

```bash
chmod +x install-steamcontroller.sh
./install-steamcontroller.sh SAINT-Controller.AppImage
```

The installer places the stable executable at:

```text
~/Applications/SAINT-Controller.AppImage
```
