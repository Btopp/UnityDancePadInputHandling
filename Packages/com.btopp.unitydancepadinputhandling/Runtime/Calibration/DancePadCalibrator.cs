using System;
using UnityEngine.InputSystem;

namespace Btopp.UnityDancePadInputHandling
{
    // Captures "whichever control on the target device just got actuated",
    // regardless of whether the pad reports a given button as a digital
    // button, a POV/hat segment, or an axis - which is exactly the point,
    // since dance pads don't follow any fixed layout. Drives both the
    // editor calibration window and the in-game calibration menu, since
    // PerformInteractiveRebinding works identically in and out of play mode.
    public class DancePadCalibrator : IDisposable
    {
        private readonly InputDevice sourceDevice;
        private InputAction probeAction;
        private InputActionRebindingExtensions.RebindingOperation operation;

        public bool IsCapturing => operation != null;

        public DancePadCalibrator(InputDevice sourceDevice)
        {
            this.sourceDevice = sourceDevice;
        }

        // magnitudeThreshold: how far a control has to move before it counts
        // as "this is the button being pressed" - keep it fairly high so
        // idle jitter on unrelated axes doesn't get captured by mistake.
        public void BeginCapture(Action<string> onCaptured, Action onCancelled = null, float magnitudeThreshold = 0.5f)
        {
            CancelInternal();

            probeAction = new InputAction();
            operation = probeAction.PerformInteractiveRebinding()
                .WithControlsExcluding("<Keyboard>")
                .WithControlsExcluding("<Mouse>")
                .WithControlsExcluding("<Pointer>")
                .WithMagnitudeHavingToBeGreaterThan(magnitudeThreshold)
                .OnMatchWaitForAnother(0.05f)
                .OnApplyBinding((op, generatedPath) =>
                {
                    var control = op.selectedControl;
                    if (control == null || control.device != sourceDevice)
                    {
                        // Ignore input from anything other than the pad
                        // being calibrated and keep listening.
                        op.Start();
                        return;
                    }

                    var relativePath = control.path.Substring(control.device.path.Length + 1);
                    CleanUp();
                    onCaptured?.Invoke(relativePath);
                })
                .OnCancel(_ =>
                {
                    CleanUp();
                    onCancelled?.Invoke();
                })
                .Start();
        }

        public void Cancel() => CancelInternal();

        private void CancelInternal()
        {
            operation?.Cancel();
            CleanUp();
        }

        private void CleanUp()
        {
            operation?.Dispose();
            operation = null;
            probeAction?.Dispose();
            probeAction = null;
        }

        public void Dispose() => CancelInternal();
    }
}
