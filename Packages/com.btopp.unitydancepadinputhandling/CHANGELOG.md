# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the package uses [Semantic Versioning](https://semver.org/).

## [0.2.1] - 2026-10-10

### Fixed
- Calibrated pads never reached the game: the bridge queued the virtual Gamepad's state with
  `QueueStateEvent` from `onAfterUpdate`, where the Input System loses the event (the native buffer is
  still being handed back). The bridge now sets the state with `InputState.Change`, like Unity's
  `VirtualMouseInput`.
- In the editor's play mode, the bridge and the calibrator's release check also ran during editor updates,
  which read the editor's separate input state where the pad looks idle. Both now skip editor updates
  while playing.
- Calibration never started: `DancePadCalibrator` probed with an action without bindings, which
  `RebindingOperation.Start()` rejects (`InvalidOperationException`). The editor window and the in-game
  menu kept asking for the first button while nothing was listening. The calibrator now builds the
  rebinding operation itself.
- Input from another device (a second controller, the pad's own virtual Gamepad) silently ended the
  capture instead of being ignored. Only the device being calibrated is considered now.
- A held button was captured for several steps in a row: the capture suppressed the pad's events, so
  the button never showed up as pressed in the device state and every report the pad resends while it
  is held counted as a new press. Events are no longer suppressed, a button counts once it is pressed
  and released again, and buttons captured earlier in the same calibration can't be taken again
  (`BeginCapture(excludedPaths: ...)`).
- Arrows a pad reports on an axis or hat could be captured as the whole axis (`stick/y`, `hat`), so up and
  down would map to the same control. Only button controls (`stick/up`, `hat/up`, `button3` ...) are captured.
- "Skip" cancelled the whole calibration (and in the editor window led to an
  `IndexOutOfRangeException` on the next press). Stopping a capture from code no longer reports a cancel;
  `onCancelled` only fires when the player presses Escape.
- The editor window and the in-game menu report a capture that fails to start instead of waiting forever.
- "Save As New Asset" in the calibration window replaced any asset at the chosen path, keeping its GUID, so
  references to it (e.g. a config) then pointed at the profile. It now refuses paths that hold another asset type.

### Added
- Calibration window: shows which device the last button press came from, with a button to select it.
  The device can no longer be changed while a calibration is running.

## [0.2.0] - 2026-10-07

### Added
- `DancePadManager.AddKnownProfile()` and `KnownProfiles`, so a host can create the manager from code
  and hand it the profiles it ships with.
- `DancePadManager.Instance`: only one manager is active at a time. A second one hands its profiles
  over to the first and disables itself instead of bridging the same pads again (every press would
  otherwise arrive twice).
- `persistAcrossScenes` option: keeps the manager, and every connected pad, alive across scene loads.
  Can also be switched from code at any time (`PersistAcrossScenes`).
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
- The basic usage sample gets the manager through `DancePadManager.Instance` instead of a scene
  reference.

### Fixed
- Pads that were already plugged in at startup could fire `PadConnected`/`UnknownPadDetected` before
  other scripts had subscribed, depending on script order. The manager now runs early
  (`DefaultExecutionOrder(-1000)`, so `Instance` is set before other scripts' `OnEnable`) and does its
  first device scan in `Start`.

## [0.1.0] - 2026-09-12

### Added
- First version: calibration window (editor), calibration menu (in game), mapping profiles as assets
  or saved per machine, `DancePadManager` and `DancePadBridge` that expose a calibrated pad as a
  virtual `Gamepad`.
