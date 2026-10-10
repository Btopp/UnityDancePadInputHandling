using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Btopp.UnityDancePadInputHandling
{
    // Captures "whichever control on the target device just got actuated",
    // regardless of whether the pad reports a given button as a digital
    // button, a POV/hat segment, or an axis - which is exactly the point,
    // since dance pads don't follow any fixed layout. Drives both the
    // editor calibration window and the in-game calibration menu, since a
    // RebindingOperation works identically in and out of play mode.
    //
    // A control counts once it is pressed and released again: many pads
    // resend a held button several times a second, and the next step must
    // not pick up the same press.
    public class DancePadCalibrator : IDisposable
    {
        private readonly InputDevice sourceDevice;
        private InputActionRebindingExtensions.RebindingOperation operation;
        private Action<string> onCaptured;
        private Action onCancelled;
        private float threshold;
        private readonly HashSet<string> excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pressed, waiting for its release.
        private InputControl pressedControl;
        private bool afterUpdateHooked;

        public bool IsCapturing => operation != null;

        public DancePadCalibrator(InputDevice sourceDevice)
        {
            this.sourceDevice = sourceDevice ?? throw new ArgumentNullException(nameof(sourceDevice));
        }

        // magnitudeThreshold: how far a control has to move before it counts
        // as "this is the button being pressed" - keep it fairly high so
        // idle jitter on unrelated axes doesn't get captured by mistake.
        //
        // excludedPaths: controls (relative paths, as passed to onCaptured)
        // that are already taken, e.g. by earlier steps of the same
        // calibration. Pressing them does nothing.
        //
        // onCancelled only fires when the player cancels (Escape). Stopping
        // the capture from code (Cancel, Dispose, starting the next capture)
        // is silent, so a host moving on to the next step doesn't get told
        // the calibration was cancelled.
        public void BeginCapture(Action<string> onCaptured, Action onCancelled = null, float magnitudeThreshold = 0.5f,
            IEnumerable<string> excludedPaths = null)
        {
            Stop();
            this.onCaptured = onCaptured;
            this.onCancelled = onCancelled;
            threshold = magnitudeThreshold;
            this.excludedPaths.Clear();
            if (excludedPaths != null)
                this.excludedPaths.UnionWith(excludedPaths);

            // Built directly instead of through InputAction.PerformInteractiveRebinding:
            // that needs an action with at least one binding, otherwise Start() throws.
            // Matching events are not suppressed: a suppressed event never
            // reaches the device state, so a held button would look released
            // and every repeated report of it would count as a new press.
            operation = new InputActionRebindingExtensions.RebindingOperation()
                // Buttons only: a pad arrow reported on an axis or a hat then
                // comes in as "stick/up" or "hat/up" instead of "stick/y" or
                // "hat", which would also fire for the opposite direction.
                .WithExpectedControlType<ButtonControl>()
                .WithControlsExcluding("<Keyboard>")
                .WithControlsExcluding("<Mouse>")
                .WithControlsExcluding("<Pointer>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithMagnitudeHavingToBeGreaterThan(magnitudeThreshold)
                // Matches are picked in OnPotentialMatch, so input from other
                // devices (another controller, the pad's own virtual Gamepad)
                // is dropped there instead of ending the capture.
                .OnPotentialMatch(OnPotentialMatch)
                // Start() requires an apply callback when there is no action;
                // never called, since the operation is never completed.
                .OnApplyBinding((_, __) => { })
                .OnCancel(OnOperationCancelled);
            operation.Start();
        }

        public void Cancel() => Stop();

        public void Dispose() => Stop();

        private string RelativePath(InputControl control) => control.path.Substring(sourceDevice.path.Length + 1);

        private void OnPotentialMatch(InputActionRebindingExtensions.RebindingOperation op)
        {
            if (op != operation) return;

            // Candidates are sorted by score, best first. The operation only
            // finds the press; the pressed control is tracked here, so the
            // candidates are cleared every time.
            foreach (var control in op.candidates.ToArray())
            {
                op.RemoveCandidate(control);
                if (pressedControl != null || control.device != sourceDevice) continue;
                if (excludedPaths.Contains(RelativePath(control))) continue;
                pressedControl = control;
            }

            if (pressedControl != null && !afterUpdateHooked)
            {
                InputSystem.onAfterUpdate += OnAfterUpdate;
                afterUpdateHooked = true;
            }
        }

        private void OnAfterUpdate()
        {
            if (pressedControl == null || DancePadBridge.ReadsEditorStateInPlayMode()) return;
            if (pressedControl.magnitude >= threshold) return;

            var relativePath = RelativePath(pressedControl);
            var callback = onCaptured;
            Stop();
            callback?.Invoke(relativePath);
        }

        private void OnOperationCancelled(InputActionRebindingExtensions.RebindingOperation op)
        {
            // Stop() cancels the operation too, but clears it first.
            if (op != operation) return;
            var callback = onCancelled;
            Stop();
            callback?.Invoke();
        }

        // Safe from inside the operation's own callbacks: the operation
        // touches nothing it has disposed on the way out.
        private void Stop()
        {
            if (afterUpdateHooked)
            {
                InputSystem.onAfterUpdate -= OnAfterUpdate;
                afterUpdateHooked = false;
            }
            pressedControl = null;

            var op = operation;
            operation = null;
            onCaptured = null;
            onCancelled = null;
            if (op == null) return;
            op.Cancel();
            op.Dispose();
        }
    }
}
