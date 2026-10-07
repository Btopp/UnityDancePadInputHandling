# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the package uses [Semantic Versioning](https://semver.org/).

## [0.2.0] - unreleased

### Added
- `DancePadManager.AddKnownProfile()` and `KnownProfiles`, so a host can create the manager from code
  and hand it the profiles it ships with.
- `DancePadManager.Instance`: only one manager is active at a time. A second one hands its profiles
  over to the first and disables itself instead of bridging the same pads again (every press would
  otherwise arrive twice).
- `persistAcrossScenes` option: keeps the manager, and every connected pad, alive across scene loads.
- `reportUnknownGamepads` option (off by default): devices the Input System already recognizes as a
  gamepad (Xbox, PlayStation, Switch Pro ...) no longer fire `UnknownPadDetected`, so plugging in a
  regular controller doesn't open the calibration menu. Gamepads with a matching profile still connect.

### Changed
- **Breaking:** renamed. Package `com.sharkblaster.dancepadinput` is now
  `com.btopp.unitydancepadinputhandling`, namespace `SharkBlaster.DancePadInput` is now
  `Btopp.UnityDancePadInputHandling` (assemblies likewise), menus are under
  "Unity Dance Pad Input Handling". Existing profile assets and components keep working, since Unity
  references scripts by GUID; code using the old namespace and asmdefs referencing the old assembly
  name have to be updated.
- Minimum Unity version is now 6000.0 (required by `com.unity.ugui` 2.0.0; 2021.3 was never valid).

## [0.1.0] - 2026-09-12

### Added
- First version: calibration window (editor), calibration menu (in game), mapping profiles as assets
  or saved per machine, `DancePadManager` and `DancePadBridge` that expose a calibrated pad as a
  virtual `Gamepad`.
