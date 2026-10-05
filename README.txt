DRESSMAKER AUTOMATION SETTINGS

In-game settings UI for the Windows Dressmaker Automation port.

Controls
  F8: open or close. Escape: close.
  Drag the title to move; drag the lower-right corner to resize.
  Double-click the title to reset the window. Layout is remembered.
  Save & apply writes settings and updates the running mod.
  Set money replaces the balance exactly when Infinite money is OFF.
  Load a game before setting money, then save your game normally.
  Set money also applies pending settings; Cancel does not undo it.

Installation
  Download the release ZIP, extract it, close Dressmaker, run install.bat.
  Run uninstall.bat to restore the files backed up during installation.
  Existing CFG settings and game saves are retained.
  .NET Runtime 6 or newer is required for patching.
  Run diagnose.bat if the installer rejects the game DLL.

Compatibility
  Two inspected Windows game DLL variants and their patched forms are
  supported. Detection uses SHA-256, not just the displayed game version.
  Unknown assemblies are rejected before game files are changed.
  See installer/install.ps1 for exact hashes.
  Other mods or game updates can require additional verification.

Source
  src/        Unity UI, configuration, core bridge and window controls.
  installer/  Installation, rollback, removal and compatibility reports.
  tools/      Bootstrap instrumentation for the original Automation core.
  tests/      Configuration parsing and file preservation checks.

Build
  Requires a .NET SDK supporting netstandard2.1, installed Windows game
  Managed assemblies, the original Automation 1.1.0 core and Windows patcher.
  Those game assemblies are not included in this repository or release.

  powershell -ExecutionPolicy Bypass -File build.ps1 -GameManaged "C:\path\to\Dressmaker_Data\Managed" -OriginalCore "C:\path\to\original\DressmakerAutomation.Core.dll" -PatcherDirectory "C:\path\to\patcher\win"

  Output: artifacts/DressmakerAutomation-UI-Windows-1.0.4.zip
  Configuration tests: dotnet run --project tests/SettingsTests.csproj
  The test project currently targets .NET 10.

Implementation
  Existing core initialization loads the SettingsUI assembly. Original
  automation gameplay method bodies are preserved. UI uses the game's
  Unity UI and TextMeshPro libraries. No BepInEx or Harmony dependency.
  The Windows patcher and original core are upstream dependencies;
  their original source is not represented as part of this UI project.

Validation
  Both supported DLLs: patch hooks, installer/uninstaller round trips,
  prior-mod restoration and changed-file rejection.
  Isolated Unity player: startup/F8 visibility, drag/resize, layout restore,
  live settings, exact balance/HUD updates, normal spending and validation.
  Verified on two users' Windows installations; not every game build.

Credits and upstream links are in NOTICE.txt. License: MIT.
