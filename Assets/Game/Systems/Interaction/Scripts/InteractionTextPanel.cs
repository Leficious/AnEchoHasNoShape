using System.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnEchoHasNoShape.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionTextPanel : MonoBehaviour
    {
        private static readonly Color BorderColor = new Color(0.43f, 0.50f, 0.48f, 0.9f);
        private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.061f, 0.96f);
        private static readonly Color TextColor = new Color(0.91f, 0.90f, 0.84f, 1f);
        private static readonly Color HintColor = new Color(0.60f, 0.66f, 0.63f, 1f);
        private static readonly Color ScrollTrackColor = new Color(0.12f, 0.16f, 0.17f, 0.72f);
        private static readonly Color ScrollHandleColor = new Color(0.63f, 0.68f, 0.64f, 0.82f);

        public static InteractionTextPanel Instance { get; private set; }

        private GameObject panelRoot;
        private Text bodyText;
        private Text hintText;
        private RectTransform textContentRect;
        private ScrollRect textScrollRect;
        private FirstPersonController firstPersonController;
        private bool previousCameraCanMove;
        private bool previousPlayerCanMove;
        private float previousTimeScale = 1f;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private Coroutine typingCoroutine;
        private string fullContent;
        private bool isTyping;
        private bool followTypedText;
        private Action onClosed;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
        public bool IsTyping => isTyping;

        private void Update()
        {
            if (!IsOpen || textScrollRect == null)
            {
                return;
            }

            float wheelDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheelDelta) < 0.01f)
            {
                return;
            }

            RefreshTextLayout();

            float overflow = textContentRect.rect.height - textScrollRect.viewport.rect.height;
            if (overflow <= 0.01f)
            {
                return;
            }

            float normalizedDelta = wheelDelta * 34f / overflow;
            textScrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                textScrollRect.verticalNormalizedPosition + normalizedDelta);

            // Scrolling upward pauses typewriter auto-follow so the player can reread.
            // Returning to the bottom resumes it.
            followTypedText = textScrollRect.verticalNormalizedPosition <= 0.002f;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildPanel();
            panelRoot.SetActive(false);
        }

        public static void Show(string content, float charactersPerSecond = 36f, Action onClosed = null)
        {
            if (Instance == null)
            {
                GameObject panelObject = new GameObject("Interaction Text UI");
                panelObject.AddComponent<InteractionTextPanel>();
            }

            Instance.ShowContent(content, charactersPerSecond, onClosed);
        }

        public void AdvanceOrClose()
        {
            if (isTyping)
            {
                CompleteTyping();
                return;
            }

            Hide();
        }

        public void Hide()
        {
            if (!IsOpen)
            {
                return;
            }

            StopTyping();

            panelRoot.SetActive(false);

            if (firstPersonController != null)
            {
                firstPersonController.cameraCanMove = previousCameraCanMove;
                firstPersonController.playerCanMove = previousPlayerCanMove;
            }

            Time.timeScale = previousTimeScale;

            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;

            Action completion = onClosed;
            onClosed = null;
            completion?.Invoke();
        }

        private void ShowContent(string content, float charactersPerSecond, Action completion)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            StopTyping();
            fullContent = FormatMarkup(content);
            onClosed = completion;
            bodyText.text = string.Empty;
            isTyping = true;
            followTypedText = true;
            hintText.text = "F  /  REVEAL";

            if (!IsOpen)
            {
                previousTimeScale = Time.timeScale;
                previousCursorLockMode = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                firstPersonController = FindAnyObjectByType<FirstPersonController>();

                if (firstPersonController != null)
                {
                    previousCameraCanMove = firstPersonController.cameraCanMove;
                    previousPlayerCanMove = firstPersonController.playerCanMove;
                    firstPersonController.cameraCanMove = false;
                    firstPersonController.playerCanMove = false;
                }

                Time.timeScale = 0f;
            }

            panelRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshTextLayout();
            textScrollRect.verticalNormalizedPosition = 1f;
            typingCoroutine = StartCoroutine(TypeContent(Mathf.Max(1f, charactersPerSecond)));
        }

        private IEnumerator TypeContent(float charactersPerSecond)
        {
            float baseDelay = 1f / charactersPerSecond;
            StringBuilder typedContent = new StringBuilder(fullContent.Length);
            List<string> activeClosingTags = new List<string>(4);

            for (int index = 0; index < fullContent.Length; index++)
            {
                char character = fullContent[index];

                // Rich-text tags are inserted atomically so markup never types
                // visibly one bracket at a time.
                if (character == '<')
                {
                    int tagEnd = fullContent.IndexOf('>', index);
                    if (tagEnd >= index)
                    {
                        string tag = fullContent.Substring(index, tagEnd - index + 1);
                        typedContent.Append(tag);
                        UpdateActiveRichTextTags(tag, activeClosingTags);
                        index = tagEnd;
                        continue;
                    }
                }

                typedContent.Append(character);
                StringBuilder displayedContent = new StringBuilder(typedContent.Length + activeClosingTags.Count * 10);
                displayedContent.Append(typedContent);
                for (int tagIndex = activeClosingTags.Count - 1; tagIndex >= 0; tagIndex--)
                {
                    displayedContent.Append(activeClosingTags[tagIndex]);
                }

                // Legacy UI Text displays an opening rich-text tag literally until
                // its closing tag exists. Close active tags only in the displayed
                // copy while the underlying typewriter continues through the text.
                bodyText.text = displayedContent.ToString();
                RefreshTextLayout();

                if (followTypedText)
                {
                    textScrollRect.verticalNormalizedPosition = 0f;
                }

                float delay = baseDelay;
                if (character == '.' || character == '!' || character == '?')
                {
                    delay += 0.22f;
                }
                else if (character == ',' || character == ';' || character == ':')
                {
                    delay += 0.10f;
                }
                else if (character == '\n')
                {
                    delay += 0.14f;
                }

                yield return new WaitForSecondsRealtime(delay);
            }

            typingCoroutine = null;
            isTyping = false;
            RefreshTextLayout();
            if (followTypedText)
            {
                textScrollRect.verticalNormalizedPosition = 0f;
            }
            hintText.text = "F  /  CLOSE";
        }

        private static void UpdateActiveRichTextTags(string tag, List<string> activeClosingTags)
        {
            if (tag.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
            {
                activeClosingTags.Add("</color>");
            }
            else if (tag.StartsWith("<size=", StringComparison.OrdinalIgnoreCase))
            {
                activeClosingTags.Add("</size>");
            }
            else if (tag.Equals("</color>", StringComparison.OrdinalIgnoreCase))
            {
                RemoveLastClosingTag(activeClosingTags, "</color>");
            }
            else if (tag.Equals("</size>", StringComparison.OrdinalIgnoreCase))
            {
                RemoveLastClosingTag(activeClosingTags, "</size>");
            }
        }

        private static void RemoveLastClosingTag(List<string> activeClosingTags, string closingTag)
        {
            for (int index = activeClosingTags.Count - 1; index >= 0; index--)
            {
                if (activeClosingTags[index].Equals(closingTag, StringComparison.OrdinalIgnoreCase))
                {
                    activeClosingTags.RemoveAt(index);
                    return;
                }
            }
        }

        private void CompleteTyping()
        {
            if (!isTyping)
            {
                return;
            }

            StopTyping();
            bodyText.text = fullContent;
            RefreshTextLayout();
            textScrollRect.verticalNormalizedPosition = 0f;
            hintText.text = "F  /  CLOSE";
        }

        private void StopTyping()
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }

            isTyping = false;
        }

        private static string FormatMarkup(string content)
        {
            return content
                .Replace("[important]", "<color=#E2C77C><size=34>")
                .Replace("[/important]", "</size></color>");
        }

        private void BuildPanel()
        {
            Font fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();
            Font uiBodyFont = gameManager != null && gameManager.BodyFont != null
                ? gameManager.BodyFont
                : fallbackFont;
            Font interactionFont = gameManager != null && gameManager.InteractionFont != null
                ? gameManager.InteractionFont
                : uiBodyFont;

            GameObject canvasObject = new GameObject("Interaction Text Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemObject = new GameObject(
                    "Interaction Event System",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystemObject.transform.SetParent(transform, false);
            }

            panelRoot = CreateRect("Text Panel Root", canvasObject.transform, Vector2.zero, Vector2.one).gameObject;

            RectTransform border = CreateRect(
                "Border",
                panelRoot.transform,
                new Vector2(0.17f, 0.17f),
                new Vector2(0.83f, 0.83f));
            Image borderImage = border.gameObject.AddComponent<Image>();
            borderImage.color = BorderColor;

            RectTransform panel = CreateRect("Panel", border, Vector2.zero, Vector2.one);
            panel.offsetMin = new Vector2(2f, 2f);
            panel.offsetMax = new Vector2(-2f, -2f);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;

            RectTransform scrollArea = CreateRect("Scrollable Text Area", panel, Vector2.zero, Vector2.one);
            scrollArea.offsetMin = new Vector2(56f, 68f);
            scrollArea.offsetMax = new Vector2(-56f, -52f);
            textScrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
            textScrollRect.horizontal = false;
            textScrollRect.vertical = true;
            textScrollRect.movementType = ScrollRect.MovementType.Clamped;
            textScrollRect.inertia = true;
            textScrollRect.decelerationRate = 0.12f;
            // Wheel movement is handled directly so it remains reliable with either
            // Unity input backend and while the first-person controller is paused.
            textScrollRect.scrollSensitivity = 0f;

            RectTransform viewport = CreateRect("Viewport", scrollArea, Vector2.zero, Vector2.one);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            textContentRect = CreateRect(
                "Text Content",
                viewport,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f));
            textContentRect.pivot = new Vector2(0.5f, 1f);
            textContentRect.anchoredPosition = Vector2.zero;
            // Leave room for italic/calligraphic glyphs whose flourishes extend
            // beyond their reported character bounds and would otherwise be masked.
            textContentRect.offsetMin = new Vector2(16f, 0f);
            textContentRect.offsetMax = new Vector2(-12f, 0f);

            bodyText = textContentRect.gameObject.AddComponent<Text>();
            bodyText.font = interactionFont;
            bodyText.fontSize = 30;
            bodyText.fontStyle = FontStyle.Normal;
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.color = TextColor;
            bodyText.lineSpacing = 1.25f;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.raycastTarget = false;
            bodyText.supportRichText = true;

            ContentSizeFitter contentFitter = textContentRect.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform scrollbarRect = CreateRect(
                "Scrollbar",
                scrollArea,
                new Vector2(1f, 0f),
                Vector2.one);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(9f, 0f);
            Image trackImage = scrollbarRect.gameObject.AddComponent<Image>();
            trackImage.color = ScrollTrackColor;

            Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            RectTransform slidingArea = CreateRect("Sliding Area", scrollbarRect, Vector2.zero, Vector2.one);
            slidingArea.offsetMin = new Vector2(2f, 2f);
            slidingArea.offsetMax = new Vector2(-2f, -2f);

            RectTransform handle = CreateRect("Handle", slidingArea, Vector2.zero, Vector2.one);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = ScrollHandleColor;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;

            textScrollRect.viewport = viewport;
            textScrollRect.content = textContentRect;
            textScrollRect.verticalScrollbar = scrollbar;
            textScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            textScrollRect.verticalScrollbarSpacing = 12f;

            RectTransform hintRect = CreateRect(
                "Close Hint",
                panel,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f));
            hintRect.offsetMin = new Vector2(56f, 24f);
            hintRect.offsetMax = new Vector2(-56f, 56f);

            hintText = hintRect.gameObject.AddComponent<Text>();
            hintText.font = uiBodyFont;
            hintText.text = "F  /  CLOSE";
            hintText.fontSize = 14;
            hintText.alignment = TextAnchor.MiddleRight;
            hintText.color = HintColor;
            hintText.raycastTarget = false;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private void RefreshTextLayout()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(textContentRect);
            Canvas.ForceUpdateCanvases();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            if (IsOpen && firstPersonController != null)
            {
                firstPersonController.cameraCanMove = previousCameraCanMove;
                firstPersonController.playerCanMove = previousPlayerCanMove;
            }

            if (IsOpen)
            {
                Time.timeScale = previousTimeScale;
                Cursor.lockState = previousCursorLockMode;
                Cursor.visible = previousCursorVisible;
            }

            Instance = null;
        }
    }
}
