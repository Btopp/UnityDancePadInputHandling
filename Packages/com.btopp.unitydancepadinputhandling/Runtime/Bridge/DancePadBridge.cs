using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Btopp.UnityDancePadInputHandling
{
    // Reads the raw pad device every input update and forwards its state,
    // translated through a DancePadMappingProfile, onto a virtual
    // DancePadDevice - so any code elsewhere just sees a normal Gamepad.
    public class DancePadBridge : IDisposable
    {
        private static readonly Dictionary<DancePadFunction, GamepadButton> FunctionToButton = new Dictionary<DancePadFunction, GamepadButton>
        {
            { DancePadFunction.Up, GamepadButton.DpadUp },
            { DancePadFunction.Down, GamepadButton.DpadDown },
            { DancePadFunction.Left, GamepadButton.DpadLeft },
            { DancePadFunction.Right, GamepadButton.DpadRight },
            { DancePadFunction.Sym1, GamepadButton.North },
            { DancePadFunction.Sym2, GamepadButton.South },
            { DancePadFunction.Sym3, GamepadButton.East },
            { DancePadFunction.Sym4, GamepadButton.West },
            { DancePadFunction.Start, GamepadButton.Start },
            { DancePadFunction.Select, GamepadButton.Select },
            // The pad has no analog stick, so the stick-press button is
            // otherwise unused - a natural home for the center/special key.
            { DancePadFunction.Special, GamepadButton.LeftStick },
        };

        public InputDevice SourceDevice { get; }
        public DancePadDevice VirtualDevice { get; private set; }

        private readonly Dictionary<DancePadFunction, InputControl> resolvedControls = new Dictionary<DancePadFunction, InputControl>();
        private GamepadState lastState;
        private bool disposed;

        public DancePadBridge(InputDevice sourceDevice, DancePadMappingProfile profile)
        {
            SourceDevice = sourceDevice ?? throw new ArgumentNullException(nameof(sourceDevice));

            DancePadDevice.EnsureRegistered();
            VirtualDevice = InputSystem.AddDevice<DancePadDevice>($"DancePad ({sourceDevice.displayName})");

            ApplyProfile(profile);
            InputSystem.onAfterUpdate += OnAfterUpdate;
        }

        // Called again after (re)calibration so the running bridge picks up
        // the new mapping without needing to be torn down and recreated.
        public void ApplyProfile(DancePadMappingProfile profile)
        {
            resolvedControls.Clear();
            if (profile == null) return;

            foreach (var binding in profile.bindings)
            {
                if (string.IsNullOrEmpty(binding.controlPath)) continue;
                var control = SourceDevice.TryGetChildControl(binding.controlPath);
                if (control != null) resolvedControls[binding.function] = control;
            }
        }

        private void OnAfterUpdate()
        {
            if (disposed || VirtualDevice == null) return;

            var state = new GamepadState();
            foreach (var entry in resolvedControls)
            {
                if (entry.Value.magnitude < 0.5f) continue;
                state = state.WithButton(FunctionToButton[entry.Key]);
            }

            if (state.buttons == lastState.buttons) return;
            lastState = state;
            InputSystem.QueueStateEvent(VirtualDevice, state);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            InputSystem.onAfterUpdate -= OnAfterUpdate;
            if (VirtualDevice != null && VirtualDevice.added)
                InputSystem.RemoveDevice(VirtualDevice);
            VirtualDevice = null;
        }
    }
}
