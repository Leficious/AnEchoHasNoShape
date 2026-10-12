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
            CenterMonument = 0,
            GlacierMonument = 1,
            GlacierMid = 2,
            GlacierEnd = 3,
            CityMainEntrance = 4,
            GlacierCity = 5,
            WoodsEnd = 6
        }

        public enum PlayerCheckpoint
        {
            Monument,
            GlacierStart,
            GlacierMid,
            GlacierEnd,
            CityMainEntrance,
            GlacierCity,
            GlacierMonument,
            WoodsEnd
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

        [Tooltip("Override the fog's ambient brightness inside the glacier, without changing scene mesh lighting.")]
        [SerializeField] private bool overrideGlacierFogAmbientLight;
        [Tooltip("Fog Ambient Light Scale at full glacier blend. Outside the glacier the material's original value is restored.")]
        [SerializeField, Min(0f)] private float glacierFogAmbientLightScale = 1f;

        [System.Serializable]
        public struct CityFogPreset
        {
            public Color color;
            [Min(0f)] public float ambientLightScale;
            public CityFogPreset(Color tint, float ambient) { color = tint; ambientLightScale = ambient; }
        }

        [Header("Unremembered City Fog")]
        [SerializeField] private CityFogPreset cityDefaultFog = new CityFogPreset(new Color(0.51f, 0.62f, 0.63f), 1f);
        [SerializeField] private CityFogPreset cityMuseFog = new CityFogPreset(new Color(0.45f, 0.65f, 0.8f), 1f);
        [SerializeField] private CityFogPreset cityRulerFog = new CityFogPreset(new Color(0.65f, 0.4f, 0.38f), 1f);
        [SerializeField] private CityFogPreset cityArchitectFog = new CityFogPreset(new Color(0.4f, 0.65f, 0.48f), 1f);
        [SerializeField] private CityFogPreset cityGoldFog = new CityFogPreset(new Color(0.8f, 0.7f, 0.4f), 1f);
        private CityFogPreset activeCityFog;
        private float cityFogBlend;

        public CityFogPreset GetCityFogPreset(int perspective)
        {
            switch (perspective)
            {
                case 1: return cityMuseFog;
                case 2: return cityRulerFog;
                case 3: return cityArchitectFog;
                case 4: return cityGoldFog;
                default: return cityDefaultFog;
            }
        }

        public void SetCityFogBlend(CityFogPreset preset, float blend)
        {
            activeCityFog = preset;
            cityFogBlend = Mathf.Clamp01(blend);
            ApplyFogColor();
        }

        [Header("Illusory Woods Fog")]
        [SerializeField] private Color woodsFogColor = new Color(0.4f, 0.55f, 0.48f, 1f);
        [SerializeField] private bool overrideWoodsFogAmbientLight;
        [Tooltip("Fog-only ambient brightness inside either woods inner bound. Outside the woods, normal regional lighting is restored.")]
        [SerializeField, Min(0f)] private float woodsFogAmbientLightScale = 1f;
        private readonly System.Collections.Generic.List<(BoxCollider outer, BoxCollider inner)> woodsFogAreas = new();
        private Transform woodsFogPlayer;
        private float woodsFogBlend;

        [System.Serializable]
        private sealed class RegionSoundtrack
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.5f;
            [System.NonSerialized] public AudioSource source;
            [System.NonSerialized] public float weight;
        }

        [Header("Looping Region Soundtracks")]
        [SerializeField] private RegionSoundtrack defaultSoundtrack = new RegionSoundtrack();
        [SerializeField] private RegionSoundtrack glacierSoundtrack = new RegionSoundtrack();
        [SerializeField] private RegionSoundtrack citySoundtrack = new RegionSoundtrack();
        [SerializeField] private RegionSoundtrack woodsSoundtrack = new RegionSoundtrack();
        [Header("City Character Ambience")]
        [SerializeField] private RegionSoundtrack museAmbience = new RegionSoundtrack();
        [SerializeField] private RegionSoundtrack rulerAmbience = new RegionSoundtrack();
        [SerializeField] private RegionSoundtrack architectAmbience = new RegionSoundtrack();
        private Vector3 cityAmbienceWeights;
        private AudioSource cityEchoAudio;

        [Header("City Phase Camera Rumble")]
        [SerializeField] private bool enableCityRumble = true;
        [SerializeField, Min(0.1f)] private float cityRumbleDuration = 8f;
        [SerializeField, Range(0f, 2f)] private float cityRumbleAngle = 0.22f;
        [SerializeField, Min(0.1f)] private float cityRumbleFrequency = 7f;
        private float cityRumbleElapsed = float.PositiveInfinity;
        private Transform cityRumbleCamera;
        private Quaternion rumbleBaseRotation, rumbleAppliedRotation;
        private bool rumbleApplied;

        private void RemoveCityRumble()
        {
            // Restore only our own contribution; don't overwrite another system
            // if it has since taken control (for example drowning or teleporting).
            if (rumbleApplied && cityRumbleCamera != null &&
                Quaternion.Angle(cityRumbleCamera.localRotation, rumbleAppliedRotation) < 0.001f)
                cityRumbleCamera.localRotation = rumbleBaseRotation;
            rumbleApplied = false;
        }

        private void ApplyCityRumble()
        {
            if (!enableCityRumble || cityRumbleElapsed >= cityRumbleDuration || Time.timeScale <= 0f) return;
            if (cityRumbleCamera == null)
            {
                FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
                if (controller != null && controller.playerCamera != null) cityRumbleCamera = controller.playerCamera.transform;
            }
            cityRumbleElapsed += Time.deltaTime;
            if (cityRumbleCamera == null) return;
            float progress = Mathf.Clamp01(cityRumbleElapsed / Mathf.Max(0.1f, cityRumbleDuration));
            float envelope = Mathf.SmoothStep(0f, 1f, progress / 0.2f)
                * (1f - Mathf.SmoothStep(0f, 1f, (progress - 0.65f) / 0.35f));
            float t = Time.time * cityRumbleFrequency;
            Vector3 angles = new Vector3(Mathf.PerlinNoise(t, 13.7f) - 0.5f,
                Mathf.PerlinNoise(29.1f, t) - 0.5f, (Mathf.PerlinNoise(t, 47.3f) - 0.5f) * 0.5f);
            rumbleBaseRotation = cityRumbleCamera.localRotation;
            rumbleAppliedRotation = rumbleBaseRotation * Quaternion.Euler(angles * (2f * cityRumbleAngle * envelope));
            cityRumbleCamera.localRotation = rumbleAppliedRotation;
            rumbleApplied = true;
        }

        private void OnDisable()
        {
            RemoveCityRumble();
            cityRumbleElapsed = float.PositiveInfinity;
        }

        public void SetCityAmbienceWeights(Vector3 weights)
        {
            cityAmbienceWeights = weights;
        }

        public void PlayCityEchoSound()
        {
            // Restart, never stack multiple camera offsets on rapid phase changes.
            cityRumbleElapsed = 0f;
            var library = AnEchoHasNoShape.Core.GameAudioLibrary.Load();
            if (library == null || library.CityEchoClip == null) return;
            if (cityEchoAudio == null)
            {
                GameObject audioObject = new GameObject("City Echo Audio (Runtime)");
                audioObject.transform.SetParent(transform, false);
                cityEchoAudio = audioObject.AddComponent<AudioSource>();
                cityEchoAudio.playOnAwake = false;
                cityEchoAudio.spatialBlend = 0f;
            }
            cityEchoAudio.Stop();
            cityEchoAudio.PlayOneShot(library.CityEchoClip, library.CityEchoVolume);
        }
        [Tooltip("Additional smoothing for teleports and sudden region changes. Normal fades follow the inner/outer bounds.")]
        [SerializeField, Min(0.01f)] private float soundtrackBlendSeconds = 1f;

        private void UpdateRegionSoundtracks()
        {
            // Empty regional slots leave the default playing. Normalise overlapping
            // zones so their combined weight never exceeds one.
            float glacier = glacierSoundtrack.clip != null ? Mathf.Clamp01(fogColorBlendAmount) : 0f;
            float city = citySoundtrack.clip != null ? Mathf.Clamp01(cityFogBlend) : 0f;
            float woods = woodsSoundtrack.clip != null ? Mathf.Clamp01(woodsFogBlend) : 0f;
            float outside = 1f - Mathf.Max(glacier, city, woods);
            float total = Mathf.Max(1f, outside + glacier + city + woods);
            UpdateSoundtrack(defaultSoundtrack, "Default Soundtrack", outside / total);
            UpdateSoundtrack(glacierSoundtrack, "Glacial Ocean Soundtrack", glacier / total);
            UpdateSoundtrack(citySoundtrack, "Unremembered City Soundtrack", city / total);
            UpdateSoundtrack(woodsSoundtrack, "Illusory Woods Soundtrack", woods / total);
            UpdateSoundtrack(museAmbience, "Muse Ambience", Mathf.Clamp01(cityAmbienceWeights.x) * cityFogBlend);
            UpdateSoundtrack(rulerAmbience, "Ruler Ambience", Mathf.Clamp01(cityAmbienceWeights.y) * cityFogBlend);
            UpdateSoundtrack(architectAmbience, "Architect Ambience", Mathf.Clamp01(cityAmbienceWeights.z) * cityFogBlend);
        }

        private void UpdateSoundtrack(RegionSoundtrack track, string label, float target)
        {
            if (track.source == null)
            {
                GameObject audioObject = new GameObject(label + " (Runtime)");
                audioObject.transform.SetParent(transform, false);
                track.source = audioObject.AddComponent<AudioSource>();
                track.source.playOnAwake = false;
                track.source.loop = true;
                track.source.spatialBlend = 0f;
                track.source.volume = 0f;
            }
            if (track.source.clip != track.clip)
            {
                track.source.Stop();
                track.source.clip = track.clip;
                if (track.clip != null) track.source.Play();
            }
            float smoothing = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, soundtrackBlendSeconds));
            track.weight = Mathf.Lerp(track.weight, target, smoothing);
            track.source.volume = Mathf.Clamp01(track.volume) * track.weight;
        }

        [Header("Region Titles")]
        [SerializeField, Min(0.1f)] private float regionTitleFadeIn = 1.2f;
        [SerializeField, Min(0f)] private float regionTitleHold = 3f;
        [SerializeField, Min(0.1f)] private float regionTitleFadeOut = 1.5f;
        private sealed class TitleRegion
        {
            public string title;
            public readonly System.Collections.Generic.List<Collider> inner = new();
            public readonly System.Collections.Generic.List<Collider> outer = new();
            public bool announced;
        }
        private readonly TitleRegion[] titleRegions = {
            new TitleRegion { title = "Glacial Ocean" },
            new TitleRegion { title = "Unremembered City" },
            new TitleRegion { title = "Illusory Woods" }
        };
        private Transform regionTitlePlayer;
        private Text regionTitleText;
        private CanvasGroup regionTitleGroup;
        private Coroutine regionTitleRoutine;

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

        [Tooltip("Optional monument-side glacier spawn. Otherwise uses GlacierRespawnMonument.")]
        [UnityEngine.Serialization.FormerlySerializedAs("glacierStartSpawn")]
        [SerializeField] private Transform glacierMonumentSpawn;
        [Tooltip("Optional city-side glacier spawn. Otherwise uses GlacierRespawnCity.")]
        [SerializeField] private Transform glacierCitySpawn;

        [Tooltip("Optional glacier-midpoint spawn. If empty, an object named GlacierRespawnMid is used.")]
        [SerializeField] private Transform glacierMidSpawn;

        [Tooltip("Optional glacier-end spawn. If empty, an object named GlacierRespawnEnd is used.")]
        [SerializeField] private Transform glacierEndSpawn;
        [Tooltip("Optional city entrance spawn. If empty, CityMainEntrance is used.")]
        [SerializeField] private Transform cityMainEntranceSpawn;
        [Tooltip("Optional woods-end spawn. If empty, WoodsEnd is used.")]
        [SerializeField] private Transform woodsEndSpawn;

        private const string VolumetricFogResourcePath = "Rendering/AERO Basic Fog";
        private const float VolumetricDensityScale = 1f;
        private const float EditingFogDensity = 0f;
        private static readonly int FogColourId = Shader.PropertyToID("_Colour");
        private static readonly int FogAmbientScaleId = Shader.PropertyToID("_Ambient_Light_Scale");
        private float defaultFogAmbientScale;
        private bool hasDefaultFogAmbientScale;

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
            InstallRegionTitles();
            InstallWoodsFogAreas();
            currentCheckpoint = PlayerCheckpoint.Monument;
            EnsureEchoIgnored("GlacierAreaOuter");
            EnsureEchoIgnored("GlacierAreaInner");
            InstallGlacierEntrance("GlacierCityTrigger", PlayerCheckpoint.GlacierCity);
            InstallGlacierEntrance("GlacierMonumentTrigger", PlayerCheckpoint.GlacierMonument);
            echoUnlocked = startWithEchoUnlocked;
            volumetricFogMaterial = Resources.Load<Material>(VolumetricFogResourcePath);
            CaptureDefaultFogColor();
            ApplyFogSettings();

            if (DevelopmentToolsEnabled && GetComponent<RuntimeDebugConsole>() == null)
            {
                gameObject.AddComponent<RuntimeDebugConsole>();
            }
        }

        private void InstallRegionTitles()
        {
            foreach (Collider volume in FindObjectsByType<Collider>(FindObjectsInactive.Include))
            {
                string name = volume.name.ToLowerInvariant();
                if (name == "glacierareainner" || name.StartsWith("glacierareainner (")) titleRegions[0].inner.Add(volume);
                if (name == "glacierareaouter" || name.StartsWith("glacierareaouter (")) titleRegions[0].outer.Add(volume);
                if (name == "cityinner" || name == "cityinnerbounds") titleRegions[1].inner.Add(volume);
                if (name == "cityouterbounds") titleRegions[1].outer.Add(volume);
                if (name.StartsWith("woodsinnerbounds")) titleRegions[2].inner.Add(volume);
                if (name.StartsWith("woodsoutterbounds") || name.StartsWith("woodsouterbounds")) titleRegions[2].outer.Add(volume);
            }
        }

        private static bool InsideTitleVolumes(System.Collections.Generic.List<Collider> volumes, Vector3 position)
        {
            foreach (Collider volume in volumes)
                if (volume != null && volume.enabled && volume.gameObject.activeInHierarchy &&
                    (volume.ClosestPoint(position) - position).sqrMagnitude < 0.000001f) return true;
            return false;
        }

        private void Update()
        {
            if (Instance != this) return;
            RemoveCityRumble();
            UpdateRegionSoundtracks();
            if (regionTitlePlayer == null)
            {
                FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
                if (controller != null) regionTitlePlayer = controller.transform;
            }
            if (regionTitlePlayer == null) return;
            foreach (TitleRegion region in titleRegions)
            {
                bool inside = InsideTitleVolumes(region.inner, regionTitlePlayer.position);
                // Rearm only after leaving the whole region, not at an inner seam.
                if (!inside && !InsideTitleVolumes(region.outer, regionTitlePlayer.position)) region.announced = false;
                if (!inside || region.announced) continue;
                region.announced = true;
                if (regionTitleRoutine != null) StopCoroutine(regionTitleRoutine);
                regionTitleRoutine = StartCoroutine(ShowRegionTitle(region.title));
            }
        }

        private IEnumerator ShowRegionTitle(string title)
        {
            if (regionTitleGroup == null)
            {
                GameObject root = new GameObject("Region Title (Runtime)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
                root.transform.SetParent(transform, false);
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                regionTitleGroup = root.GetComponent<CanvasGroup>();
                regionTitleGroup.blocksRaycasts = regionTitleGroup.interactable = false;
                GameObject label = new GameObject("Location", typeof(RectTransform), typeof(Text), typeof(Shadow));
                label.transform.SetParent(root.transform, false);
                RectTransform rect = label.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.1f, 1f);
                rect.anchorMax = new Vector2(0.9f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -100f);
                rect.sizeDelta = new Vector2(0f, 80f);
                regionTitleText = label.GetComponent<Text>();
                regionTitleText.font = HeaderFont != null ? HeaderFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                regionTitleText.fontSize = 38;
                regionTitleText.alignment = TextAnchor.MiddleCenter;
                regionTitleText.color = new Color(0.94f, 0.94f, 0.88f);
                regionTitleText.raycastTarget = false;
                label.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            }
            regionTitleGroup.alpha = 0f;
            regionTitleText.text = title;
            float fadeIn = Mathf.Max(0.1f, regionTitleFadeIn);
            float hold = Mathf.Max(0f, regionTitleHold);
            float fadeOut = Mathf.Max(0.1f, regionTitleFadeOut);
            for (float elapsed = 0f; elapsed < fadeIn + hold + fadeOut; elapsed += Time.deltaTime)
            {
                regionTitleGroup.alpha = elapsed < fadeIn ? Mathf.SmoothStep(0f, 1f, elapsed / fadeIn)
                    : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - fadeIn - hold) / fadeOut);
                yield return null;
            }
            regionTitleGroup.alpha = 0f;
            regionTitleRoutine = null;
        }

        private void InstallWoodsFogAreas()
        {
            BoxCollider[] boxes = FindObjectsByType<BoxCollider>(FindObjectsInactive.Include);
            foreach (BoxCollider outer in boxes)
            {
                string prefix = outer.name.StartsWith("WoodsOutterBounds", System.StringComparison.OrdinalIgnoreCase)
                    ? "WoodsOutterBounds" : "WoodsOuterBounds";
                if (!outer.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) continue;
                string innerName = "WoodsInnerBounds" + outer.name.Substring(prefix.Length);
                BoxCollider inner = System.Array.Find(boxes, box => box.name.Equals(innerName, System.StringComparison.OrdinalIgnoreCase));
                PrepareWoodsFogVolume(outer);
                if (inner == null)
                {
                    Debug.LogWarning($"Woods fog is missing {innerName}.", outer);
                    continue;
                }
                PrepareWoodsFogVolume(inner);
                woodsFogAreas.Add((outer, inner));
            }
        }

        private static void PrepareWoodsFogVolume(BoxCollider volume)
        {
            volume.isTrigger = true;
            if (volume.GetComponent<EchoRenderingExclusion>() == null)
                volume.gameObject.AddComponent<EchoRenderingExclusion>();
            foreach (EchoReactiveSurface surface in volume.GetComponentsInChildren<EchoReactiveSurface>(true))
                Destroy(surface);
            foreach (Renderer renderer in volume.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (Instance == this) ApplyCityRumble();
            if (Instance != this || woodsFogAreas.Count == 0) return;
            if (woodsFogPlayer == null)
            {
                FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
                if (controller != null) woodsFogPlayer = controller.transform;
            }
            float blend = 0f;
            if (woodsFogPlayer != null)
                foreach (var pair in woodsFogAreas)
                {
                    if (pair.outer == null || pair.inner == null || !pair.outer.enabled || !pair.inner.enabled ||
                        !pair.outer.gameObject.activeInHierarchy || !pair.inner.gameObject.activeInHierarchy) continue;
                    Vector3 local = pair.outer.transform.InverseTransformPoint(woodsFogPlayer.position) - pair.outer.center;
                    Vector3 half = pair.outer.size * 0.5f;
                    Vector3 scale = pair.outer.transform.lossyScale;
                    float margin = Mathf.Min((half.x - Mathf.Abs(local.x)) * Mathf.Abs(scale.x),
                        (half.y - Mathf.Abs(local.y)) * Mathf.Abs(scale.y),
                        (half.z - Mathf.Abs(local.z)) * Mathf.Abs(scale.z));
                    if (margin <= 0f) continue;
                    float distance = Vector3.Distance(woodsFogPlayer.position, pair.inner.ClosestPoint(woodsFogPlayer.position));
                    blend = Mathf.Max(blend, margin / (margin + distance));
                }
            woodsFogBlend = Mathf.SmoothStep(0f, 1f, blend);
            ApplyFogColor();
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
                case DevelopmentSpawnPoint.WoodsEnd:
                    SetCheckpoint(PlayerCheckpoint.WoodsEnd);
                    destination = GetCurrentCheckpointTransform();
                    break;
                case DevelopmentSpawnPoint.CityMainEntrance:
                    SetCheckpoint(PlayerCheckpoint.CityMainEntrance);
                    destination = GetCurrentCheckpointTransform();
                    break;
                case DevelopmentSpawnPoint.GlacierMonument:
                    SetCheckpoint(PlayerCheckpoint.GlacierMonument);
                    destination = GetCurrentCheckpointTransform();
                    break;
                case DevelopmentSpawnPoint.GlacierCity:
                    SetCheckpoint(PlayerCheckpoint.GlacierCity);
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
            if (string.Equals(pointName, "woodsend", System.StringComparison.OrdinalIgnoreCase))
                return ResolveCheckpoint(PlayerCheckpoint.WoodsEnd);

            if (string.Equals(pointName, "citymainentrance", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveCheckpoint(PlayerCheckpoint.CityMainEntrance);
            }

            if (string.Equals(pointName, "centermonument", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveCheckpoint(PlayerCheckpoint.Monument);
            }

            if (string.Equals(pointName, "glaciermonument", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pointName, "glacierstart", System.StringComparison.OrdinalIgnoreCase))
            {
                return ResolveCheckpoint(PlayerCheckpoint.GlacierMonument);
            }

            if (string.Equals(pointName, "glaciercity", System.StringComparison.OrdinalIgnoreCase))
                return ResolveCheckpoint(PlayerCheckpoint.GlacierCity);

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

        private void InstallGlacierEntrance(string objectName, PlayerCheckpoint checkpoint)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null) { Debug.LogWarning("Missing glacier entrance: " + objectName, this); return; }
            if (target.GetComponent<EchoRenderingExclusion>() == null) target.AddComponent<EchoRenderingExclusion>();
            EnsureEchoIgnored(objectName);
            var trigger = target.GetComponent<GlacierEntranceCheckpoint>();
            if (trigger == null) trigger = target.AddComponent<GlacierEntranceCheckpoint>();
            trigger.Configure(checkpoint);
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
                PlayerCheckpoint.WoodsEnd => ResolveTransform(woodsEndSpawn, "WoodsEnd"),
                PlayerCheckpoint.GlacierStart => ResolveTransform(
                    glacierMonumentSpawn, "GlacierRespawnMonument", "GlacierRespawnStart", "GlacierRespawn"),
                PlayerCheckpoint.GlacierCity => ResolveTransform(glacierCitySpawn, "GlacierRespawnCity"),
                PlayerCheckpoint.GlacierMonument => ResolveTransform(glacierMonumentSpawn, "GlacierRespawnMonument"),
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
            if (volumetricFogMaterial.HasProperty(FogAmbientScaleId))
            {
                defaultFogAmbientScale = volumetricFogMaterial.GetFloat(FogAmbientScaleId);
                hasDefaultFogAmbientScale = true;
            }
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
            activeColor = Color.Lerp(activeColor, activeCityFog.color, cityFogBlend);
            activeColor = Color.Lerp(activeColor, woodsFogColor, woodsFogBlend);
            volumetricFogMaterial.SetColor(FogColourId, activeColor);
            if (hasDefaultFogAmbientScale)
            {
                float ambientTarget = overrideGlacierFogAmbientLight
                    ? Mathf.Max(0f, glacierFogAmbientLightScale) : defaultFogAmbientScale;
                float ambient = Mathf.Lerp(defaultFogAmbientScale, ambientTarget, fogColorBlendAmount);
                ambient = Mathf.Lerp(ambient, Mathf.Max(0f, activeCityFog.ambientLightScale), cityFogBlend);
                if (overrideWoodsFogAmbientLight)
                    ambient = Mathf.Lerp(ambient, Mathf.Max(0f, woodsFogAmbientLightScale), woodsFogBlend);
                volumetricFogMaterial.SetFloat(FogAmbientScaleId, ambient);
            }
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
                    if (hasDefaultFogAmbientScale)
                        volumetricFogMaterial.SetFloat(FogAmbientScaleId, defaultFogAmbientScale);
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
