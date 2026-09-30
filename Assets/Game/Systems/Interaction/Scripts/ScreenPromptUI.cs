using UnityEngine;
using UnityEngine.UI;

namespace AnEchoHasNoShape.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ScreenPromptUI : MonoBehaviour
    {
        private static readonly Color PromptColor = new Color(0.88f, 0.89f, 0.82f, 1f);
        private static readonly Color ShimmerColor = new Color(0.72f, 0.94f, 1f, 1f);
        private static ScreenPromptUI instance;

        private Text interactionPrompt;
        private Text lessonPrompt;
        private RectTransform lessonRect;
        private float lessonEndsAt;
        private bool lessonPersistent;

        public static void EnsureExists()
        {
            if (instance != null) return;
            instance = FindAnyObjectByType<ScreenPromptUI>();
            if (instance == null) new GameObject("Screen Prompt UI").AddComponent<ScreenPromptUI>();
        }

        public static void SetInteraction(string message)
        {
            EnsureExists();
            instance.interactionPrompt.text = message ?? string.Empty;
            instance.interactionPrompt.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        public static void ClearInteraction()
        {
            if (instance == null || instance.interactionPrompt == null) return;
            instance.interactionPrompt.text = string.Empty;
            instance.interactionPrompt.gameObject.SetActive(false);
        }

        public static void ShowLesson(string message, float duration)
        {
            EnsureExists();
            instance.SetLesson(message, false, Time.unscaledTime + Mathf.Max(0.1f, duration));
        }

        public static void ShowEchoTutorial()
        {
            EnsureExists();
            instance.SetLesson("Press E to Echo.", true, 0f);
        }

        public static void DismissEchoTutorial()
        {
            if (instance == null) return;
            instance.lessonPersistent = false;
            instance.lessonEndsAt = 0f;
            if (instance.lessonPrompt == null) return;
            instance.lessonPrompt.text = string.Empty;
            instance.lessonPrompt.gameObject.SetActive(false);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            BuildUI();
        }

        private void Update()
        {
            if (lessonPrompt == null || !lessonPrompt.gameObject.activeSelf) return;
            if (!lessonPersistent && Time.unscaledTime >= lessonEndsAt)
            {
                DismissEchoTutorial();
                return;
            }

            float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.5f);
            lessonPrompt.color = Color.Lerp(PromptColor, ShimmerColor, 0.35f + shimmer * 0.65f);
            lessonRect.localScale = Vector3.one * Mathf.Lerp(0.99f, 1.035f, shimmer);
        }

        private void SetLesson(string message, bool persistent, float endsAt)
        {
            lessonPrompt.text = message ?? string.Empty;
            lessonPersistent = persistent;
            lessonEndsAt = endsAt;
            lessonRect.localScale = Vector3.one;
            lessonPrompt.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void BuildUI()
        {
            Font fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font font = GameManager.Instance != null && GameManager.Instance.BodyFont != null
                ? GameManager.Instance.BodyFont : fallbackFont;

            GameObject canvasObject = new GameObject("Prompt Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            interactionPrompt = CreatePrompt(canvasObject.transform, "Bottom Center Interaction Prompt",
                new Vector2(0.5f, 0f), new Vector2(0f, 58f), 38, font, out _);
            lessonPrompt = CreatePrompt(canvasObject.transform, "Top Center Echo Tutorial",
                new Vector2(0.5f, 1f), new Vector2(0f, -82f), 42, font, out lessonRect);
        }

        private static Text CreatePrompt(Transform parent, string name, Vector2 anchor,
            Vector2 anchoredPosition, int fontSize, Font font, out RectTransform rect)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor.y > 0.5f ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(1120f, 88f);
            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(2f, -2f);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 26;
            text.resizeTextMaxSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = PromptColor;
            text.raycastTarget = false;
            text.gameObject.SetActive(false);
            return text;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
