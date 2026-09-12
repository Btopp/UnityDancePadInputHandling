using System;

namespace SharkBlaster.DancePadInput
{
    // One resolved slot: which raw control on the source device maps to a
    // logical function. controlPath is relative to the device root (e.g.
    // "button3", "dpad/up") so it can be re-resolved against any InputDevice
    // instance of the same physical pad, even after a reconnect changes its
    // deviceId. See InputControl.TryGetChildControl.
    [Serializable]
    public struct DancePadBinding
    {
        public DancePadFunction function;
        public string controlPath;
    }
}
