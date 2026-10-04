using System.Collections;
using AnEchoHasNoShape.Echolocation;
using AnEchoHasNoShape.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace AnEchoHasNoShape
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        public enum DevelopmentSpawnPoint
        {
            CenterMonument,
            GlacierStart,
            GlacierMid,
            GlacierEnd,
            CityMainEntrance
        }

        public enum PlayerCheckpoint
        {
            Monument,
            GlacierStart,
            GlacierMid,
            GlacierEnd,
            CityMainEntrance
        }

        public static GameManager Instance { get; private set; }

        [Header("Master Typography")]
        [Tooltip("Display face used for the game title and major headings.")]
        [SerializeField] private Font headerFont;

        [Tooltip("Readable face used for buttons, labels, values, and supporting text.")]
        [SerializeField] private Font bodyFont;

        [Tooltip("Face used for lore passages and other interactable world text. Falls back to the body font when empty.")]
        [SerializeField] private Font interactionFont;

        [Header("Runtime Environment")]
        [Tooltip("Enables global fog when the game starts. Fog remains disabled while editing the scene.")]
        [SerializeField] private bool enableFogAtGameStart = true;

        [Tooltip("Global fog density applied when the game starts.")]
        [SerializeField, Range(0f, 0.5f)] private float fogStrength = 0.15f;

        [Tooltip("AERO fog tint used at full strength inside GlacierAreaInner.")]
        [SerializeField] private Color glacierFogColor = new Color(0.62f, 0.78f, 0.9f, 1f);

        [Header("Player Progression")]
        [Tooltip("Useful for testing later sections. Leave disabled for the intended opening.")]
        [SerializeField] private bool startWithEchoUnlocked;

        [Header("Checkpoint Transition")]
        [SerializeField, Min(0.01f)] private float checkpointFadeToBlackDuration = 0.25f;
        [SerializeField, Min(0f)] private float checkpointBlackHoldDuration = 0.08f;
        [SerializeField, Min(0.01f)] private float checkpointFadeFromBlackDuration = 0.7f;

        [Header("Development Start State")]
        [Tooltip("Enables start-state overrides and the slash-command console in the Editor and standalone builds. Disable this for public releases that should not expose debug commands.")]
        [SerializeField] private bool enableDevelopmentTools;

        [SerializeField] private DevelopmentSpawnPoint initialSpawnPoint = DevelopmentSpawnPoint.CenterMonument;

        [Tooltip("Starts after TabletMain: echo unlocked, first reverberation counted, and HiddenDoorStone open.")]
        [SerializeField] private bool tutorialCompleted;

        [Tooltip("Optional explicit monument spawn. If empty, an object named MonumentSpawn is used.")]
        [SerializeField] private Transform centerMonumentSpawn;

        [Tooltip("Optional explicit glacier-start spawn. If empty, an object named GlacierRespawnStart is used.")]
        [SerializeField] private Transform glacierStartSpawn;

        [Tooltip("Optional glacier-midpoint spawn. If empty, an object named GlacierRespawnMid is used.")]
        [SerializeField] private Transform glacierMidSpawn;

        [Tooltip("Optional glacier-end spawn. If empty, an object named GlacierRespawnEnd is used.")]
        [SerializeField] private Transform glacierEndSpawn;
        [Tooltip("Optional city entrance spawn. If empty, CityMainEntrance is used.")]
        [SerializeField] private Transform cityMainEntranceSpawn;

        private const string VolumetricFogResourcePath = "Rendering/AERO Basic Fog";
        private const float VolumetricDensityScale = 1f;
        private const float EditingFogDensity = 0f;
        private static readonly int FogColourId = Shader.PropertyToID("_Colour");

        private Material volumetricFogMaterial;
        private Color defaultFogColor;
        private Color fogBlendTarget;
        private float fogColorBlendAmount;
        private bool hasDefaultFogColor;
        private bool echoUnlocked;
        private bool developmentStateApplied;
        private int completedWorldReverberations;
        private PlayerCheckpoint currentCheckpoint = PlayerCheckpoint.Monument;
        private Canvas checkpointFadeCanvas;
        private CanvasGroup checkpointFadeGroup;
        private bool checkpointTransitionActive;

        public Font HeaderFont => headerFont;
        public Font BodyFont => bodyFont;
        public Font InteractionFont => interactionFont != null ? interactionFont : bodyFont;
        public bool FogEnabled => enableFogAtGameStart;
        public float FogStrength => fogStrength;
        public Color GlacierFogColor => glacierFogColor;
        public bool EchoUnlocked => echoUnlocked;
        public bool DevelopmentToolsEnabled => enableDevelopmentTools;
        public int CompletedWorldReverberations => completedWorldReverberations;
        public PlayerCheckpoint CurrentCheckpoint => currentCheckpoint;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("More than one GameManager exists. The first instance will remain active.", this);
                return;
            }

            Instance = this;
            currentCheckpoint = PlayerCheckpoint.Monument;
            EnsureEchoIgnored("GlacierAreaOuter");
            EnsureEchoIgnored("GlacierAreaInner");
            echoUnlocked = startWithEchoUnlocked;
            volumetricFogMaterial = Resources.Load<Material>(VolumetricFogResourcePath);
            CaptureDefaultFogColor();
            ApplyFogSettings();

            if (DevelopmentToolsEnabled && GetComponent<RuntimeDebugConsole>() == null)
            {
                gameObject.AddComponent<RuntimeDebugConsole>();
            }
        }

        public void UnlockEcho()
        {
            echoUnlocked = true;
        }

        public void RegisterWorldReverberationCompletion()
        {
            completedWorldReverberations++;
        }

        public void ApplyDevelopmentStartState()
        {
            if (!DevelopmentToolsEnabled || developmentStateApplied)
            {
                return;
            }

            developmentStateApplied = true;

            if (tutorialCompleted)
            {
                ApplyTutorialCompletedState();
            }

            Transform destination;
            switch (initialSpawnPoint)
            {
                case DevelopmentSpawnPoint.CityMainEntrance:
                    SetCheckpoint(PlayerCheckpoint.CityMainEntrance);
                    destination = GetCurrentCheckpointTransform();
                    break;
                case DevelopmentSpawnPoint.GlacierStart:
                    SetCheckpoint(PlayerCheckpoint.GlacierStart);
                    destination = GetCurrentCheckpointTransform();
                    break;
                case DevelopmentSpawnPoint.GlacierMid:
                    SetCheckpoint(PlayerCheckpoint.GlacierMid);
                    destination = GetCurrentCheckpointTransform();
                    break;
                case DevelopmentSpawnPoint.GlacierEnd:
                    SetCheckpoint(PlayerCheckpoint.GlacierEnd);
                    destination = GetCurrentCheckpointTransform();
                    break;
                default:
                    SetCheckpoint(PlayerCheckpoint.Monument);
                    destination = GetCurrentCheckpointTransform();
                    break;
            }

            if (destination != null)
            {
                FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
                player?.TeleportTo(destination);
            }
        }

        public void ApplyTutorialCompletedState()
        {
            echoUnlocked = true;
            completedWorldReverberations = Mathf.Max(1, completedWorldReverberations);
            ScreenPromptUI.DismissEchoTutorial();

            GameObject tabletObject = GameObject.Find("TabletMain");
            ReadableTextInteractable tablet = tabletObject != null
                ? tabletObject.GetComponent<ReadableTextInteractable>()
                : null;
            tablet?.ApplyCompletedState(false, false);

            ReadableTextInteractable.OpenHiddenDoorStone();
        }

        public Transform GetPointOfInterest(string pointName)
        {
            if (string.Equals(pointName, "citymainentrance", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveCheckpoint(PlayerCheckpoint.CityMainEntrance);
            }

            if (string.Equals(pointName, "centermonument", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveCheckpoint(PlayerCheckpoint.Monument);
            }

            if (string.Equals(pointName, "glacierstart", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveCheckpoint(PlayerCheckpoint.GlacierStart);
            }

            if (string.Equals(pointName, "glaciermid", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveCheckpoint(PlayerCheckpoint.GlacierMid);
            }

            if (string.Equals(pointName, "glacierend", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveCheckpoint(PlayerCheckpoint.GlacierEnd);
            }

            return null;
        }

        public bool SetCheckpoint(PlayerCheckpoint checkpoint)
        {
            Transform resolved = ResolveCheckpoint(checkpoint);
            if (resolved == null)
            {
                Debug.LogWarning($"Checkpoint '{checkpoint}' could not find its scene locator.", this);
                return false;
            }

            currentCheckpoint = checkpoint;
            return true;
        }

        public void RegisterGlacierDeath()
        {
            if (currentCheckpoint == PlayerCheckpoint.Monument)
            {
                SetCheckpoint(PlayerCheckpoint.GlacierStart);
            }
        }

        public Transform GetCurrentCheckpointTransform()
        {
            return ResolveCheckpoint(currentCheckpoint);
        }

        public bool TeleportPlayerToCheckpoint(FirstPersonController player = null)
        {
            Transform destination = GetCurrentCheckpointTransform();
            return TeleportPlayerWithFade(player, destination);
        }

        public bool TeleportPlayerWithFade(FirstPersonController player, Transform destination)
        {
            if (checkpointTransitionActive)
            {
                return false;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<FirstPersonController>();
            }

            if (destination == null || player == null)
            {
                Debug.LogWarning("Travel transition could not find the player or destination.", this);
                return false;
            }

            StartCoroutine(CheckpointTeleportRoutine(player, destination));
            return true;
        }

        private IEnumerator CheckpointTeleportRoutine(FirstPersonController player, Transform destination)
        {
            checkpointTransitionActive = true;

            // Menus restore gameplay state immediately after invoking travel.
            // Waiting one frame lets this transition capture and temporarily
            // override that restored state instead of fighting it.
            yield return null;

            bool previousMovementState = player.playerCanMove;
            bool previousCameraState = player.cameraCanMove;
            player.playerCanMove = false;
            player.cameraCanMove = false;

            EnsureCheckpointFadeOverlay();
            yield return FadeCheckpointOverlay(0f, 1f, checkpointFadeToBlackDuration);

            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            player.TeleportTo(destination);
            Physics.SyncTransforms();

            if (checkpointBlackHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(checkpointBlackHoldDuration);
            }

            yield return FadeCheckpointOverlay(1f, 0f, checkpointFadeFromBlackDuration);

            player.playerCanMove = previousMovementState;
            player.cameraCanMove = previousCameraState;
            checkpointTransitionActive = false;
        }

        private IEnumerator FadeCheckpointOverlay(float from, float to, float duration)
        {
            checkpointFadeCanvas.gameObject.SetActive(true);
            checkpointFadeGroup.alpha = from;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                checkpointFadeGroup.alpha = Mathf.SmoothStep(from, to, progress);
                yield return null;
            }

            checkpointFadeGroup.alpha = to;
            if (to <= 0f)
            {
                checkpointFadeCanvas.gameObject.SetActive(false);
            }
        }

        private void EnsureCheckpointFadeOverlay()
        {
            if (checkpointFadeCanvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Checkpoint Fade (Runtime)")
            {
                hideFlags = HideFlags.DontSave
            };
            canvasObject.transform.SetParent(transform, false);

            checkpointFadeCanvas = canvasObject.AddComponent<Canvas>();
            checkpointFadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            checkpointFadeCanvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            checkpointFadeGroup = canvasObject.AddComponent<CanvasGroup>();
            checkpointFadeGroup.alpha = 0f;
            checkpointFadeGroup.interactable = false;
            checkpointFadeGroup.blocksRaycasts = false;

            GameObject imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            imageObject.GetComponent<Image>().color = Color.black;

            canvasObject.SetActive(false);
        }

        private Transform ResolveCheckpoint(PlayerCheckpoint checkpoint)
        {
            return checkpoint switch
            {
                PlayerCheckpoint.CityMainEntrance => ResolveTransform(cityMainEntranceSpawn, "CityMainEntrance"),
                PlayerCheckpoint.GlacierStart => ResolveTransform(
                    glacierStartSpawn, "GlacierRespawnStart", "GlacierRespawn"),
                PlayerCheckpoint.GlacierMid => ResolveTransform(
                    glacierMidSpawn, "GlacierRespawnMid"),
                PlayerCheckpoint.GlacierEnd => ResolveTransform(
                    glacierEndSpawn, "GlacierRespawnEnd", "GlacierEnd"),
                _ => ResolveTransform(
                    centerMonumentSpawn, "MonumentSpawn", "CenterMonumentSpawn", "CenterSpawn")
            };
        }

        private static Transform ResolveTransform(Transform assigned, params string[] objectNames)
        {
            if (assigned != null)
            {
                return assigned;
            }

            foreach (string objectName in objectNames)
            {
                GameObject found = GameObject.Find(objectName);
                if (found != null)
                {
                    return found.transform;
                }
            }

            return null;
        }

        private static void EnsureEchoIgnored(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null && target.GetComponent<IgnoreEcholocation>() == null)
            {
                target.AddComponent<IgnoreEcholocation>();
            }
        }

        public void SetFogEnabled(bool enabled)
        {
            enableFogAtGameStart = enabled;
            ApplyFogSettings();
        }

        public void SetFogStrength(float strength)
        {
            fogStrength = Mathf.Clamp(strength, 0f, 0.5f);
            ApplyFogSettings();
        }

        public void SetFogColorBlend(Color targetColor, float blendAmount)
        {
            fogBlendTarget = targetColor;
            fogColorBlendAmount = Mathf.Clamp01(blendAmount);
            ApplyFogColor();
        }

        private void ApplyFogSettings()
        {
            // AERO supplies the visible fog. Keep Unity's legacy distance fog off
            // so the two systems do not get composited over each other.
            RenderSettings.fog = false;

            if (volumetricFogMaterial == null)
            {
                volumetricFogMaterial = Resources.Load<Material>(VolumetricFogResourcePath);
            }

            if (volumetricFogMaterial != null)
            {
                float density = enableFogAtGameStart ? fogStrength * VolumetricDensityScale : 0f;
                volumetricFogMaterial.SetFloat("_Density", density);
                CaptureDefaultFogColor();
                ApplyFogColor();
            }
        }

        private void CaptureDefaultFogColor()
        {
            if (hasDefaultFogColor || volumetricFogMaterial == null ||
                !volumetricFogMaterial.HasProperty(FogColourId))
            {
                return;
            }

            defaultFogColor = volumetricFogMaterial.GetColor(FogColourId);
            fogBlendTarget = defaultFogColor;
            hasDefaultFogColor = true;
        }

        private void ApplyFogColor()
        {
            if (!hasDefaultFogColor || volumetricFogMaterial == null)
            {
                return;
            }

            Color activeColor = Color.Lerp(defaultFogColor, fogBlendTarget, fogColorBlendAmount);
            volumetricFogMaterial.SetColor(FogColourId, activeColor);
        }

        private void OnValidate()
        {
            fogStrength = Mathf.Clamp(fogStrength, 0f, 0.5f);
            checkpointFadeToBlackDuration = Mathf.Max(0.01f, checkpointFadeToBlackDuration);
            checkpointBlackHoldDuration = Mathf.Max(0f, checkpointBlackHoldDuration);
            checkpointFadeFromBlackDuration = Mathf.Max(0.01f, checkpointFadeFromBlackDuration);

            if (Application.isPlaying && Instance == this)
            {
                ApplyFogSettings();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                RenderSettings.fog = false;

                if (volumetricFogMaterial != null)
                {
                    // Resources.Load returns the shared material asset. Restore its
                    // editing state so Scene view stays clear after Play Mode ends.
                    volumetricFogMaterial.SetFloat("_Density", EditingFogDensity);
                    if (hasDefaultFogColor)
                    {
                        volumetricFogMaterial.SetColor(FogColourId, defaultFogColor);
                    }
                }

                Instance = null;
            }
        }
    }
}
