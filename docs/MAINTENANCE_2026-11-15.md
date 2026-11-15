# Maintenance — 2026-11-15

GPU Assign continues to use WinUI 3. Updated the supported .NET 11 / Windows App SDK 2.6.5 dependency graph with official WebView2 1.0.4314.35, Windows AI MachineLearning 2.5.96, SDK BuildTools.MSIX 1.7.261102100 and System.Numerics.Tensors 11.0.0 packages. Added a NuGet lock file, audit failures for NU1901–NU1904, pinned GitHub Actions and weekly Dependabot checks.

## Fixes

- Changing a GPU preference preserves the other fields in Windows' per-app graphics value, including Auto HDR and windowed optimizations.
- Inline GPU changes use the ComboBox's selected index before saving: the WinUI two-way binding can otherwise update the model after the selection handler, leaving the previous value in the file and registry. Inline choices are also localized and errors are shown.
- GPU preference parsing validates complete fields and the supported 0–2 range. Registry backup strings escape quotes and backslashes; imports are restricted to the GPU-preference key.
- Settings pages share current configuration. Writes use a temporary file and replacement, and I/O errors are surfaced. A damaged configuration is not silently replaced by an empty one.
- A discovery failure for one app no longer aborts the whole sync. User regular expressions have a 200 ms match limit; recursive scans skip reparse points and inaccessible directories.
- Sync writes a new assignment before removing old entries. Manual sync creates a backup, with unique backup filenames.
- The app runs as the current user. Newly registered scheduled tasks use limited privileges; when logon-task creation is denied and no existing task remains, startup falls back to the current user's Run key. Task-command failures are reported instead of being treated as success.
- Disabled Release trimming so reflection-based configuration serialization works in published builds. Portable publishing includes the Windows App SDK and no longer depends on a hard-coded Visual Studio installation path.
- Custom WinUI title bar, DPI-aware initial size, theme-aware caption button contrast and readable accessibility labels.

## Validation

`dotnet run --project Tests/CoreRegression -c Release`: 11 checks passed. Production core sources are compiled into the test runner; only WinUI image metadata and Store display-name lookup are stubbed. Tests use unique temporary EXE paths and registry values, remove their fixtures, and compare unrelated GPU settings before and after.

Release self-contained and framework-dependent packages were built. The self-contained application started with the existing personal configuration, containing 45 app definitions. UI checks covered fixture sync (including preservation of Auto HDR), settings persistence, theme and language switching. Existing personal definitions are kept local and are not part of this repository.

The final NuGet query reported no outdated packages or known package vulnerabilities, including transitive dependencies. Hardware-specific GPU choice by a third-party application is outside these regression checks; this tool manages Windows preferences.
