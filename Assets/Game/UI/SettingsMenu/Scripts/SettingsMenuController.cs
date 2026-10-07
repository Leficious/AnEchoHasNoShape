using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AnEchoHasNoShape.Interaction;

namespace AnEchoHasNoShape.UI
{
    /// <summary>
    /// Compact pause/settings menu. The UI is generated at runtime so it works in
    /// every playable scene without modifying scene or controller prefab assets.
    /// </summary>
    public sealed class SettingsMenuController : MonoBehaviour
    {
        private const string VolumePreference = "AEHNS.Settings.Volume";
        private const string SensitivityPreference = "AEHNS.Settings.Sensitivity";
        private const string DisplayModePreference = "AEHNS.Settings.DisplayMode";

        private static readonly Color BackdropColor = new Color(0.018f, 0.027f, 0.039f, 0.78f);
        private static readonly Color PanelColor = new Color(0.055f, 0.071f, 0.086f, 0.98f);
        private static readonly Color ControlColor = new Color(0.105f, 0.129f, 0.145f, 1f);
        private static readonly Color AccentColor = new Color(0.78f, 0.70f, 0.51f, 1f);
        private static readonly Color TextColor = new Color(0.91f, 0.89f, 0.83f, 1f);
        private static readonly Color MutedTextColor = new Color(0.63f, 0.65f, 0.63f, 1f);

        private GameObject menuRoot;
        private FirstPersonController firstPersonController;
        private Font headerFont;
        private Font bodyFont;
        private Text volumeValueText;
        private Text sensitivityValueText;
        private Dropdown modeDropdown;

        private bool isOpen;
        private bool previousCameraCanMove;
        private bool previousPlayerCanMove;
        private float previousTimeScale = 1f;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;

        private float volume;
        private float sensitivity;
        private int displayMode;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            Font fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            headerFont = gameManager != null && gameManager.HeaderFont != null
                ? gameManager.HeaderFont
                : fallbackFont;
            bodyFont = gameManager != null && gameManager.BodyFont != null
                ? gameManager.BodyFont
                : fallbackFont;
            FindPlayerController();
            LoadAndApplySettings();
            EnsureEventSystem();
            BuildMenu();
            menuRoot.SetActive(false);
        }

