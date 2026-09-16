# SDL 3.4.16

Official release: https://github.com/libsdl-org/SDL/releases/tag/release-3.4.16
Downloaded 2026-09-12. Archive hashes were verified against the release API's
SHA-256 digests before extraction. The SDL license is included in LICENSE.txt.

| Archive | SHA-256 |
| --- | --- |
| SDL3-3.4.16-win32-x64.zip | 4217944b4e51457af4a59c82d883f8443b3e65964b2acd8943484c492756c4b6 |
| SDL3-3.4.16-win32-arm64.zip | dbd381378164447ce7985e40876e619eda0ec8c14b7694080029ee79aa2a494d |
| SDL3-3.4.16-win32-x86.zip | beb4e86e4a101f66556162c3efff9fd1ad88f1761b63be35867a3336a72f8c41 |

Steam 2026 mapping verified against this release's SDL_hidapi_steam_triton.c and
SDL_gamepad.c. Stable 3.4 does not have the SDL_GAMEPAD_TYPE_STEAM enum added in
later development versions. Detection uses Valve VID, device name, two touchpads
and rear-button capabilities to exclude the older Steam Controller and Steam Deck.
