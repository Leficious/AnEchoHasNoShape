using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;
using AnEchoHasNoShape.Echolocation;

namespace AnEchoHasNoShape.Interaction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("An Echo Has No Shape/Interaction/Golden Gate Interactable")]
    public sealed class GoldenGateInteractable : MonoBehaviour, IInteractable, IInteractionPrompt
    {
        [Tooltip("Called once when the player passes through the unlocked gate. Hook the future ending sequence here.")]
        [SerializeField] private UnityEvent onEndingRequested = new UnityEvent();

        [Header("Five Ending Pulses")]
        [SerializeField, Min(0.1f)] private float pulseDuration = 3f;
        [SerializeField, Min(0.1f)] private float pulseInterval = 6.5f;
        [Tooltip("Multipliers of the GameManager camera rumble strength, from first pulse to fifth.")]
        [SerializeField] private Vector2 shakeStrength = new Vector2(1f, 5f);
        [SerializeField] private AudioClip shakingAudio;
        [SerializeField] private Vector2 shakingVolume = new Vector2(0.15f, 0.85f);
        [SerializeField] private Color paleGold = new Color(1f, 0.91f, 0.65f, 1f);
        [Header("Ending Phase Visual")]
        [SerializeField, Range(0f, 1f)] private float phaseOpacity = 0.82f;
        [SerializeField, Range(0f, 0.06f)] private float phaseDistortion = 0.024f;
        [SerializeField, Range(0f, 3f)] private float phaseVignette = 1.45f;
        [SerializeField, Min(0.01f)] private float phaseFadeSeconds = 0.8f;
        [Header("After The Fifth Pulse")]
        [SerializeField, Min(0.1f)] private float audioFadeSeconds = 3f;
        [SerializeField, Min(0f)] private float goldHoldSeconds = 2f;
        [SerializeField, Min(0.1f)] private float fadeToBlackSeconds = 3f;
        [Header("Ending Text")]
        [SerializeField, TextArea(1, 5)] private string[] lines =
            { "test sentence 1", "test sentence 2", "test sentence 3" };
        [SerializeField, Min(0.1f)] private float textFadeSeconds = 1.5f;
        [SerializeField, Min(0f)] private float textHoldSeconds = 3f;
        [SerializeField] private string endingTitle = "An Echo Has No Sound";
        [SerializeField] private string endingCaption = "Fin.";

        private static GoldenGateInteractable activeEnding;
        public static bool IsEnding => activeEnding != null && activeEnding.EndingRequested;
        private GameObject canvasRoot;
        private Image veil;
        private Text lineText, titleText, finText;
        private AudioSource rumbleAudio;
        private bool ownsListenerAudio;
        private float previousListenerVolume;
        private bool previousListenerPause;
        private FirstPersonController player;
        private Rigidbody playerBody;
        private RigidbodyConstraints previousBodyConstraints;
        private bool previousMovement, previousLook;
        private WorldReverberationController world;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => activeEnding = null;

        public bool EndingRequested { get; private set; }
        public event System.Action EndingSequenceRequested;
        public string InteractionVerb => "Pass through the Golden Gate";
        public string PromptText => "Pass through the Golden Gate?";
        public bool CanInteract => isActiveAndEnabled && !IsEnding && !EndingRequested &&
            GameManager.Instance != null && GameManager.Instance.AllRegionsCompleted;

        public void Interact(PlayerInteractionController interactor)
        {
            if (!CanInteract) return;
            world = WorldReverberationController.Instance;
            if (world == null)
            {
                Debug.LogError("Golden Gate requires a World Reverberation Controller.", this);
                return;
            }
            EndingRequested = true;
            activeEnding = this;
            StartCoroutine(PlayEnding());
            EndingSequenceRequested?.Invoke();
            onEndingRequested.Invoke();
        }

        private IEnumerator PlayEnding()
        {
            player = FindAnyObjectByType<FirstPersonController>();
            if (player != null)
            {
                previousMovement = player.playerCanMove;
                previousLook = player.cameraCanMove;
                player.playerCanMove = false;
                player.cameraCanMove = false;
                playerBody = player.GetComponent<Rigidbody>();
                if (playerBody != null)
                {
                    previousBodyConstraints = playerBody.constraints;
                    if (!playerBody.isKinematic)
                    {
                        playerBody.linearVelocity = Vector3.zero;
                        playerBody.angularVelocity = Vector3.zero;
                    }
                    playerBody.constraints = RigidbodyConstraints.FreezeAll;
                }
            }
            ScreenPromptUI.ClearInteraction();
            ScreenPromptUI.DismissEchoTutorial();
            world.BeginEndingSequence();
            PhasePassageScreenEffect.Enter(this,
                new Color(0.12f, 0.095f, 0.055f, 1f), paleGold,
                phaseOpacity, phaseFadeSeconds, phaseDistortion, phaseVignette,
                22000f, muffleAudio: false);
            // The phase veil begins immediately; only the final pulse sequence waits
            // for the wave already underway and its sound to finish naturally.
            while (world.HasLingeringPulse) yield return null;
            BuildPresentation();
            rumbleAudio = canvasRoot.AddComponent<AudioSource>();
            rumbleAudio.playOnAwake = false;
            rumbleAudio.spatialBlend = 0f;
            rumbleAudio.loop = true;
            rumbleAudio.clip = shakingAudio;
            rumbleAudio.volume = 0f;
            if (shakingAudio != null) rumbleAudio.Play();

            float duration = Mathf.Max(0.1f, pulseDuration);
            for (int i = 0; i < 5; i++)
            {
                float step = i / 4f;
                world.TriggerShortReverberation(duration, Mathf.Lerp(shakeStrength.x, shakeStrength.y, step));
                float initialVolume = rumbleAudio.volume;
                float targetVolume = Mathf.Clamp01(Mathf.Lerp(shakingVolume.x, shakingVolume.y, step));
                float fromAlpha = i / 5f;
                float toAlpha = (i + 1) / 5f;
                yield return Animate(duration, t =>
                {
                    veil.color = new Color(paleGold.r, paleGold.g, paleGold.b, Mathf.Lerp(fromAlpha, toAlpha, t));
                    rumbleAudio.volume = Mathf.Lerp(initialVolume, targetVolume, t);
                });
                if (i < 4) yield return new WaitForSeconds(Mathf.Max(0f, pulseInterval - duration));
            }
            GameManager.Instance?.StopEchoCameraRumble();
            PhasePassageScreenEffect.Exit(this);
            float peakVolume = rumbleAudio.volume;
            yield return Animate(audioFadeSeconds, t =>
            {
                rumbleAudio.volume = peakVolume * (1f - t);
                world.SetEndingAudioFade(1f - t);
            });
            rumbleAudio.Stop();
            world.StopReverberation();
            yield return new WaitForSeconds(Mathf.Max(0f, goldHoldSeconds));
            Color solidGold = new Color(paleGold.r, paleGold.g, paleGold.b, 1f);
            GameManager.Instance?.FadeOutEndingMusic(fadeToBlackSeconds);
            previousListenerVolume = AudioListener.volume;
            previousListenerPause = AudioListener.pause;
            ownsListenerAudio = true;
            yield return Animate(fadeToBlackSeconds, t =>
            {
                veil.color = Color.Lerp(solidGold, Color.black, t);
                AudioListener.volume = previousListenerVolume * (1f - t);
            });
            // Silence every source, including persistent ambience and one-shot effects,
            // throughout the text and final card.
            AudioListener.pause = true;
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                lineText.text = line;
                yield return Animate(textFadeSeconds, t => SetTextAlpha(lineText, t));
                yield return new WaitForSeconds(Mathf.Max(0f, textHoldSeconds));
                yield return Animate(textFadeSeconds, t => SetTextAlpha(lineText, 1f - t));
            }
            lineText.text = string.Empty;
            yield return Animate(textFadeSeconds, t =>
            {
                SetTextAlpha(titleText, t);
                SetTextAlpha(finText, t);
            });
            // Remain on this card until the future title-screen transition is implemented.
        }

        private static IEnumerator Animate(float seconds, System.Action<float> apply)
        {
            float duration = Mathf.Max(0.01f, seconds);
            apply(0f);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                apply(Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            apply(1f);
        }

        private void BuildPresentation()
        {
            canvasRoot = new GameObject("Golden Gate Ending (Runtime)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var background = new GameObject("Gold To Black", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvasRoot.transform, false);
            veil = background.GetComponent<Image>();
            veil.color = new Color(paleGold.r, paleGold.g, paleGold.b, 0f);
            veil.raycastTarget = false;
            RectTransform rect = veil.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            lineText = CreateText("Ending Line", "", TextAnchor.MiddleCenter, 40, false);
            titleText = CreateText("Title", endingTitle, TextAnchor.UpperLeft, 52, true);
            finText = CreateText("Fin", endingCaption, TextAnchor.LowerRight, 40, true);
        }

        private Text CreateText(string objectName, string content, TextAnchor alignment, int size, bool header)
        {
            var obj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(canvasRoot.transform, false);
            Text text = obj.GetComponent<Text>();
            text.font = header ? GameManager.Instance.HeaderFont : GameManager.Instance.BodyFont;
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(1f, 0.95f, 0.81f, 0f);
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(80f, 70f);
            text.rectTransform.offsetMax = new Vector2(-80f, -70f);
            return text;
        }

        private static void SetTextAlpha(Text text, float alpha)
        {
            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }

        private void OnDisable()
        {
            if (activeEnding != this) return;
            StopAllCoroutines();
            PhasePassageScreenEffect.Exit(this);
            GameManager.Instance?.StopEchoCameraRumble();
            if (world != null) world.StopReverberation();
            if (canvasRoot != null) Destroy(canvasRoot);
            if (ownsListenerAudio)
            {
                AudioListener.pause = previousListenerPause;
                AudioListener.volume = previousListenerVolume;
                ownsListenerAudio = false;
            }
            if (player != null)
            {
                player.playerCanMove = previousMovement;
                player.cameraCanMove = previousLook;
            }
            if (playerBody != null)
                playerBody.constraints = previousBodyConstraints;
            activeEnding = null;
        }
    }
}
