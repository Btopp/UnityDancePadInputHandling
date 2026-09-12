using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SharkBlaster.DancePadInput
{
    // One calibrated dance pad model. deviceProduct/deviceManufacturer match
    // InputDevice.description so a shipped profile (created once via the
    // editor calibration window) or a runtime override (created via the
    // in-game calibration menu) auto-applies whenever a matching pad is
    // plugged in again, without recalibrating.
    [CreateAssetMenu(fileName = "DancePadProfile", menuName = "Dance Pad Input/Mapping Profile")]
    public class DancePadMappingProfile : ScriptableObject
    {
        public string deviceProduct;
        public string deviceManufacturer;
        public List<DancePadBinding> bindings = new List<DancePadBinding>();

        public bool Matches(string product, string manufacturer)
        {
            if (string.IsNullOrEmpty(deviceProduct)) return false;
            if (!string.Equals(deviceProduct, product, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrEmpty(deviceManufacturer)) return true;
            return string.Equals(deviceManufacturer, manufacturer, StringComparison.OrdinalIgnoreCase);
        }

        public bool TryGetControlPath(DancePadFunction function, out string controlPath)
        {
            foreach (var binding in bindings)
            {
                if (binding.function != function) continue;
                controlPath = binding.controlPath;
                return true;
            }
            controlPath = null;
            return false;
        }

        public void SetControlPath(DancePadFunction function, string controlPath)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].function != function) continue;
                var binding = bindings[i];
                binding.controlPath = controlPath;
                bindings[i] = binding;
                return;
            }
            bindings.Add(new DancePadBinding { function = function, controlPath = controlPath });
        }

        public bool IsComplete()
        {
            var functions = (DancePadFunction[])Enum.GetValues(typeof(DancePadFunction));
            return functions.All(f => TryGetControlPath(f, out _));
        }

        public void CopyFrom(DancePadMappingProfile other)
        {
            deviceProduct = other.deviceProduct;
            deviceManufacturer = other.deviceManufacturer;
            bindings = new List<DancePadBinding>(other.bindings);
        }
    }
}
