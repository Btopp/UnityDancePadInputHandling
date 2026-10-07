using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Btopp.UnityDancePadInputHandling.Editor
{
    // Dev-time counterpart to DancePadCalibrationMenu: create/edit a
    // DancePadMappingProfile asset for a specific pad model without
    // entering Play Mode, so it can be checked into the project and shipped
    // with the build (DancePadManager.KnownProfiles).
    public class DancePadCalibrationWindow : EditorWindow
    {
        private static readonly DancePadFunction[] Steps = (DancePadFunction[])Enum.GetValues(typeof(DancePadFunction));

        private InputDevice[] candidateDevices = Array.Empty<InputDevice>();
        private int selectedDeviceIndex;
        private DancePadMappingProfile existingProfileAsset;
        private DancePadMappingProfile workingProfile;
        private DancePadCalibrator calibrator;
        private int stepIndex = -1;
        private string statusMessage = "";

        [MenuItem("Tools/Unity Dance Pad Input Handling/Calibration Window")]
        public static void Open()
        {
            var window = GetWindow<DancePadCalibrationWindow>(true, "Dance Pad Calibration", true);
            window.RefreshDevices();
        }

        private void OnEnable()
        {
            DancePadDevice.EnsureRegistered();
            RefreshDevices();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            calibrator?.Dispose();
            calibrator = null;
        }

        private void OnEditorUpdate()
        {
            // Repaint continuously while capturing so the pad's live input
            // (visible in the Input Debugger too) feels responsive here.
            if (calibrator != null) Repaint();
        }

        private void RefreshDevices()
        {
            candidateDevices = DancePadManager.GetCandidateDevices().ToArray();
            if (selectedDeviceIndex >= candidateDevices.Length) selectedDeviceIndex = 0;
        }

        private InputDevice SelectedDevice =>
            candidateDevices.Length > 0 && selectedDeviceIndex >= 0 && selectedDeviceIndex < candidateDevices.Length
                ? candidateDevices[selectedDeviceIndex]
                : null;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("1. Pick the device to calibrate", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var names = candidateDevices
                    .Select(d => $"{d.displayName} [{d.description.product}]")
                    .DefaultIfEmpty("<no candidate devices connected>")
                    .ToArray();
                using (new EditorGUI.DisabledScope(candidateDevices.Length == 0))
                    selectedDeviceIndex = EditorGUILayout.Popup(selectedDeviceIndex, names);
                if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshDevices();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2. Optional: re-calibrate an existing profile", EditorStyles.boldLabel);
            existingProfileAsset = (DancePadMappingProfile)EditorGUILayout.ObjectField(
                "Existing Profile", existingProfileAsset, typeof(DancePadMappingProfile), false);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(SelectedDevice == null || stepIndex >= 0))
            {
                if (GUILayout.Button("3. Start Calibration", GUILayout.Height(28)))
                    StartCalibration();
            }

            EditorGUILayout.Space();
            if (stepIndex >= 0) DrawCalibrationStep();
            else if (workingProfile != null) DrawSaveOptions();

            if (!string.IsNullOrEmpty(statusMessage))
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }

        private void DrawSaveOptions()
        {
            EditorGUILayout.LabelField("4. Save the calibrated profile", EditorStyles.boldLabel);
            for (var i = 0; i < Steps.Length; i++)
            {
                var has = workingProfile.TryGetControlPath(Steps[i], out var path);
                EditorGUILayout.LabelField(Steps[i].ToString(), has ? path : "<not set>");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save As New Asset")) SaveAsNewAsset();
                using (new EditorGUI.DisabledScope(existingProfileAsset == null))
                {
                    if (GUILayout.Button("Overwrite Existing Asset")) OverwriteExistingAsset();
                }
            }
        }

        private void StartCalibration()
        {
            var device = SelectedDevice;
            workingProfile = existingProfileAsset != null
                ? Instantiate(existingProfileAsset)
                : ScriptableObject.CreateInstance<DancePadMappingProfile>();
            workingProfile.deviceProduct = device.description.product;
            workingProfile.deviceManufacturer = device.description.manufacturer;

            stepIndex = 0;
            statusMessage = "";
            BeginCaptureForCurrentStep();
        }

        private void BeginCaptureForCurrentStep()
        {
            calibrator?.Dispose();
            calibrator = new DancePadCalibrator(SelectedDevice);
            calibrator.BeginCapture(
                onCaptured: path =>
                {
                    workingProfile.SetControlPath(Steps[stepIndex], path);
                    stepIndex++;
                    if (stepIndex >= Steps.Length)
                    {
                        calibrator?.Dispose();
                        calibrator = null;
                        stepIndex = -1;
                        statusMessage = "Calibration complete. Save the profile below.";
                    }
                    else
                    {
                        BeginCaptureForCurrentStep();
                    }
                    Repaint();
                },
                onCancelled: CancelCalibration);
        }

        private void CancelCalibration()
        {
            calibrator?.Dispose();
            calibrator = null;
            stepIndex = -1;
            statusMessage = "Calibration cancelled.";
            Repaint();
        }

        private void DrawCalibrationStep()
        {
            var function = Steps[stepIndex];
            EditorGUILayout.HelpBox($"Press the pad button for: {function}\n(Step {stepIndex + 1} / {Steps.Length})", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Skip"))
                {
                    stepIndex++;
                    if (stepIndex >= Steps.Length)
                    {
                        calibrator?.Dispose();
                        calibrator = null;
                        stepIndex = -1;
                        statusMessage = "Calibration complete (some steps skipped). Save the profile below.";
                    }
                    else
                    {
                        BeginCaptureForCurrentStep();
                    }
                }
                if (GUILayout.Button("Cancel")) CancelCalibration();
            }
        }

        private void SaveAsNewAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Dance Pad Profile", workingProfile.deviceProduct + "Profile", "asset",
                "Choose where to save the calibrated profile.");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<DancePadMappingProfile>();
            asset.CopyFrom(workingProfile);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            existingProfileAsset = asset;
            statusMessage = $"Saved to {path}";
        }

        private void OverwriteExistingAsset()
        {
            if (existingProfileAsset == null) return;
            Undo.RecordObject(existingProfileAsset, "Recalibrate Dance Pad Profile");
            existingProfileAsset.CopyFrom(workingProfile);
            EditorUtility.SetDirty(existingProfileAsset);
            AssetDatabase.SaveAssets();
            statusMessage = $"Overwrote {AssetDatabase.GetAssetPath(existingProfileAsset)}";
        }
    }
}
