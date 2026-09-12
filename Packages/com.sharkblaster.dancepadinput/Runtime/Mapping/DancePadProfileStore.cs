using System.IO;
using System.Linq;
using UnityEngine;

namespace SharkBlaster.DancePadInput
{
    // Persists mapping profiles created/edited at runtime (in-game
    // calibration menu) to disk, keyed by device product name, so a pad
    // calibrated once on a machine stays calibrated across future runs
    // without needing a shipped, checked-in profile asset for it.
    public static class DancePadProfileStore
    {
        private static string DirectoryPath => Path.Combine(Application.persistentDataPath, "DancePadProfiles");

        public static string GetOverridePath(string deviceProduct)
        {
            return Path.Combine(DirectoryPath, SanitizeFileName(deviceProduct) + ".json");
        }

        public static bool TryLoadOverride(string deviceProduct, out DancePadMappingProfile profile)
        {
            var path = GetOverridePath(deviceProduct);
            if (!File.Exists(path))
            {
                profile = null;
                return false;
            }

            profile = ScriptableObject.CreateInstance<DancePadMappingProfile>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), profile);
            return true;
        }

        public static void SaveOverride(DancePadMappingProfile profile)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(GetOverridePath(profile.deviceProduct), JsonUtility.ToJson(profile));
        }

        public static void DeleteOverride(string deviceProduct)
        {
            var path = GetOverridePath(deviceProduct);
            if (File.Exists(path)) File.Delete(path);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
