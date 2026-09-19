# Command/timeline splitter crash

The September 18 crash report identifies `Grid.SetFinalSizeMaxDiscrepancy`
throwing `IndexOutOfRangeException` during arrange on .NET 10.0.12. The user
reported that dragging the divider below the embedded URDF's bottom triggered it.
Their executable was the Debug build, version 1.28.0.

A minimal two-row grid reproduced the same exception: total height
618.8771869143831, pixel row 150.04933861850532, and a star row with minimum 120
and maximum 468.8278482958777. Fractional arithmetic leaves a tiny remainder
after the star row resolves to its maximum. WPF's sizing code then accesses a
previously removed constraint index. Microsoft's source inspected:
https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Grid.cs

`FitEditorPanels` now caps pixel rows but leaves star rows' MaxHeight infinite:
star sizing already allocates the remaining space. Pixel drag limits and minimum
timeline heights remain enforced. Layout-mode changes and saved-layout restore
clear previous pixel caps before restoring star sizes. The same finite star-row
cap existed in repository commit 178bdad (the 1.26.7 deployment); the earlier
version working does not isolate a recent feature as the cause.

Validation:
- `--splitter-rounding --baseline` reproduced the original WPF exception.
- `--splitter-rounding`: 10,000 fractional-height cases pass, plus checks for
  pixel limits and transitions back to star sizing (3 assertions).
- `--layout-recovery`: 195 assertions pass, now including repeated splitter
  moves with populated command rows, audio, cursor adorners, controller mappings,
  four viewport sizes, and all three timeline modes. A hidden presentation
  source attaches the visuals without MainWindow's hardware startup.
- Release 1.28.2 and Debug 1.28.3 built successfully, with only the existing
  vent-center field warnings. No full test suite or physical hardware operation.

User projects and saved layout files were not changed.
