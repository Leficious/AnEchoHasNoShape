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
        private EchoReactiveSurface[] surfaces;
        private readonly Vector4[] waveOrigins = new Vector4[4];
        private readonly Vector4[] waveColors = new Vector4[4];
        private readonly Vector4[] waveSettings = new Vector4[4];
        private int waveCount, lastCharacter = -1;
        private bool validChain = true;
        private Collider outerBounds;
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
        }

        private void Start()
        {
            surfaces = GetComponentsInChildren<EchoReactiveSurface>(true);
            GameObject centerTablet = GameObject.Find("CityCenterTablet");
            if (centerTablet != null)
            {
                EchoColorOverride colorOverride = centerTablet.GetComponentInChildren<EchoColorOverride>(true);
                Color gold = centerTabletGold;
                if (colorOverride != null) colorOverride.SetEchoColor(gold);
                foreach (EchoReactiveSurface surface in centerTablet.GetComponentsInChildren<EchoReactiveSurface>(true))
                    surface.SetCityCharacterColor(gold, false);
                foreach (EchoInteractionGate gate in centerTablet.GetComponentsInChildren<EchoInteractionGate>(true))
                    gate.SetCityRevealSource(this);
            }
            foreach (Collider candidate in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
                if (candidate.name.Equals("CityOuterBounds", System.StringComparison.OrdinalIgnoreCase)) outerBounds = candidate;
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

        private void BeginFailure()
        {
            if (completed || failing) return;
            stage = 0;
            lastCharacter = -1;
            validChain = true;
            waving = false;
            failing = true;
            failureStart = Time.time;
        }

        private void BeginWave(Vector3 position, Color color, float speedOverride = 0f, int perspective = 4)
        {
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
                if (wasInside && !inside && !completed) BeginFailure();
                wasInside = inside;
            }
            if (surfaces == null || (!waving && !failing)) return;
            elapsed += Time.deltaTime;
            if (failing)
            {
                float fade = 1f - Mathf.SmoothStep(0f, 1f, (Time.time - failureStart) / failureFadeDuration);
                Shader.SetGlobalFloat("_CitySequenceVisibility", fade);
                if (fade <= 0f)
                {
                    failing = false;
                    waveCount = 0;
                    Shader.SetGlobalInt("_CityWaveCount", 0);
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
                    GameManager.Instance?.RegisterWorldReverberationCompletion();
                    BeginWave(world != null ? world.transform.position : origin,
                        world != null ? world.ReverberationColor : new Color(1f, 0.66f, 0.2f),
                        world != null ? world.PulseSpeed : pulseSpeed);
                }
            }
        }

        private void OnDisable()
        {
            ResetShaderState();
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
            return amount * (failing ? 1f - Mathf.SmoothStep(0f, 1f, (Time.time - failureStart) / failureFadeDuration) : 1f);
        }
    }
}
