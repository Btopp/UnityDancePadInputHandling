using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
        private MessageType statusType = MessageType.Info;
        private InputDevice lastPressedDevice;
        // Controls captured in this run; the next steps can't take them again.
        private readonly List<string> capturedPaths = new List<string>();

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
            InputSystem.onEvent += OnInputEvent;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            InputSystem.onEvent -= OnInputEvent;
            InputSystem.onDeviceChange -= OnDeviceChange;
            calibrator?.Dispose();
            calibrator = null;
        }

        private void OnEditorUpdate()
        {
            // Repaint continuously while capturing so the pad's live input
            // (visible in the Input Debugger too) feels responsive here.
            if (calibrator != null) Repaint();
        }

        // Shows which device a button press came from, so the pad can be
        // told apart from other controllers by just stepping on it. Axes
        // count too: many pads report their arrows on a stick.
        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device == lastPressedDevice || !DancePadManager.IsCandidateDevice(device)) return;
            if (!eventPtr.HasButtonPress(0.5f, buttonControlsOnly: false)) return;
            lastPressedDevice = device;
            Repaint();
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Added && change != InputDeviceChange.Removed) return;
            if (change == InputDeviceChange.Removed && device == lastPressedDevice) lastPressedDevice = null;
            // Keep the list as is while calibrating, the selected index must not shift.
            if (stepIndex < 0) RefreshDevices();
            Repaint();
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
                using (new EditorGUI.DisabledScope(candidateDevices.Length == 0 || stepIndex >= 0))
                    selectedDeviceIndex = EditorGUILayout.Popup(selectedDeviceIndex, names);
                using (new EditorGUI.DisabledScope(stepIndex >= 0))
                {
                    if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshDevices();
                }
            }
            DrawLastPressedDevice();

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
                EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        private void DrawLastPressedDevice()
        {
            if (lastPressedDevice == null)
            {
                EditorGUILayout.HelpBox("Step on the pad to see which device it is.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Last button press from",
                    $"{lastPressedDevice.displayName} [{lastPressedDevice.description.product}]");
                var index = Array.IndexOf(candidateDevices, lastPressedDevice);
                using (new EditorGUI.DisabledScope(index < 0 || index == selectedDeviceIndex || stepIndex >= 0))
                {
                    if (GUILayout.Button("Select", GUILayout.Width(70))) selectedDeviceIndex = index;
                }
            }
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
            capturedPaths.Clear();
            SetStatus("");
            BeginCaptureForCurrentStep();
        }

        private void SetStatus(string message, MessageType type = MessageType.Info)
        {
            statusMessage = message;
            statusType = type;
        }

        private void BeginCaptureForCurrentStep()
        {
            calibrator?.Dispose();
            calibrator = new DancePadCalibrator(SelectedDevice);
            try
            {
                StartCapture();
            }
            catch (Exception e)
            {
                // Otherwise the window keeps asking for a button nobody listens for.
                Debug.LogException(e);
                CancelCalibration();
                SetStatus($"Calibration could not start: {e.Message}", MessageType.Error);
            }
        }

        private void StartCapture()
        {
            calibrator.BeginCapture(
                onCaptured: path =>
                {
                    workingProfile.SetControlPath(Steps[stepIndex], path);
                    capturedPaths.Add(path);
                    stepIndex++;
                    if (stepIndex >= Steps.Length)
                    {
                        calibrator?.Dispose();
                        calibrator = null;
                        stepIndex = -1;
                        SetStatus("Calibration complete. Save the profile below.");
                    }
                    else
                    {
                        BeginCaptureForCurrentStep();
                    }
                    Repaint();
                },
                onCancelled: CancelCalibration,
                excludedPaths: capturedPaths);
        }

        private void CancelCalibration()
        {
            calibrator?.Dispose();
            calibrator = null;
            stepIndex = -1;
            SetStatus("Calibration cancelled.");
            Repaint();
        }

        private void DrawCalibrationStep()
        {
            var function = Steps[stepIndex];
            EditorGUILayout.HelpBox($"Press and release the pad button for: {function}\n(Step {stepIndex + 1} / {Steps.Length}, Escape cancels)", MessageType.Warning);
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
                        SetStatus("Calibration complete (some steps skipped). Save the profile below.");
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

            // CreateAsset replaces whatever is at that path and keeps its GUID,
            // so every reference to e.g. a config asset would silently point
            // at this profile instead.
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is DancePadMappingProfile))
            {
                SetStatus($"Not saved: {path} is a {existing.GetType().Name}, not a dance pad profile. Choose another file name.",
                    MessageType.Error);
                return;
            }

            var asset = CreateInstance<DancePadMappingProfile>();
            asset.CopyFrom(workingProfile);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            existingProfileAsset = asset;
            SetStatus($"Saved to {path}");
        }

        private void OverwriteExistingAsset()
        {
            if (existingProfileAsset == null) return;
            Undo.RecordObject(existingProfileAsset, "Recalibrate Dance Pad Profile");
            existingProfileAsset.CopyFrom(workingProfile);
            EditorUtility.SetDirty(existingProfileAsset);
            AssetDatabase.SaveAssets();
            SetStatus($"Overwrote {AssetDatabase.GetAssetPath(existingProfileAsset)}");
        }
    }
}
