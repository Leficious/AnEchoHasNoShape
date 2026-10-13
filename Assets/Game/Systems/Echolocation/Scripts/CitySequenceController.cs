using UnityEngine;
using AnEchoHasNoShape.Interaction;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    public sealed class CitySequenceController : MonoBehaviour
    {
        [SerializeField] private Color architectColor = new Color(0.2f, 1f, 0.38f);
        [SerializeField] private Color museColor = new Color(0.45f, 0.8f, 1f);
        [SerializeField] private Color rulerColor = new Color(1f, 0.16f, 0.04f);
        [Tooltip("Permanent echo color for CityCenterTablet, independent of character and world pulse colors.")]
        [SerializeField] private Color centerTabletGold = new Color(1f, 0.86f, 0.36f);
        [SerializeField, Min(1f)] private float pulseSpeed = 35f;
        [SerializeField, Min(0.1f)] private float failureFadeDuration = 0.65f;
        [SerializeField, Min(1f)] private float failureWaveSpeed = 45f;
        [SerializeField, Min(0.1f)] private float failureWaveWidth = 8f;
        private bool spatialFailure;
        private Vector3 failureOrigin;
        private float failureWaveDuration;
        private EchoReactiveSurface[] surfaces;
        private readonly Vector4[] waveOrigins = new Vector4[5];
        private readonly Vector4[] waveColors = new Vector4[5];
        private readonly Vector4[] waveSettings = new Vector4[5];
        private ReadableTextInteractable centerReadable;
        private Transform centerTabletTransform;
        private int waveCount, lastCharacter = -1;
        private bool validChain = true;
        private Collider outerBounds;
        private Collider innerBounds;
        private Transform player;
        private bool wasInside;
        private Vector3 origin;
        private float elapsed, waveDuration, failureStart;
        private float activeWaveSpeed;
        private int stage;
        private bool waving, failing, completed, goldStarted;

        public static void Install(GameObject city)
        {
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                string name = candidate.name.ToLowerInvariant();
                if (name != "cityinner" && name != "cityinnerbounds" && name != "cityouterbounds") continue;
                if (candidate.GetComponent<EchoRenderingExclusion>() == null)
                    candidate.gameObject.AddComponent<EchoRenderingExclusion>();
                foreach (EchoReactiveSurface surface in candidate.GetComponentsInChildren<EchoReactiveSurface>(true)) Destroy(surface);
                foreach (Collider collider in candidate.GetComponents<Collider>()) collider.isTrigger = true;
            }
            if (city == null || city.GetComponent<CitySequenceController>() != null) return;
            city.AddComponent<CitySequenceController>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShaderState()
        {
            Shader.SetGlobalInt("_CityWaveCount", 0);
            Shader.SetGlobalFloat("_CitySequenceVisibility", 0f);
            Shader.SetGlobalFloat("_CityFailureActive", 0f);
        }

        private void Start()
        {
            surfaces = GetComponentsInChildren<EchoReactiveSurface>(true);
            GameObject centerTablet = GameObject.Find("CityCenterTablet");
            if (centerTablet != null)
            {
                centerTabletTransform = centerTablet.transform;
                centerReadable = centerTablet.GetComponentInChildren<ReadableTextInteractable>(true);
                if (centerReadable != null) centerReadable.ReadingCompleted += TriggerNeutralEcho;
                EchoColorOverride colorOverride = centerTablet.GetComponentInChildren<EchoColorOverride>(true);
                Color gold = centerTabletGold;
                if (colorOverride != null) colorOverride.SetEchoColor(gold);
                foreach (EchoReactiveSurface surface in centerTablet.GetComponentsInChildren<EchoReactiveSurface>(true))
                    surface.SetCityCharacterColor(gold, false);
                foreach (EchoInteractionGate gate in centerTablet.GetComponentsInChildren<EchoInteractionGate>(true))
                    gate.SetCityRevealSource(this);
            }
            foreach (Collider candidate in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
            {
                if (candidate.name.Equals("CityOuterBounds", System.StringComparison.OrdinalIgnoreCase)) outerBounds = candidate;
                if (candidate.name.Equals("CityInner", System.StringComparison.OrdinalIgnoreCase) ||
                    candidate.name.Equals("CityInnerBounds", System.StringComparison.OrdinalIgnoreCase)) innerBounds = candidate;
            }
            if (innerBounds == null) Debug.LogWarning("City fog needs a CityInner or CityInnerBounds collider.", this);
            if (outerBounds == null) Debug.LogWarning("City sequence cannot reset on exit: CityOuterBounds collider missing.", this);
            string[] names = { "Muse", "Ruler", "Architect" };
            for (int i = 0; i < names.Length; i++)
            {
                Transform group = transform.Find(names[i] + "Group");
                if (group == null) { Debug.LogWarning($"Missing city perspective group: {names[i]}Group", this); continue; }
                CityPerspectiveGroup perspective = group.GetComponent<CityPerspectiveGroup>();
                if (perspective == null) perspective = group.gameObject.AddComponent<CityPerspectiveGroup>();
                perspective.Configure(this, i + 1);
            }
            for (int i = 0; i < names.Length; i++)
            {
                GameObject actor = GameObject.Find(names[i]);
                if (actor == null) { Debug.LogWarning($"City character missing: {names[i]}", this); continue; }
                CityCharacterInteractable interaction = actor.GetComponent<CityCharacterInteractable>();
                if (interaction == null) interaction = actor.AddComponent<CityCharacterInteractable>();
                interaction.Configure(this, i, names[i]);
                Color identityColor = i == 0 ? museColor : i == 1 ? rulerColor : architectColor;
                interaction.ConfigureSkinnedAppearance(identityColor);
                foreach (EchoReactiveSurface surface in actor.GetComponentsInChildren<EchoReactiveSurface>(true))
                    surface.SetCityCharacterColor(identityColor);
                if (actor.GetComponentInChildren<Collider>() == null)
                {
                    BoxCollider collider = actor.AddComponent<BoxCollider>();
                    Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
                    Renderer[] renderers = actor.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds world = renderers[0].bounds;
                        foreach (Renderer renderer in renderers) world.Encapsulate(renderer.bounds);
                        bounds = new Bounds(actor.transform.InverseTransformPoint(world.center), Vector3.zero);
                        for (int corner = 0; corner < 8; corner++)
                            bounds.Encapsulate(actor.transform.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                                new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
                    }
                    collider.center = bounds.center;
                    collider.size = bounds.size;
                }
            }
        }

        public bool DebugSetEchoState(string state)
        {
            if (!isActiveAndEnabled || surfaces == null) return false;
            int character = state == "muse" ? 0 : state == "ruler" ? 1 : state == "architect" ? 2 : -1;
            if (character < 0 && state != "reset" && state != "gold" && state != "neutral") return false;

            // Every command starts a fresh state, even after permanent completion.
            // This also prevents repeated debug pulses overflowing the wave buffer.
            waving = failing = completed = goldStarted = false;
            stage = waveCount = 0;
            lastCharacter = -1;
            validChain = true;
            elapsed = 0f;
            ResetShaderState();
            WorldReverberationController world = WorldReverberationController.Instance;
            world?.StopReverberation();
            if (state == "reset")
            {
                GameManager.Instance?.PlayCityEchoSound();
                return true;
            }
            if (state == "neutral")
            {
                TriggerNeutralEcho();
                return true;
            }
            if (state == "gold")
            {
                completed = goldStarted = true;
                stage = 3;
                world?.TriggerReverberation();
                BeginWave(world != null ? world.transform.position : transform.position,
                    world != null ? world.ReverberationColor : new Color(1f, 0.66f, 0.2f),
                    world != null ? world.PulseSpeed : pulseSpeed);
                return true;
            }
            string actorName = character == 0 ? "Muse" : character == 1 ? "Ruler" : "Architect";
            GameObject actor = GameObject.Find(actorName);
            stage = character + 1;
            lastCharacter = character;
            BeginWave(actor != null ? actor.transform.position : transform.position,
                character == 0 ? museColor : character == 1 ? rulerColor : architectColor,
                perspective: character + 1);
            return true;
        }

        public void TriggerNeutralEcho()
        {
            if (!isActiveAndEnabled || surfaces == null || completed || failing || stage > 0 || waveCount > 0) return;
            BeginWave(centerTabletTransform != null ? centerTabletTransform.position : transform.position,
                Color.white, perspective: 0);
        }

        public void Visit(int character, Vector3 position)
        {
            if (completed || failing || surfaces == null || character == lastCharacter) return;
            if (stage > 0 && (!validChain || character != stage))
            {
                BeginFailure();
                return;
            }
            if (stage == 0) validChain = character == 0;
            lastCharacter = character;
            stage++;
            BeginWave(position, character == 0 ? museColor : character == 1 ? rulerColor : architectColor, perspective: character + 1);
            completed = stage == 3;
        }

        private void BeginFailure(bool playFeedback = true)
        {
            if (completed || failing) return;
            stage = 0;
            lastCharacter = -1;
            validChain = true;
            waving = false;
            failing = true;
            failureStart = Time.time;
            spatialFailure = playFeedback;
            if (spatialFailure)
            {
                if (player == null)
                {
                    FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
                    if (controller != null) player = controller.transform;
                }
                failureOrigin = player != null ? player.position : transform.position;
                float distance = 1f;
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                    distance = Mathf.Max(distance, Vector3.Distance(failureOrigin, renderer.bounds.center) + renderer.bounds.extents.magnitude);
                if (outerBounds != null)
                    distance = Mathf.Max(distance, Vector3.Distance(failureOrigin, outerBounds.bounds.center) + outerBounds.bounds.extents.magnitude);
                failureWaveDuration = (distance + failureWaveWidth) / Mathf.Max(1f, failureWaveSpeed);
                Shader.SetGlobalVector("_CityFailureOrigin", failureOrigin);
                Shader.SetGlobalVector("_CityFailureWave", new Vector4(failureStart, failureWaveSpeed, failureWaveWidth, 0f));
                Shader.SetGlobalFloat("_CityFailureActive", 1f);
            }
            if (playFeedback) GameManager.Instance?.PlayCityEchoSound();
        }

        private void BeginWave(Vector3 position, Color color, float speedOverride = 0f, int perspective = 4)
        {
            GameManager.Instance?.PlayCityEchoSound();
            activeWaveSpeed = Mathf.Max(1f, speedOverride > 0f ? speedOverride : pulseSpeed);
            origin = position;
            elapsed = 0f;
            waveDuration = 0f;
            waving = true;
            float maxDistance = 1f;
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i] != null)
                {
                    Bounds bounds = surfaces[i].GetComponent<Renderer>().bounds;
                    maxDistance = Mathf.Max(maxDistance, Vector3.Distance(origin, bounds.center) + bounds.extents.magnitude);
                }
            waveDuration = (maxDistance + 3f) / activeWaveSpeed;
            int index = waveCount++;
            waveOrigins[index] = new Vector4(origin.x, origin.y, origin.z, Time.time);
            waveColors[index] = color;
            waveSettings[index] = new Vector4(activeWaveSpeed, maxDistance, 3f, perspective);
            Shader.SetGlobalVectorArray("_CityWaveOrigins", waveOrigins);
            Shader.SetGlobalVectorArray("_CityWaveColors", waveColors);
            Shader.SetGlobalVectorArray("_CityWaveSettings", waveSettings);
            Shader.SetGlobalInt("_CityWaveCount", waveCount);
            Shader.SetGlobalFloat("_CitySequenceVisibility", 1f);
        }

        private void Update()
        {
            if (player == null)
            {
                FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
                if (controller != null) player = controller.transform;
            }
            if (player != null && outerBounds != null && outerBounds.enabled)
            {
                bool inside = (outerBounds.ClosestPoint(player.position) - player.position).sqrMagnitude < 0.0001f;
                if (wasInside && !inside && !completed) BeginFailure(playFeedback: false);
                wasInside = inside;
            }
            if (surfaces == null || (!waving && !failing)) return;
            elapsed += Time.deltaTime;
            if (failing)
            {
                float fade = spatialFailure ? 1f : FailureVisibilityAt(transform.position);
                Shader.SetGlobalFloat("_CitySequenceVisibility", fade);
                if (spatialFailure ? Time.time - failureStart >= failureWaveDuration : fade <= 0f)
                {
                    failing = false;
                    waveCount = 0;
                    ResetShaderState();
                }
            }
            if (waving && elapsed >= waveDuration)
            {
                waving = false;
                if (completed && !goldStarted)
                {
                    goldStarted = true;
                    WorldReverberationController world = WorldReverberationController.Instance;
                    world?.TriggerReverberation();
                    GameManager.Instance?.RegisterRegionCompletion(GameManager.CompletedRegion.City);
                    BeginWave(world != null ? world.transform.position : origin,
                        world != null ? world.ReverberationColor : new Color(1f, 0.66f, 0.2f),
                        world != null ? world.PulseSpeed : pulseSpeed);
                }
            }
        }

        private void OnDisable()
        {
            ResetShaderState();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetCityFogBlend(default, 0f);
                GameManager.Instance.SetCityAmbienceWeights(Vector3.zero);
            }
        }

        private void LateUpdate()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null) return;
            float areaBlend = 0f;
            if (player != null && BoundsActive(outerBounds) && BoundsActive(innerBounds))
            {
                Vector3 position = player.position;
                float outerMargin = InteriorMargin(outerBounds, position);
                if (outerMargin > 0f)
                {
                    float innerDistance = Vector3.Distance(position, innerBounds.ClosestPoint(position));
                    areaBlend = innerDistance <= 0.0001f ? 1f : outerMargin / (outerMargin + innerDistance);
                }
            }

            // Match the echo reaching the player, not merely the interaction time.
            GameManager.CityFogPreset neutral = manager.GetCityFogPreset(0);
            GameManager.CityFogPreset current = neutral;
            Vector3 ambience = Vector3.zero;
            if (player != null)
                for (int i = 0; i < waveCount; i++)
                {
                    float radius = Mathf.Max(0f, Time.time - waveOrigins[i].w) * waveSettings[i].x;
                    float behind = radius - Vector3.Distance(player.position, (Vector3)waveOrigins[i]);
                    float arrived = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(behind / waveSettings[i].z));
                    current = BlendFog(current, manager.GetCityFogPreset(Mathf.RoundToInt(waveSettings[i].w)), arrived);
                    int perspective = Mathf.RoundToInt(waveSettings[i].w);
                    Vector3 target = perspective == 1 ? Vector3.right : perspective == 2 ? Vector3.up
                        : perspective == 3 ? Vector3.forward : Vector3.zero;
                    ambience = Vector3.Lerp(ambience, target, arrived);
                }
            if (failing)
                current = BlendFog(current, neutral, 1f - FailureVisibilityAt(player != null ? player.position : transform.position));
            manager.SetCityFogBlend(current, areaBlend);
            if (failing) ambience *= FailureVisibilityAt(player != null ? player.position : transform.position);
            manager.SetCityAmbienceWeights(ambience);
        }

        private static GameManager.CityFogPreset BlendFog(GameManager.CityFogPreset a, GameManager.CityFogPreset b, float t)
        {
            return new GameManager.CityFogPreset(Color.Lerp(a.color, b.color, t),
                Mathf.Lerp(a.ambientLightScale, b.ambientLightScale, t));
        }

        private static bool BoundsActive(Collider volume)
        {
            return volume != null && volume.enabled && volume.gameObject.activeInHierarchy;
        }

        private static float InteriorMargin(Collider volume, Vector3 position)
        {
            if ((volume.ClosestPoint(position) - position).sqrMagnitude > 0.00000001f) return 0f;
            if (volume is SphereCollider sphere)
            {
                Vector3 scale = sphere.transform.lossyScale;
                float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                return Mathf.Max(0f, radius - Vector3.Distance(position, sphere.transform.TransformPoint(sphere.center)));
            }
            if (volume is BoxCollider box)
            {
                Vector3 local = box.transform.InverseTransformPoint(position) - box.center;
                Vector3 half = box.size * 0.5f;
                Vector3 scale = box.transform.lossyScale;
                return Mathf.Max(0f, Mathf.Min((half.x - Mathf.Abs(local.x)) * Mathf.Abs(scale.x),
                    (half.y - Mathf.Abs(local.y)) * Mathf.Abs(scale.y),
                    (half.z - Mathf.Abs(local.z)) * Mathf.Abs(scale.z)));
            }
            // Conservative fallback for other volume shapes.
            Bounds bounds = volume.bounds;
            Vector3 offset = position - bounds.center;
            return Mathf.Max(0f, Mathf.Min(bounds.extents.x - Mathf.Abs(offset.x),
                bounds.extents.y - Mathf.Abs(offset.y), bounds.extents.z - Mathf.Abs(offset.z)));
        }

        private void OnDestroy()
        {
            if (centerReadable != null) centerReadable.ReadingCompleted -= TriggerNeutralEcho;
        }

        public float PerspectiveVisibility(int perspective, Vector3 position)
        {
            if (!isActiveAndEnabled) return 0f;
            float amount = 0f;
            for (int i = 0; i < waveCount; i++)
            {
                float radius = Mathf.Max(0f, Time.time - waveOrigins[i].w) * waveSettings[i].x;
                float behind = radius - Vector3.Distance(position, (Vector3)waveOrigins[i]);
                float arrived = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(behind / waveSettings[i].z));
                float target = perspective == 0 || Mathf.RoundToInt(waveSettings[i].w) == perspective || waveSettings[i].w > 3.5f ? 1f : 0f;
                amount = Mathf.Lerp(amount, target, arrived);
            }
            return amount * FailureVisibilityAt(position);
        }

        private float FailureVisibilityAt(Vector3 position)
        {
            if (!failing) return 1f;
            float progress = spatialFailure
                ? ((Time.time - failureStart) * failureWaveSpeed - Vector3.Distance(position, failureOrigin)) / Mathf.Max(0.1f, failureWaveWidth)
                : (Time.time - failureStart) / Mathf.Max(0.1f, failureFadeDuration);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        }
    }
}
