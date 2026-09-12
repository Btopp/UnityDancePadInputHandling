using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SharkBlaster.DancePadInput
{
    // Main entry point for a host project: drop this in a persistent scene,
    // assign any profiles you've already calibrated (via the editor
    // calibration window) to KnownProfiles, and it will auto-connect
    // matching pads as they're plugged in. Pads with no known profile fire
    // UnknownPadDetected so the host can show the in-game calibration menu
    // (DancePadCalibrationMenu) for that device.
    public class DancePadManager : MonoBehaviour
    {
        [Tooltip("Profiles calibrated ahead of time (editor tool) and shipped with the build.")]
        [SerializeField] private List<DancePadMappingProfile> knownProfiles = new List<DancePadMappingProfile>();

        public event Action<DancePadBridge> PadConnected;
        public event Action<DancePadBridge> PadDisconnected;
        public event Action<InputDevice> UnknownPadDetected;

        private readonly Dictionary<InputDevice, DancePadBridge> activeBridges = new Dictionary<InputDevice, DancePadBridge>();

        public IReadOnlyDictionary<InputDevice, DancePadBridge> ActiveBridges => activeBridges;

        public static bool IsCandidateDevice(InputDevice device)
        {
            if (device is DancePadDevice) return false;
            if (device is Keyboard || device is Mouse || device is Pointer || device is Sensor || device is TrackedDevice) return false;
            return true;
        }

        // Devices currently plugged in that could plausibly be a dance pad -
        // feed this list to a device-picker UI for manual calibration.
        public static IEnumerable<InputDevice> GetCandidateDevices()
        {
            foreach (var device in InputSystem.devices)
                if (IsCandidateDevice(device))
                    yield return device;
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            foreach (var device in InputSystem.devices)
                if (IsCandidateDevice(device))
                    TryAutoConnect(device);
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            foreach (var bridge in activeBridges.Values)
                bridge.Dispose();
            activeBridges.Clear();
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added when IsCandidateDevice(device):
                    TryAutoConnect(device);
                    break;
                case InputDeviceChange.Removed when activeBridges.TryGetValue(device, out var bridge):
                    activeBridges.Remove(device);
                    bridge.Dispose();
                    PadDisconnected?.Invoke(bridge);
                    break;
            }
        }

        private void TryAutoConnect(InputDevice device)
        {
            if (activeBridges.ContainsKey(device)) return;

            var profile = FindProfileFor(device);
            if (profile == null)
            {
                UnknownPadDetected?.Invoke(device);
                return;
            }

            Connect(device, profile);
        }

        // Runtime-saved overrides (in-game recalibration) take precedence
        // over a shipped profile asset, since they reflect a more recent,
        // machine-specific calibration.
        public DancePadMappingProfile FindProfileFor(InputDevice device)
        {
            var product = device.description.product;
            if (DancePadProfileStore.TryLoadOverride(product, out var overrideProfile))
                return overrideProfile;

            foreach (var profile in knownProfiles)
                if (profile != null && profile.Matches(product, device.description.manufacturer))
                    return profile;

            return null;
        }

        public DancePadBridge Connect(InputDevice device, DancePadMappingProfile profile)
        {
            if (profile != null && !profile.IsComplete())
                Debug.LogWarning($"Dance pad profile for '{profile.deviceProduct}' is missing some bindings; those buttons will not respond.");

            if (activeBridges.TryGetValue(device, out var existing))
            {
                existing.ApplyProfile(profile);
                return existing;
            }

            var bridge = new DancePadBridge(device, profile);
            activeBridges[device] = bridge;
            PadConnected?.Invoke(bridge);
            return bridge;
        }

        public void Disconnect(InputDevice device)
        {
            if (!activeBridges.TryGetValue(device, out var bridge)) return;
            activeBridges.Remove(device);
            bridge.Dispose();
            PadDisconnected?.Invoke(bridge);
        }
    }
}
