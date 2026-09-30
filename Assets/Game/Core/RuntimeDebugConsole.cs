using AnEchoHasNoShape.Echolocation;
using AnEchoHasNoShape.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnEchoHasNoShape
{
    [DisallowMultipleComponent]
    public sealed class RuntimeDebugConsole : MonoBehaviour
    {
        private Canvas canvas;
        private InputField input;
        private Text feedback;
        private FirstPersonController player;
        private Rigidbody playerBody;
        private Collider playerCollider;
        private bool consoleOpen;
        private bool godMode;
        private bool previousCameraCanMove;
        private bool previousPlayerCanMove;
        private bool previousKinematic;
        private bool previousGravity;
        private bool previousColliderEnabled;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private Vector3 centerPosition;
        private Quaternion centerRotation;
        private bool centerPoseCaptured;

        private void Awake()
        {
            if (GameManager.Instance == null || !GameManager.Instance.DevelopmentToolsEnabled)
            {
                enabled = false;
                return;
            }

            BuildUI();
            canvas.gameObject.SetActive(false);
        }

        private void Start()
        {
            FindPlayer();
            CaptureCenterPose();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Slash) && !consoleOpen)
            {
                OpenConsole();
                return;
            }

            if (consoleOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CloseConsole();
                return;
            }

            if (godMode) UpdateGodMovement();
        }

        private void OpenConsole()
        {
            FindPlayer();
            consoleOpen = true;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            if (player != null)
            {
                previousCameraCanMove = player.cameraCanMove;
                previousPlayerCanMove = player.playerCanMove;
                player.cameraCanMove = false;
                player.playerCanMove = false;
            }

            canvas.gameObject.SetActive(true);
            input.text = "/";
            input.ActivateInputField();
            input.caretPosition = input.text.Length;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CloseConsole()
        {
            consoleOpen = false;
            canvas.gameObject.SetActive(false);
            if (player != null)
            {
                player.cameraCanMove = godMode || previousCameraCanMove;
                player.playerCanMove = godMode ? false : previousPlayerCanMove;
            }

            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private void Execute(string rawCommand)
        {
            string command = (rawCommand ?? string.Empty).Trim().TrimStart('/');
            string[] parts = command.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                CloseConsole();
                return;
            }

            string result;
            switch (parts[0].ToLowerInvariant())
            {
                case "help":
                    result = "/tp centermonument | /tp glacierstart | /tp glacierend | /god | /player | /echo unlock | /tutorial complete | /reverb";
                    break;
                case "tp":
                    result = parts.Length > 1 ? Teleport(parts[1]) : "Usage: /tp <centermonument|glacierstart|glacierend>";
                    break;
                case "god":
                    SetGodMode(true);
                    result = "God mode enabled. WASD + Space/Ctrl, Shift to boost.";
                    break;
                case "player":
                    SetGodMode(false);
                    result = "Player controls restored.";
                    break;
                case "echo":
                    if (parts.Length > 1 && parts[1].Equals("unlock", System.StringComparison.OrdinalIgnoreCase))
                    {
                        GameManager.Instance?.UnlockEcho();
                        ScreenPromptUI.DismissEchoTutorial();
                        result = "Echo unlocked.";
                    }
                    else result = "Usage: /echo unlock";
                    break;
                case "tutorial":
                    if (parts.Length > 1 && parts[1].Equals("complete", System.StringComparison.OrdinalIgnoreCase))
                    {
                        GameManager.Instance?.ApplyTutorialCompletedState();
                        result = "Tutorial completion state applied.";
                    }
                    else result = "Usage: /tutorial complete";
                    break;
                case "reverb":
                    WorldReverberationController.Instance?.TriggerReverberation();
                    result = "World reverberation triggered.";
                    break;
                default:
                    result = $"Unknown command: /{parts[0]}. Try /help.";
                    break;
            }

            feedback.text = result;
            input.text = "/";
            input.ActivateInputField();
            input.caretPosition = input.text.Length;
        }

        private string Teleport(string pointName)
        {
            FindPlayer();
            if (player == null) return "Player not found.";

            if (pointName.Equals("centermonument", System.StringComparison.OrdinalIgnoreCase) && centerPoseCaptured)
            {
                TeleportToPose(centerPosition, centerRotation);
                return "Teleported to center monument.";
            }

            Transform destination = GameManager.Instance?.GetPointOfInterest(pointName);
            if (destination == null) return $"Point of interest not found: {pointName}.";
            player.TeleportTo(destination);
            return $"Teleported to {pointName}.";
        }

        private void TeleportToPose(Vector3 position, Quaternion rotation)
        {
            GameObject poseObject = new GameObject("Debug Teleport Pose") { hideFlags = HideFlags.HideAndDontSave };
            poseObject.transform.SetPositionAndRotation(position, rotation);
            player.TeleportTo(poseObject.transform);
            Destroy(poseObject);
        }

        private void SetGodMode(bool enabledState)
        {
            FindPlayer();
            if (player == null || godMode == enabledState) return;
            godMode = enabledState;
            if (enabledState)
            {
                playerBody = player.GetComponent<Rigidbody>();
                playerCollider = player.GetComponent<Collider>();
                previousKinematic = playerBody != null && playerBody.isKinematic;
                previousGravity = playerBody != null && playerBody.useGravity;
                previousColliderEnabled = playerCollider == null || playerCollider.enabled;
                player.playerCanMove = false;
                player.cameraCanMove = true;
                if (playerBody != null)
                {
                    playerBody.linearVelocity = Vector3.zero;
                    playerBody.isKinematic = true;
                    playerBody.useGravity = false;
                }
                if (playerCollider != null) playerCollider.enabled = false;
            }
            else
            {
                if (playerBody != null)
                {
                    playerBody.isKinematic = previousKinematic;
                    playerBody.useGravity = previousGravity;
                }
                if (playerCollider != null) playerCollider.enabled = previousColliderEnabled;
                player.playerCanMove = true;
                player.cameraCanMove = true;
                if (consoleOpen)
                {
                    // Closing the console after /player must restore normal
                    // controls, not the disabled state captured from god mode.
                    previousPlayerCanMove = true;
                    previousCameraCanMove = true;
                }
            }
        }

        private void UpdateGodMovement()
        {
            if (player == null) return;
            Camera view = player.playerCamera != null ? player.playerCamera : Camera.main;
            Transform basis = view != null ? view.transform : player.transform;
            Vector3 direction = basis.forward * Input.GetAxisRaw("Vertical") + basis.right * Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.Space)) direction += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) direction -= Vector3.up;
            float speed = Input.GetKey(KeyCode.LeftShift) ? 32f : 12f;
            player.transform.position += direction.normalized * speed * Time.unscaledDeltaTime;
        }

        private void FindPlayer()
        {
            if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        }

        private void CaptureCenterPose()
        {
            if (player == null || centerPoseCaptured) return;
            centerPosition = player.transform.position;
            centerRotation = player.transform.rotation;
            centerPoseCaptured = true;
        }

        private void BuildUI()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            Font font = GameManager.Instance != null && GameManager.Instance.BodyFont != null
                ? GameManager.Instance.BodyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject canvasObject = new GameObject("Development Console", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject panel = new GameObject("Console Panel", typeof(RectTransform), typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(canvasObject.transform, false);
            panelRect.anchorMin = new Vector2(0.12f, 0.76f);
            panelRect.anchorMax = new Vector2(0.88f, 0.95f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.052f, 0.97f);

            feedback = CreateText(panel.transform, "Feedback", font, 22, new Vector2(0.03f, 0.48f), new Vector2(0.97f, 0.9f));
            feedback.text = "Development console — /help";

            GameObject fieldObject = new GameObject("Command", typeof(RectTransform), typeof(Image), typeof(InputField));
            RectTransform fieldRect = fieldObject.GetComponent<RectTransform>();
            fieldRect.SetParent(panel.transform, false);
            fieldRect.anchorMin = new Vector2(0.03f, 0.12f);
            fieldRect.anchorMax = new Vector2(0.97f, 0.43f);
            fieldRect.offsetMin = fieldRect.offsetMax = Vector2.zero;
            fieldObject.GetComponent<Image>().color = new Color(0.1f, 0.13f, 0.14f, 1f);
            Text inputText = CreateText(fieldObject.transform, "Text", font, 25, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f));
            inputText.alignment = TextAnchor.MiddleLeft;
            input = fieldObject.GetComponent<InputField>();
            input.textComponent = inputText;
            input.targetGraphic = fieldObject.GetComponent<Image>();
            input.lineType = InputField.LineType.SingleLine;
            input.onEndEdit.AddListener(value =>
            {
                if (consoleOpen && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Execute(value);
            });
        }

        private static Text CreateText(Transform parent, string name, Font font, int size, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = obj.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = new Color(0.88f, 0.89f, 0.82f, 1f);
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private void OnDestroy()
        {
            if (godMode) SetGodMode(false);
        }
    }
}
