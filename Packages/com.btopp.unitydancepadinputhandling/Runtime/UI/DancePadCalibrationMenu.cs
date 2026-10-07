using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Btopp.UnityDancePadInputHandling
{
    // Self-contained, code-built calibration wizard usable inside a running
    // build (no prefab or scene setup needed - it builds its own Canvas and
    // EventSystem on demand). Steps through every DancePadFunction asking
    // the player to press the matching physical button, then saves the
    // result via DancePadProfileStore and hands it to the DancePadManager.
    //
    // This is the in-game counterpart to the editor calibration window -
    // both drive the same DancePadCalibrator capture logic, so a pad model
    // can be (re)calibrated identically whether that happens at dev time or
    // by a player/booth operator in the finished build.
    public class DancePadCalibrationMenu : MonoBehaviour
    {
        private static readonly DancePadFunction[] Steps = (DancePadFunction[])Enum.GetValues(typeof(DancePadFunction));

        private static readonly Dictionary<DancePadFunction, string> DefaultLabels = new Dictionary<DancePadFunction, string>
        {
            { DancePadFunction.Up, "Up" },
            { DancePadFunction.Down, "Down" },
            { DancePadFunction.Left, "Left" },
            { DancePadFunction.Right, "Right" },
            { DancePadFunction.Sym1, "Symbol 1" },
            { DancePadFunction.Sym2, "Symbol 2" },
            { DancePadFunction.Sym3, "Symbol 3" },
            { DancePadFunction.Sym4, "Symbol 4" },
            { DancePadFunction.Start, "Start" },
            { DancePadFunction.Select, "Select" },
            { DancePadFunction.Special, "Special (center)" },
        };

        private InputDevice device;
        private DancePadManager manager;
        private DancePadMappingProfile profile;
        private DancePadCalibrator calibrator;
        private Action<DancePadMappingProfile> onFinished;
        private Action onCancelled;
        private Dictionary<DancePadFunction, string> labels;
        private int stepIndex;

        private Text promptText;
        private Text detailText;

        public static DancePadCalibrationMenu Begin(
            InputDevice device,
            DancePadManager manager,
            Action<DancePadMappingProfile> onFinished = null,
            Action onCancelled = null,
            Dictionary<DancePadFunction, string> labels = null)
        {
            var go = new GameObject("DancePadCalibrationMenu");
            var menu = go.AddComponent<DancePadCalibrationMenu>();
            menu.StartCalibration(device, manager, onFinished, onCancelled, labels);
            return menu;
        }

        private void StartCalibration(
            InputDevice targetDevice,
            DancePadManager targetManager,
            Action<DancePadMappingProfile> finished,
            Action cancelled,
            Dictionary<DancePadFunction, string> customLabels)
        {
            device = targetDevice;
            manager = targetManager;
            onFinished = finished;
            onCancelled = cancelled;
            labels = customLabels ?? DefaultLabels;

            var existing = manager != null ? manager.FindProfileFor(device) : null;
            profile = existing != null ? Instantiate(existing) : ScriptableObject.CreateInstance<DancePadMappingProfile>();
            profile.deviceProduct = device.description.product;
            profile.deviceManufacturer = device.description.manufacturer;

            BuildUi();
            stepIndex = 0;
            RunStep();
        }

        private void RunStep()
        {
            if (stepIndex >= Steps.Length)
            {
                Finish();
                return;
            }

            var function = Steps[stepIndex];
            var label = labels.TryGetValue(function, out var text) ? text : function.ToString();
            promptText.text = $"Press the pad button for:\n{label}";
            detailText.text = $"Step {stepIndex + 1} / {Steps.Length}    Device: {device.displayName}";

            calibrator?.Dispose();
            calibrator = new DancePadCalibrator(device);
            calibrator.BeginCapture(
                onCaptured: path =>
                {
                    profile.SetControlPath(function, path);
                    stepIndex++;
                    RunStep();
                },
                onCancelled: Cancel);
        }

        private void SkipStep()
        {
            stepIndex++;
            RunStep();
        }

        private void Finish()
        {
            DancePadProfileStore.SaveOverride(profile);
            if (manager != null) manager.Connect(device, profile);
            var callback = onFinished;
            var profileResult = profile;
            Cleanup();
            callback?.Invoke(profileResult);
        }

        private void Cancel()
        {
            var callback = onCancelled;
            Cleanup();
            callback?.Invoke();
        }

        private void Cleanup()
        {
            calibrator?.Dispose();
            calibrator = null;
            if (gameObject != null) Destroy(gameObject);
        }

        private void OnDestroy() => calibrator?.Dispose();

        private void BuildUi()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (EventSystem.current == null)
            {
                var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystemGo.transform.SetParent(transform, false);
            }

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(canvasGo.transform, false);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            promptText = CreateText(background.transform, font, 64, TextAnchor.MiddleCenter);
            var promptRect = promptText.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.1f, 0.45f);
            promptRect.anchorMax = new Vector2(0.9f, 0.75f);
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;

            detailText = CreateText(background.transform, font, 28, TextAnchor.MiddleCenter);
            var detailRect = detailText.GetComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0.1f, 0.35f);
            detailRect.anchorMax = new Vector2(0.9f, 0.45f);
            detailRect.offsetMin = Vector2.zero;
            detailRect.offsetMax = Vector2.zero;

            CreateButton(background.transform, font, "Skip", new Vector2(0.35f, 0.15f), new Vector2(0.5f, 0.22f), SkipStep);
            CreateButton(background.transform, font, "Cancel", new Vector2(0.5f, 0.15f), new Vector2(0.65f, 0.22f), Cancel);
        }

        private static Text CreateText(Transform parent, Font font, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            return text;
        }

        private static void CreateButton(Transform parent, Font font, string label, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var go = new GameObject(label + "Button", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick());

            var text = CreateText(go.transform, font, 32, TextAnchor.MiddleCenter);
            text.text = label;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
    }
}
