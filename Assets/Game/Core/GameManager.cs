using AnEchoHasNoShape.Echolocation;
using AnEchoHasNoShape.Interaction;
using UnityEngine;

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
            GlacierEnd
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

        [Header("Development Start State")]
        [Tooltip("Development builds and the Editor only. Enables start-state overrides and the slash-command console.")]
        [SerializeField] private bool enableDevelopmentTools;

        [SerializeField] private DevelopmentSpawnPoint initialSpawnPoint = DevelopmentSpawnPoint.CenterMonument;

        [Tooltip("Starts after TabletMain: echo unlocked, first reverberation counted, and HiddenDoorStone open.")]
        [SerializeField] private bool tutorialCompleted;

        [Tooltip("Optional explicit center spawn. Leaving this empty keeps the player's authored scene position.")]
        [SerializeField] private Transform centerMonumentSpawn;

        [Tooltip("Optional explicit glacier spawn. If empty, an object named GlacierRespawn is used.")]
        [SerializeField] private Transform glacierStartSpawn;

        [Tooltip("Optional glacier-end spawn. If empty, an object named GlacierEnd is used.")]
        [SerializeField] private Transform glacierEndSpawn;

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

        public Font HeaderFont => headerFont;
        public Font BodyFont => bodyFont;
        public Font InteractionFont => interactionFont != null ? interactionFont : bodyFont;
        public bool FogEnabled => enableFogAtGameStart;
        public float FogStrength => fogStrength;
        public Color GlacierFogColor => glacierFogColor;
        public bool EchoUnlocked => echoUnlocked;
        public bool DevelopmentToolsEnabled => enableDevelopmentTools && (Application.isEditor || Debug.isDebugBuild);
        public int CompletedWorldReverberations => completedWorldReverberations;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("More than one GameManager exists. The first instance will remain active.", this);
                return;
            }

            Instance = this;
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
                case DevelopmentSpawnPoint.GlacierStart:
                    destination = ResolveTransform(glacierStartSpawn, "GlacierRespawn");
                    break;
                case DevelopmentSpawnPoint.GlacierEnd:
                    destination = ResolveTransform(glacierEndSpawn, "GlacierEnd");
                    break;
                default:
                    destination = ResolveTransform(centerMonumentSpawn, "CenterMonumentSpawn");
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
            if (string.Equals(pointName, "centermonument", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveTransform(centerMonumentSpawn, "CenterMonumentSpawn");
            }

            if (string.Equals(pointName, "glacierstart", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveTransform(glacierStartSpawn, "GlacierRespawn");
            }

            if (string.Equals(pointName, "glacierend", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveTransform(glacierEndSpawn, "GlacierEnd");
            }

            return null;
        }

        private static Transform ResolveTransform(Transform assigned, string objectName)
        {
            if (assigned != null)
            {
                return assigned;
            }

            GameObject found = GameObject.Find(objectName);
            return found != null ? found.transform : null;
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
