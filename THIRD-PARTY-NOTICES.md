# Third-Party Notices

The Johnny 5 Animation Editor includes or depends on the third-party components
listed below. Each remains under its own license and copyright, held by its
respective authors. Nothing in this file alters those terms.

Full license texts for redistributed components are kept alongside the files
themselves, or in the `licenses/` folder where noted.

---

## .NET runtime and libraries

### .NET 10 (Microsoft)

- License: MIT
- https://github.com/dotnet/runtime

The editor targets `net10.0-windows`. If the application is published
self-contained, .NET runtime files are redistributed with it.

### NAudio 2.2.1

- License: MIT
- Copyright (c) Mark Heath and contributors
- https://github.com/naudio/NAudio

Audio file loading, waveform data and playback.

### SkiaSharp.Views.WPF 2.88.8

- License: MIT
- Copyright (c) Microsoft Corporation; SkiaSharp is a binding for Skia
  (Copyright (c) Google Inc., BSD 3-Clause)
- https://github.com/mono/SkiaSharp
- https://github.com/google/skia

Timeline, waveform and spline rendering.

### System.Management 10.0.0

- License: MIT
- Copyright (c) .NET Foundation and Contributors
- https://github.com/dotnet/runtime

Serial device enumeration for hardware discovery.

### System.IO.Ports 10.0.0

- License: MIT
- Copyright (c) .NET Foundation and Contributors
- https://github.com/dotnet/runtime

Serial communication with the Maestro, Arduino and Tic controllers.

---

## Native libraries

### SDL 3.4.16

- License: zlib
- Copyright (C) 1997-2026 Sam Lantinga <slouken@libsdl.org>
- https://github.com/libsdl-org/SDL
- Release: https://github.com/libsdl-org/SDL/releases/tag/release-3.4.16

Game controller input (Xbox, PlayStation, Steam and generic gamepads).

Official release binaries for win32 x64, arm64 and x86 are redistributed
unmodified in `ServoAnimator/Native/SDL3/`. The license text is included there
as `LICENSE.txt`, and `PROVENANCE.md` records the download date and the
SHA-256 digests verified against the official release.

---

## Face tracking

### MediaPipe (Google)

- License: Apache License 2.0
- Copyright (c) Google LLC
- https://github.com/google-ai-edge/mediapipe
- Face Landmarker: https://ai.google.dev/edge/mediapipe/solutions/vision/face_landmarker

Used by the face-tracking helper to produce head pose, facial landmarks and
blendshape values that drive neck and eye animation.

The MediaPipe Python package is installed by the user from PyPI and is not
redistributed with this project. If a Face Landmarker model file (`.task`) or a
bundled helper executable containing MediaPipe is ever distributed with this
repository or its deploy output, the Apache 2.0 license text and any
accompanying `NOTICE` file must be included with it, together with a statement
of any modifications made. Model weights are covered by their own model cards,
which should be consulted before redistribution.

---

## Stream Deck plugin

The Stream Deck XL plugin in `StreamDeckXL/` has its own notices file at
`com.johnny5.animationeditor.sdPlugin/THIRD_PARTY_NOTICES.txt`, which is
distributed inside the packaged plugin.

### ws 8.x

- License: MIT
- Copyright (c) 2011 Einar Otto Stangvik, (c) 2013 Arnout Kazemier and
  contributors, (c) 2016 Luigi Pinca and contributors
- https://github.com/websockets/ws

WebSocket communication between the plugin and the editor. Bundled into the
packaged plugin.

### Build-time only (not redistributed)

These are used to build, test and package the plugin and are not included in
the shipped plugin or the editor:

| Package | License | Project |
| --- | --- | --- |
| @elgato/cli | MIT | https://github.com/elgatosf/cli |
| esbuild | MIT | https://github.com/evanw/esbuild |
| playwright | Apache 2.0 | https://github.com/microsoft/playwright |
| sharp | Apache 2.0 | https://github.com/lovell/sharp |

---

## Notes

- Versions above reflect the project files at the time this notice was written.
  Update them when dependencies change.
- Hardware protocols for Pololu Maestro, Pololu Tic and Arduino devices are
  implemented in this project's own code from published documentation; no
  vendor source code is included.
