# Unity Dance Pad Input Handling

Use any USB dance pad in Unity as a regular **Input System gamepad**.

Dance pads don't follow a fixed layout: one model reports the arrows as buttons 0–3, the next as a hat
switch, the next as something else entirely. This package lets you calibrate a pad model once (press
each button when asked). From then on every pad of that model shows up as a virtual `Gamepad`, and your
game reads it through `dpad`, `buttonNorth`, `startButton` and so on, with no dance-pad-specific code.

- Calibration window in the editor: create a profile once and ship it with your game.
- Calibration menu in the game: an operator can calibrate an unknown pad on site, and the result is
  saved on that machine.
- Pads with a known profile connect automatically when they are plugged in.
- Several pads at once: each becomes its own virtual gamepad.

## Requirements

- Unity 6000.0 or newer
- Input System package (installed automatically as a dependency). Under *Project Settings → Player →
  Active Input Handling* choose **Input System Package (New)** or **Both**.

## Installation

*Window → Package Manager → + → Add package from git URL…* and enter:

```
https://github.com/Btopp/UnityDancePadInputHandling.git?path=/Packages/com.btopp.unitydancepadinputhandling#v0.2.0
```

Or add it to `Packages/manifest.json`:

```json
"com.btopp.unitydancepadinputhandling": "https://github.com/Btopp/UnityDancePadInputHandling.git?path=/Packages/com.btopp.unitydancepadinputhandling#v0.2.0"
```

The part after `#` is the version tag. Leave it out to always get the latest state of `main`.

## Quick start

1. **Calibrate your pad.** Plug it in and open *Tools → Unity Dance Pad Input Handling → Calibration
   Window*. Pick the pad, click *Start Calibration* and press each button when asked (Up, Down, Left,
   Right, Symbol 1–4, Start, Select, Special). Buttons your pad doesn't have can be skipped. Then click
   *Save As New Asset*.
2. **Add the manager.** Put a `DancePadManager` on a GameObject in your first scene, drag the profile
   into *Known Profiles* and tick *Persist Across Scenes*.
3. **Read the pad** like any gamepad:

```csharp
using Btopp.UnityDancePadInputHandling;
using UnityEngine;
using UnityEngine.InputSystem;

public class PadReader : MonoBehaviour
{
    void Update()
    {
        foreach (var pad in Gamepad.all)
        {
            if (!(pad is DancePadDevice)) continue;   // only dance pads, not regular controllers
            if (pad.dpad.left.wasPressedThisFrame) Debug.Log("Left");
            if (pad.buttonSouth.wasPressedThisFrame) Debug.Log("Symbol 2");
        }
    }
}
```

Input actions work too. `<Gamepad>/dpad/left` matches dance pads *and* regular controllers, while
`<DancePadDevice>/dpad/left` matches dance pads only.

## Button mapping

| Pad | Virtual gamepad |
|---|---|
| Up, Down, Left, Right | `dpad/up`, `dpad/down`, `dpad/left`, `dpad/right` |
| Symbol 1, 2, 3, 4 | `buttonNorth`, `buttonSouth`, `buttonEast`, `buttonWest` |
| Start, Select | `startButton`, `selectButton` |
| Special (center) | `leftStickButton` |

Which physical button counts as "Symbol 1" is up to whoever calibrates. Pick an order and stick to it
for all your profiles.

## Calibrating in the game

When a pad without a profile is plugged in, the manager fires `UnknownPadDetected`. Start the
calibration menu from there:

```csharp
void OnEnable()
{
    DancePadManager.Instance.UnknownPadDetected += device =>
        DancePadCalibrationMenu.Begin(device, DancePadManager.Instance);
}
```

The menu brings its own canvas and steps through all buttons. *Skip* and *Cancel* are on-screen buttons,
so keep a mouse at hand. The result is saved as JSON under
`Application.persistentDataPath/DancePadProfiles/` and from then on takes precedence over a shipped
profile for that pad model on that machine. To recalibrate a pad that is already connected, pass its
source device:

```csharp
foreach (var bridge in DancePadManager.Instance.ActiveBridges.Values)
    DancePadCalibrationMenu.Begin(bridge.SourceDevice, DancePadManager.Instance);
```

`DancePadProfileStore.DeleteOverride(productName)` removes a saved calibration again.

## Creating the manager from code

```csharp
var go = new GameObject("DancePadManager");
var manager = go.AddComponent<DancePadManager>();
manager.PersistAcrossScenes = true;
foreach (var profile in myProfiles)
    manager.AddKnownProfile(profile);
manager.UnknownPadDetected += OnUnknownPad;
```

The manager looks for pads that are already plugged in only in its `Start`, so profiles and event
handlers set up in the same frame as `AddComponent` are in place in time.

## Options

| Field | Default | Meaning |
|---|---|---|
| Known Profiles | empty | Profiles shipped with the game. A pad matches by product name (and manufacturer, if set), so one profile covers every pad of that model. |
| Report Unknown Gamepads | off | Devices the Input System already knows as a gamepad (Xbox, PlayStation, Switch Pro …) don't fire `UnknownPadDetected`, so plugging in a regular controller doesn't open the calibration menu. Gamepads with a matching profile connect either way. |
| Persist Across Scenes | off | Keeps the manager, and with it every connected pad, alive across scene loads. |

## Good to know

- **Use `DancePadManager.Instance`.** Only one manager is active at a time. A second one, for example
  from loading the first scene again, hands its profiles over to the first and removes itself.
  Otherwise every pad would be bridged twice and every press would arrive twice.
- **One frame of delay.** The virtual pad gets its state one input update after the real one. For
  stepping on a pad that is not noticeable.
- **Pads that report two directions on one axis.** After calibrating, check that each direction got its
  own control path in the calibration window (e.g. `stick/left` and `stick/right`). If two directions
  show the same path, the pad reports them on a single axis, and this version can't tell them apart.

## Working on the package

This repository is a Unity project (6000.3) with the package embedded under
`Packages/com.btopp.unitydancepadinputhandling`. Open the root folder in Unity, and changes to the
package take effect right away. Changes are listed in the
[changelog](Packages/com.btopp.unitydancepadinputhandling/CHANGELOG.md).

## License

[MIT No Attribution](LICENSE): use, change and share it however you like, no credit required.
