using System.Collections;
using System.Collections.Generic;
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
        private static RuntimeDebugConsole activeInstance;
        private static int escapeConsumedFrame = -1;

        public static bool IsOpen => activeInstance != null && activeInstance.consoleOpen;
        public static bool ConsumedEscapeThisFrame => escapeConsumedFrame == Time.frameCount;

        private static readonly string[] SuggestedCommands =
        {
            "/help",
            "/god",
            "/player",
            "/fog",
            "/fog on",
            "/fog off",
            "/tp centermonument",
            "/tp glaciermonument",
            "/tp glaciercity",
            "/tp glaciermid",
            "/tp glacierend",
            "/tp citymainentrance",
            "/echo unlock",
            "/tutorial complete",
            "/reverb",
            "/city neutral",
            "/city muse",
            "/city ruler",
            "/city architect",
            "/city reset",
            "/city gold"
        };

        private static readonly string[] SuggestionDescriptions =
        {
            "List available commands",
            "Enable free-flight mode",
            "Restore normal player controls",
            "Toggle fog in god mode",
            "Enable fog in god mode",
            "Disable fog in god mode",
            "Teleport to the central monument",
            "Teleport to the monument-side glacier entrance",
            "Teleport to the city-side glacier entrance",
            "Teleport to the glacier midpoint",
            "Teleport to the glacier endpoint",
            "Teleport to the Unremembered City main entrance",
            "Unlock the echo ability",
            "Apply completed tutorial progression",
            "Trigger a world reverberation",
            "Trigger the center tablet's white city state",
            "Trigger the Muse's blue city state",
            "Trigger the Ruler's red city state",
            "Trigger the Architect's green city state",
            "Reset city progression and remove permanent gold",
            "Trigger the final world echo and permanent golden city"
        };

        private Canvas canvas;
        private InputField input;
        private Text feedback;
        private GameObject suggestionsPanel;
        private Text suggestionsText;
        private readonly List<string> currentSuggestions = new List<string>();
        private FirstPersonController player;
        private Rigidbody playerBody;
        private Collider playerCollider;
        private bool consoleOpen;
        private int consoleClosedFrame = -1;
        private bool godMode;
        private bool fogStateCaptured;
        private bool previousFogEnabled;
        private bool previousCameraCanMove;
        private bool previousPlayerCanMove;
        private bool previousKinematic;
        private bool previousGravity;
        private bool previousColliderEnabled;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private float previousTimeScale = 1f;
        private bool timeScaleCaptured;
        private Coroutine focusRoutine;
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

            activeInstance = this;
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
            if (!consoleOpen && Time.frameCount > consoleClosedFrame && ConsoleTogglePressed())
            {
                OpenConsole();
                return;
            }

            if (consoleOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    escapeConsumedFrame = Time.frameCount;
                    CloseConsole();
                }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    Execute(input != null ? input.text : string.Empty);
                }
                else if (Input.GetKeyDown(KeyCode.Tab))
                {
                    AutocompleteFirstSuggestion();
                }
                else if (input != null && !input.isFocused && !Input.GetMouseButton(0))
                {
                    FocusInput();
                }
                return;
            }

            if (godMode) UpdateGodMovement();
        }

        private static bool ConsoleTogglePressed()
        {
            if (Input.GetKeyDown(KeyCode.Slash) || Input.GetKeyDown(KeyCode.KeypadDivide))
            {
                return true;
            }

            // KeyCode.Slash can be unreliable in standalone players on some
            // keyboard layouts. inputString reflects the character Unity actually
            // received and makes reopening the console reliable in builds.
            return !string.IsNullOrEmpty(Input.inputString) && Input.inputString.IndexOf('/') >= 0;
        }

        private void OpenConsole()
        {
            FindPlayer();
            consoleOpen = true;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            previousTimeScale = Time.timeScale;
            timeScaleCaptured = true;
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
            canvas.gameObject.SetActive(true);
            input.enabled = true;
            input.interactable = true;
            input.SetTextWithoutNotify("/");
            UpdateSuggestions("/");
            FocusInput();
            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
            }
            focusRoutine = StartCoroutine(FocusInputNextFrame());
        }

        private void CloseConsole()
        {
            consoleOpen = false;
            consoleClosedFrame = Time.frameCount;
            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
                focusRoutine = null;
            }
            if (input != null)
            {
                input.DeactivateInputField();
            }
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            if (suggestionsPanel != null)
            {
                suggestionsPanel.SetActive(false);
            }
            canvas.gameObject.SetActive(false);
            if (player != null)
            {
                player.cameraCanMove = godMode || previousCameraCanMove;
                player.playerCanMove = godMode ? false : previousPlayerCanMove;
            }

            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            if (timeScaleCaptured)
            {
                Time.timeScale = previousTimeScale;
                timeScaleCaptured = false;
            }
        }

        private IEnumerator FocusInputNextFrame()
        {
            yield return null;
            focusRoutine = null;
            if (consoleOpen)
            {
                FocusInput();
            }
        }

        private void FocusInput()
        {
            if (input == null)
            {
                return;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(input.gameObject);
            }
            input.Select();
            input.ActivateInputField();
            input.caretPosition = input.text.Length;
        }

        private void UpdateSuggestions(string rawInput)
        {
            if (suggestionsPanel == null || suggestionsText == null)
            {
                return;
            }

            string prefix = (rawInput ?? string.Empty).Trim();
            if (prefix.Length == 0)
            {
                prefix = "/";
            }
            else if (!prefix.StartsWith("/"))
            {
                prefix = "/" + prefix;
            }

            currentSuggestions.Clear();
            List<string> displayLines = new List<string>();
            for (int index = 0; index < SuggestedCommands.Length; index++)
            {
                string command = SuggestedCommands[index];
                if (!command.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                currentSuggestions.Add(command);
                displayLines.Add($"{command}    {SuggestionDescriptions[index]}");
            }

            bool showSuggestions = consoleOpen && currentSuggestions.Count > 0;
            suggestionsPanel.SetActive(showSuggestions);
            suggestionsText.text = showSuggestions ? string.Join("\n", displayLines) : string.Empty;
        }

        private void AutocompleteFirstSuggestion()
        {
            if (input == null || currentSuggestions.Count == 0)
            {
                return;
            }

            input.text = currentSuggestions[0];
            input.caretPosition = input.text.Length;
            FocusInput();
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
            bool closeAfterExecution = false;
            switch (parts[0].ToLowerInvariant())
            {
                case "help":
                    result = "/tp centermonument | /tp glaciermonument | /tp glaciercity | /tp glaciermid | /tp glacierend | /tp citymainentrance | /god | /player | /fog [on|off] | /echo unlock | /tutorial complete | /reverb | /city <neutral|muse|ruler|architect|reset|gold>";
                    break;
                case "city":
                    if (parts.Length != 2)
                        result = "Usage: /city <neutral|muse|ruler|architect|reset|gold>";
                    else
                    {
                        CitySequenceController city = FindAnyObjectByType<CitySequenceController>();
                        if (city == null) result = "No active city sequence found.";
                        else if (city.DebugSetEchoState(parts[1].ToLowerInvariant()))
                        {
                            result = $"City state: {parts[1].ToLowerInvariant()}.";
                            closeAfterExecution = true;
                        }
                        else result = "City not ready or invalid state. Use /city <neutral|muse|ruler|architect|reset|gold>.";
                    }
                    break;
                case "fog":
                    result = SetDebugFog(parts);
                    break;
                case "tp":
                    result = parts.Length > 1 ? Teleport(parts[1]) : "Usage: /tp <centermonument|glaciermonument|glaciercity|glaciermid|glacierend|citymainentrance>";
                    break;
                case "god":
                    SetGodMode(true);
                    result = "God mode enabled. WASD + Space/Ctrl, Shift to boost.";
                    closeAfterExecution = true;
                    break;
                case "player":
                    SetGodMode(false);
                    result = "Player controls restored.";
                    closeAfterExecution = true;
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
            if (closeAfterExecution)
            {
                Debug.Log(result, this);
                CloseConsole();
                return;
            }
            input.text = "/";
            UpdateSuggestions(input.text);
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

        private string SetDebugFog(string[] parts)
        {
            if (!godMode) return "Enter /god first to change debug fog.";
            GameManager manager = GameManager.Instance;
            if (manager == null) return "Game manager not found.";
            bool enabledState = !manager.FogEnabled;
            if (parts.Length > 1)
            {
                if (parts.Length != 2) return "Usage: /fog [on|off]";
                if (parts[1].Equals("on", System.StringComparison.OrdinalIgnoreCase)) enabledState = true;
                else if (parts[1].Equals("off", System.StringComparison.OrdinalIgnoreCase)) enabledState = false;
                else return "Usage: /fog [on|off]";
            }
            if (!fogStateCaptured)
            {
                previousFogEnabled = manager.FogEnabled;
                fogStateCaptured = true;
            }
            manager.SetFogEnabled(enabledState);
            return enabledState ? "Fog enabled." : "Fog disabled.";
        }

        private void RestoreDebugFog()
        {
            if (!fogStateCaptured) return;
            GameManager.Instance?.SetFogEnabled(previousFogEnabled);
            fogStateCaptured = false;
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
            if (!enabledState) RestoreDebugFog();
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
                player.playerCanMove = !consoleOpen;
                player.cameraCanMove = !consoleOpen;
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
            Vector3 movement = direction.normalized * speed * Time.unscaledDeltaTime;
            if (playerBody != null && playerBody.isKinematic)
            {
                playerBody.position += movement;
            }
            else
            {
                player.transform.position += movement;
            }
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
            canvasObject.AddComponent<GraphicRaycaster>();
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

            suggestionsPanel = new GameObject("Command Suggestions", typeof(RectTransform), typeof(Image));
            RectTransform suggestionsRect = suggestionsPanel.GetComponent<RectTransform>();
            suggestionsRect.SetParent(canvasObject.transform, false);
            suggestionsRect.anchorMin = new Vector2(0.12f, 0.43f);
            suggestionsRect.anchorMax = new Vector2(0.88f, 0.755f);
            suggestionsRect.offsetMin = suggestionsRect.offsetMax = Vector2.zero;
            Image suggestionsBackground = suggestionsPanel.GetComponent<Image>();
            suggestionsBackground.color = new Color(0.035f, 0.062f, 0.07f, 0.95f);
            suggestionsBackground.raycastTarget = false;
            suggestionsText = CreateText(
                suggestionsPanel.transform,
                "Suggestions",
                font,
                19,
                new Vector2(0.025f, 0.055f),
                new Vector2(0.975f, 0.945f));
            suggestionsText.alignment = TextAnchor.UpperLeft;
            suggestionsText.lineSpacing = 1.08f;
            suggestionsText.raycastTarget = false;
            suggestionsPanel.SetActive(false);

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
            input.onValueChanged.AddListener(UpdateSuggestions);
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
            RestoreDebugFog();
            if (activeInstance == this)
            {
                activeInstance = null;
            }
            if (timeScaleCaptured)
            {
                Time.timeScale = previousTimeScale;
                timeScaleCaptured = false;
            }
            if (godMode) SetGodMode(false);
        }
    }
}
