using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnEchoHasNoShape.Interaction
{
    public sealed class ConfirmationPromptUI : MonoBehaviour
    {
        private static ConfirmationPromptUI instance;
        private GameObject panelRoot;
        private Text questionText;
        private Action yesAction;
        private Action noAction;
        private FirstPersonController player;
        private bool previousCameraCanMove;
        private bool previousPlayerCanMove;
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        public static bool IsOpen => instance != null && instance.panelRoot.activeSelf;

        public static void Show(string question, Action onYes, Action onNo = null)
        {
            if (instance == null) new GameObject("Confirmation Prompt UI").AddComponent<ConfirmationPromptUI>();
            instance.Open(question, onYes, onNo);
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
            panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Y)) Confirm();
            else if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        private void Open(string question, Action onYes, Action onNo)
        {
            if (IsOpen) return;
            yesAction = onYes;
            noAction = onNo;
            questionText.text = question;
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            player = FindAnyObjectByType<FirstPersonController>();
            if (player != null)
            {
                previousCameraCanMove = player.cameraCanMove;
                previousPlayerCanMove = player.playerCanMove;
                player.cameraCanMove = false;
                player.playerCanMove = false;
            }
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            panelRoot.SetActive(true);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void Confirm()
        {
            Action action = yesAction;
            Close();
            action?.Invoke();
        }

        private void Cancel()
        {
            Action action = noAction;
            Close();
            action?.Invoke();
        }

        private void Close()
        {
            if (!IsOpen) return;
            panelRoot.SetActive(false);
            Time.timeScale = previousTimeScale;
            if (player != null)
            {
                player.cameraCanMove = previousCameraCanMove;
                player.playerCanMove = previousPlayerCanMove;
            }
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            yesAction = null;
            noAction = null;
        }

        private void BuildUI()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            Font font = GameManager.Instance != null && GameManager.Instance.BodyFont != null
                ? GameManager.Instance.BodyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject canvasObject = new GameObject("Confirmation Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            panelRoot = new GameObject("Confirmation Panel", typeof(RectTransform), typeof(Image));
            RectTransform panel = panelRoot.GetComponent<RectTransform>();
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(720f, 310f);
            panelRoot.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.061f, 0.98f);

            questionText = CreateText(panel, "Question", font, 34, new Vector2(0.08f, 0.47f), new Vector2(0.92f, 0.88f));
            questionText.alignment = TextAnchor.MiddleCenter;
            CreateButton(panel, "YES", font, new Vector2(0.1f, 0.13f), new Vector2(0.46f, 0.4f), Confirm);
            CreateButton(panel, "NO", font, new Vector2(0.54f, 0.13f), new Vector2(0.9f, 0.4f), Cancel);
        }

        private static void CreateButton(Transform parent, string label, Font font, Vector2 min, Vector2 max, Action action)
        {
            GameObject obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            obj.GetComponent<Image>().color = new Color(0.1f, 0.13f, 0.14f, 1f);
            Button button = obj.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateColorBlock();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            Text text = CreateText(rect, "Label", font, 27, Vector2.zero, Vector2.one);
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
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

        private static Text CreateText(Transform parent, string name, Font font, int size, Vector2 min, Vector2 max)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = obj.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = new Color(0.9f, 0.89f, 0.83f, 1f);
            return text;
        }

        private void OnDestroy()
        {
            if (IsOpen) Close();
            if (instance == this) instance = null;
        }
    }
}
