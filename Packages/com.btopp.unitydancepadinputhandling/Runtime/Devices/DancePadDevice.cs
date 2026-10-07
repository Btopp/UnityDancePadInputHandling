using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace Btopp.UnityDancePadInputHandling
{
    // A virtual gamepad the DancePadBridge drives from a calibrated mapping.
    // Reusing GamepadState/Gamepad means downstream game code just uses
    // Gamepad.current / dpad / buttonNorth / startButton as usual - no
    // dance-pad-specific API required in the consuming project. It is never
    // auto-matched against real hardware; instances are only ever created
    // explicitly by DancePadBridge.
    [InputControlLayout(displayName = "Dance Pad", stateType = typeof(GamepadState))]
    public class DancePadDevice : Gamepad
    {
        private static bool registered;

        public static void EnsureRegistered()
        {
            if (registered) return;
            registered = true;
            InputSystem.RegisterLayout<DancePadDevice>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RuntimeInit() => EnsureRegistered();
    }
}
