using UnityEngine;
using UnityEngine.InputSystem;

namespace SharkBlaster.DancePadInput.Samples
{
    // Drop this next to a DancePadManager in any scene. It shows the whole
    // integration surface a host project needs: react to new/unknown pads,
    // trigger calibration, and read the calibrated pad exactly like any
    // other Gamepad once connected.
    public class DancePadUsageExample : MonoBehaviour
    {
        [SerializeField] private DancePadManager manager;

        private void OnEnable()
        {
            manager.PadConnected += OnPadConnected;
            manager.UnknownPadDetected += OnUnknownPadDetected;
        }

        private void OnDisable()
        {
            manager.PadConnected -= OnPadConnected;
            manager.UnknownPadDetected -= OnUnknownPadDetected;
        }

        private void OnPadConnected(DancePadBridge bridge)
        {
            Debug.Log($"Dance pad ready: {bridge.SourceDevice.displayName} -> {bridge.VirtualDevice.displayName}");
        }

        private void OnUnknownPadDetected(InputDevice rawDevice)
        {
            Debug.Log($"Unknown pad '{rawDevice.displayName}' connected - starting calibration.");
            DancePadCalibrationMenu.Begin(rawDevice, manager,
                onFinished: profile => Debug.Log($"Calibration saved for {profile.deviceProduct}"),
                onCancelled: () => Debug.Log("Calibration cancelled."));
        }

        private void Update()
        {
            // Once calibrated, every connected dance pad shows up as a
            // normal Gamepad - no dance-pad-specific API needed here.
            foreach (var gamepad in Gamepad.all)
            {
                if (!(gamepad is DancePadDevice)) continue;
                if (gamepad.dpad.up.wasPressedThisFrame) Debug.Log("Dance pad: Up");
                if (gamepad.buttonSouth.wasPressedThisFrame) Debug.Log("Dance pad: Symbol 2");
                if (gamepad.startButton.wasPressedThisFrame) Debug.Log("Dance pad: Start");
            }
        }
    }
}
