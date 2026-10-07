using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Btopp.UnityDancePadInputHandling
{
    // Main entry point for a host project: drop this in your first scene
    // (tick persistAcrossScenes) or create it from code, assign any profiles
    // you've already calibrated (via the editor calibration window) to
    // KnownProfiles, and it will auto-connect
    // matching pads as they're plugged in. Pads with no known profile fire
    // UnknownPadDetected so the host can show the in-game calibration menu
    // (DancePadCalibrationMenu) for that device.
    public class DancePadManager : MonoBehaviour
    {
        [Tooltip("Profiles calibrated ahead of time (editor tool) and shipped with the build.")]
        [SerializeField] private List<DancePadMappingProfile> knownProfiles = new List<DancePadMappingProfile>();

        [Tooltip("Also fire UnknownPadDetected for devices the Input System already recognizes as a gamepad " +
                 "(Xbox, PlayStation, Switch Pro ...). Off by default, so plugging in a regular controller " +
                 "doesn't open the calibration menu. Gamepads with a matching profile connect either way.")]
        [SerializeField] private bool reportUnknownGamepads;

        [Tooltip("Keep the manager (and with it every connected pad) alive across scene loads.")]
        [SerializeField] private bool persistAcrossScenes;

        // The one active manager. A second one would bridge the same pads
        // again and every press would arrive twice, so it hands its profiles
        // over to this one and disables itself - which also covers a
        // persistent manager's scene being loaded again.
        public static DancePadManager Instance { get; private set; }

        public event Action<DancePadBridge> PadConnected;
        public event Action<DancePadBridge> PadDisconnected;
        public event Action<InputDevice> UnknownPadDetected;

        private readonly Dictionary<InputDevice, DancePadBridge> activeBridges = new Dictionary<InputDevice, DancePadBridge>();

        public IReadOnlyDictionary<InputDevice, DancePadBridge> ActiveBridges => activeBridges;

        public IReadOnlyList<DancePadMappingProfile> KnownProfiles => knownProfiles;

        public bool ReportUnknownGamepads
        {
            get => reportUnknownGamepads;
            set => reportUnknownGamepads = value;
        }

        // Takes effect in Awake: set it before activating a manager created from code.
        public bool PersistAcrossScenes
        {
            get => persistAcrossScenes;
            set => persistAcrossScenes = value;
        }

        // For hosts that create the manager from code instead of a scene.
        // To also get UnknownPadDetected for pads that are already plugged
        // in, add the component to an inactive GameObject, add profiles,
        // subscribe to the events and only then activate it. Profiles added
        // while the manager is running connect matching pads right away.
        public void AddKnownProfile(DancePadMappingProfile profile)
        {
            if (profile == null || knownProfiles.Contains(profile)) return;
            knownProfiles.Add(profile);
            if (!isActiveAndEnabled) return;

            foreach (var device in GetCandidateDevices())
            {
                if (activeBridges.ContainsKey(device)) continue;
                if (profile.Matches(device.description.product, device.description.manufacturer))
                    Connect(device, profile);
            }
        }

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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                foreach (var profile in knownProfiles)
                    Instance.AddKnownProfile(profile);
                // Disabled before OnEnable runs, so this duplicate never
                // touches a device or fires an event. Only the component
                // goes, the GameObject may carry others.
                enabled = false;
                Destroy(this);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Statics survive entering play mode when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

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
                // Generic HID pads come in as Joystick or plain InputDevice;
                // a Gamepad here has a known layout and is almost certainly
                // a regular controller, not an uncalibrated dance pad.
                if (device is Gamepad && !reportUnknownGamepads) return;
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