        private void Update()
        {
            // The development console owns Escape while it is open. Checking both
            // states keeps this independent of Unity's script execution order:
            // either the console is still open, or it already consumed this frame.
            if (RuntimeDebugConsole.IsOpen || RuntimeDebugConsole.ConsumedEscapeThisFrame ||
                ConfirmationPromptUI.IsOpen || ConfirmationPromptUI.ConsumedEscapeThisFrame)
            {
                return;
            }

            if (InteractionTextPanel.Instance != null && InteractionTextPanel.Instance.IsOpen)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (isOpen)
                {
                    Resume();
                }
                else
                {
                    Open();
                }
            }
        }

        private void OnDestroy()
        {
            if (isOpen)
            {
                RestoreGameplayState();
            }
        }

        public void Open()
        {
            if (isOpen)
            {
                return;
            }

            FindPlayerController();
            previousTimeScale = Time.timeScale;
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;

            if (firstPersonController != null)
            {
                previousCameraCanMove = firstPersonController.cameraCanMove;
                previousPlayerCanMove = firstPersonController.playerCanMove;
                firstPersonController.cameraCanMove = false;
                firstPersonController.playerCanMove = false;
            }

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            menuRoot.SetActive(true);
            isOpen = true;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void Resume()
        {
            if (!isOpen)
            {
                return;
            }

            SaveSettings();
            menuRoot.SetActive(false);
            RestoreGameplayState();
            isOpen = false;
        }

        private void Unstuck()
        {
            FindPlayerController();
            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            if (gameManager != null && gameManager.TeleportPlayerToCheckpoint(firstPersonController))
            {
                Resume();
            }
        }

        private void Quit()
        {
            SaveSettings();
            Time.timeScale = 1f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RestoreGameplayState()
        {
            Time.timeScale = previousTimeScale;

            if (firstPersonController != null)
            {
                firstPersonController.cameraCanMove = previousCameraCanMove;
                firstPersonController.playerCanMove = previousPlayerCanMove;
            }

            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
        }

        private void FindPlayerController()
        {
            if (firstPersonController == null)
            {
                firstPersonController = FindAnyObjectByType<FirstPersonController>();
            }
        }

        private void LoadAndApplySettings()
        {
            float controllerDefault = firstPersonController != null
                ? firstPersonController.mouseSensitivity
                : 2f;

            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, 1f));
            sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityPreference, controllerDefault), 0.2f, 8f);
            displayMode = PlayerPrefs.GetInt(
                DisplayModePreference,
                Screen.fullScreenMode == FullScreenMode.Windowed ? 1 : 0);

            AudioListener.volume = volume;
            ApplySensitivity(sensitivity);

            if (PlayerPrefs.HasKey(DisplayModePreference))
            {
                ApplyDisplayMode(displayMode);
            }
        }

        private void ApplyVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            AudioListener.volume = volume;

            if (volumeValueText != null)
            {
                volumeValueText.text = Mathf.RoundToInt(volume * 100f) + "%";
            }
        }

        private void ApplySensitivity(float newSensitivity)
        {
            sensitivity = Mathf.Clamp(newSensitivity, 0.2f, 8f);
            FindPlayerController();

            if (firstPersonController != null)
            {
                firstPersonController.mouseSensitivity = sensitivity;
            }

            if (sensitivityValueText != null)
            {
                sensitivityValueText.text = sensitivity.ToString("0.0");
            }
        }

        private void ApplyDisplayMode(int mode)
        {
            displayMode = Mathf.Clamp(mode, 0, 1);
            Screen.fullScreenMode = displayMode == 0
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(VolumePreference, volume);
            PlayerPrefs.SetFloat(SensitivityPreference, sensitivity);
            PlayerPrefs.SetInt(DisplayModePreference, displayMode);
            PlayerPrefs.Save();
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("Settings Menu EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private void BuildMenu()
        {
            GameObject canvasObject = new GameObject("Settings Menu Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            menuRoot = CreateRect("Menu Root", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            Image backdrop = menuRoot.AddComponent<Image>();
            backdrop.color = BackdropColor;

            RectTransform panel = CreateRect(
                "Panel",
                menuRoot.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(460f, 570f));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;

            CreateText("Title", panel, "AN ECHO HAS NO SHAPE", 27, FontStyle.Normal,
                TextAnchor.MiddleCenter, TextColor, new Vector2(0f, 238f), new Vector2(400f, 42f), true);
            CreateText("Subtitle", panel, "PAUSED", 12, FontStyle.Normal,
                TextAnchor.MiddleCenter, MutedTextColor, new Vector2(0f, 210f), new Vector2(400f, 24f));

            CreateButton("Resume", panel, "RESUME", new Vector2(0f, 165f), Resume);
            CreateButton("Unstuck", panel, "UNSTUCK", new Vector2(0f, 117f), Unstuck);

            CreateText("Mode Label", panel, "MODE", 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, MutedTextColor, new Vector2(-100f, 14f), new Vector2(100f, 26f));
            modeDropdown = CreateDropdown(panel, new Vector2(55f, 14f));
            modeDropdown.SetValueWithoutNotify(displayMode);
            modeDropdown.onValueChanged.AddListener(ApplyDisplayMode);

            CreateText("Volume Label", panel, "VOLUME", 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, MutedTextColor, new Vector2(-60f, -42f), new Vector2(180f, 26f));
            volumeValueText = CreateText("Volume Value", panel, Mathf.RoundToInt(volume * 100f) + "%", 13,
                FontStyle.Normal, TextAnchor.MiddleRight, TextColor, new Vector2(115f, -42f), new Vector2(70f, 26f));
            Slider volumeSlider = CreateSlider("Volume Slider", panel, new Vector2(0f, -73f), 0f, 1f, volume);
            volumeSlider.onValueChanged.AddListener(ApplyVolume);

            CreateText("Sensitivity Label", panel, "SENSITIVITY", 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, MutedTextColor, new Vector2(-60f, -117f), new Vector2(180f, 26f));
            sensitivityValueText = CreateText("Sensitivity Value", panel, sensitivity.ToString("0.0"), 13,
                FontStyle.Normal, TextAnchor.MiddleRight, TextColor, new Vector2(115f, -117f), new Vector2(70f, 26f));
            Slider sensitivitySlider = CreateSlider("Sensitivity Slider", panel, new Vector2(0f, -148f), 0.2f, 8f, sensitivity);
            sensitivitySlider.onValueChanged.AddListener(ApplySensitivity);

            CreateButton("Quit", panel, "QUIT", new Vector2(0f, -211f), Quit);
            CreateText("Hint", panel, "ESC  /  RESUME", 11, FontStyle.Normal,
                TextAnchor.MiddleCenter, MutedTextColor, new Vector2(0f, -253f), new Vector2(400f, 22f));
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(300f, 38f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = ControlColor;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateColorBlock();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);

            RectTransform textRect = CreateRect("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Text text = textRect.gameObject.AddComponent<Text>();
            ConfigureText(text, label, 14, FontStyle.Normal, TextAnchor.MiddleCenter, TextColor);
            return button;
        }

        private Slider CreateSlider(string name, Transform parent, Vector2 position, float minimum, float maximum, float value)
        {
            RectTransform root = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(300f, 22f));
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = false;

            RectTransform track = CreateRect("Track", root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(-10f, 3f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = ControlColor;

            RectTransform fillArea = CreateRect("Fill Area", root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(-10f, 3f));
            RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = AccentColor;

            RectTransform handleArea = CreateRect("Handle Slide Area", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10f, 0f));
            RectTransform handle = CreateRect("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10f, 16f));
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = TextColor;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.SetValueWithoutNotify(value);
            return slider;
        }

        private Dropdown CreateDropdown(Transform parent, Vector2 position)
        {
            RectTransform root = CreateRect("Mode Dropdown", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(190f, 34f));
            Image rootImage = root.gameObject.AddComponent<Image>();
            rootImage.color = ControlColor;

            Dropdown dropdown = root.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = rootImage;
            dropdown.colors = CreateColorBlock();

            RectTransform captionRect = CreateInsetRect("Label", root, 11f, 30f, 0f, 0f);
            Text captionText = captionRect.gameObject.AddComponent<Text>();
            ConfigureText(captionText, "Fullscreen", 13, FontStyle.Normal, TextAnchor.MiddleLeft, TextColor);

            RectTransform arrowRect = CreateRect("Arrow", root, new Vector2(1f, 0f), Vector2.one, new Vector2(-15f, 0f), new Vector2(30f, 34f));
            Text arrowText = arrowRect.gameObject.AddComponent<Text>();
            ConfigureText(arrowText, "▾", 16, FontStyle.Normal, TextAnchor.MiddleCenter, AccentColor);

            RectTransform template = CreateRect("Template", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -2f), new Vector2(0f, 70f));
            template.pivot = new Vector2(0.5f, 1f);
            Image templateImage = template.gameObject.AddComponent<Image>();
            templateImage.color = PanelColor;

            ScrollRect scrollRect = template.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = CreateRect("Viewport", template, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform content = CreateRect("Content", viewport, new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 68f));
            content.pivot = new Vector2(0.5f, 1f);

            RectTransform item = CreateRect("Item", content, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -17f), new Vector2(0f, 34f));
            Toggle toggle = item.gameObject.AddComponent<Toggle>();
            Image itemBackground = item.gameObject.AddComponent<Image>();
            itemBackground.color = ControlColor;
            toggle.targetGraphic = itemBackground;

            RectTransform checkmark = CreateRect("Item Checkmark", item, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(3f, 18f));
            Image checkmarkImage = checkmark.gameObject.AddComponent<Image>();
            checkmarkImage.color = AccentColor;
            toggle.graphic = checkmarkImage;

            RectTransform itemLabelRect = CreateInsetRect("Item Label", item, 20f, 8f, 0f, 0f);
            Text itemLabel = itemLabelRect.gameObject.AddComponent<Text>();
            ConfigureText(itemLabel, "Option", 13, FontStyle.Normal, TextAnchor.MiddleLeft, TextColor);

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            dropdown.template = template;
            dropdown.captionText = captionText;
            dropdown.itemText = itemLabel;
            dropdown.options = new List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("Fullscreen"),
                new Dropdown.OptionData("Windowed")
            };

            // Two choices fit without a scrollbar; keeping it absent also prevents oversized scroll controls.
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private Text CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color,
            Vector2 position,
            Vector2 size,
            bool useHeaderFont = false)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            ConfigureText(text, content, fontSize, fontStyle, alignment, color, useHeaderFont);
            return text;
        }

        private void ConfigureText(
            Text text,
            string content,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color,
            bool useHeaderFont = false)
        {
            text.font = useHeaderFont ? headerFont : bodyFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static ColorBlock CreateColorBlock()
        {
            return new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.28f, 1.28f, 1.28f, 1f),
                pressedColor = new Color(1.55f, 1.48f, 1.25f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static RectTransform CreateInsetRect(string name, Transform parent, float left, float right, float bottom, float top)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }
    }
}
