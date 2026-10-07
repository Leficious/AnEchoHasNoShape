using UnityEngine;
using AnEchoHasNoShape.Echolocation;

namespace AnEchoHasNoShape.FogLighting
{
    [DisallowMultipleComponent]
    public sealed class GlacierFogBlendZone : MonoBehaviour
    {
        public static float CurrentAreaBlend { get; private set; }

        [Header("Blend Areas")]
        [SerializeField] private SphereCollider outerArea;
        [SerializeField] private SphereCollider innerArea;
        [System.Serializable]
        private struct AreaPair
        {
            public SphereCollider outer;
            public SphereCollider inner;
        }
        [SerializeField] private AreaPair[] additionalAreas = System.Array.Empty<AreaPair>();

        [Header("Glacier Fog")]
        [SerializeField] private bool smoothTransition = true;

        private Transform player;
        private GlacierAuroraRig auroraRig;
        private GlacierSnowfallRig snowfallRig;
        private float lastAppliedBlend = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            CurrentAreaBlend = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallFromAuthoredAreas()
        {
            GameObject outerObject = GameObject.Find("GlacierAreaOuter");
            GameObject innerObject = GameObject.Find("GlacierAreaInner");
            if (outerObject == null || innerObject == null)
            {
                return;
            }

            SphereCollider outer = outerObject.GetComponent<SphereCollider>();
            SphereCollider inner = innerObject.GetComponent<SphereCollider>();
            if (outer == null || inner == null)
            {
                Debug.LogWarning("Glacier fog areas require SphereCollider components.");
                return;
            }

            ExcludeTriggerVisualization(outerObject);
            ExcludeTriggerVisualization(innerObject);

            GlacierFogBlendZone zone = outerObject.GetComponent<GlacierFogBlendZone>();
            if (zone == null)
            {
                zone = outerObject.AddComponent<GlacierFogBlendZone>();
            }

            zone.Configure(outer, inner);
            var pairs = new System.Collections.Generic.List<AreaPair>();
            foreach (SphereCollider candidate in Object.FindObjectsByType<SphereCollider>(FindObjectsInactive.Include))
            {
                const string prefix = "GlacierAreaOuter";
                if (candidate == outer || !candidate.name.StartsWith(prefix + " (", System.StringComparison.Ordinal)) continue;
                string innerName = "GlacierAreaInner" + candidate.name.Substring(prefix.Length);
                SphereCollider pairedInner = null;
                foreach (SphereCollider match in Object.FindObjectsByType<SphereCollider>(FindObjectsInactive.Include))
                    if (match.name == innerName) { pairedInner = match; break; }
                if (pairedInner == null)
                {
                    Debug.LogWarning("Missing matching glacier sphere: " + innerName, candidate);
                    continue;
                }
                ExcludeTriggerVisualization(candidate.gameObject);
                ExcludeTriggerVisualization(pairedInner.gameObject);
                pairs.Add(new AreaPair { outer = candidate, inner = pairedInner });
            }
            zone.additionalAreas = pairs.ToArray();
        }

        private static void ExcludeTriggerVisualization(GameObject triggerObject)
        {
            if (triggerObject.GetComponent<EchoRenderingExclusion>() == null)
            {
                triggerObject.AddComponent<EchoRenderingExclusion>();
            }
            if (triggerObject.GetComponent<IgnoreEcholocation>() == null)
                triggerObject.AddComponent<IgnoreEcholocation>();

            // Handles either ordering of the two runtime installers.
            EchoReactiveSurface[] existingSurfaces = triggerObject.GetComponentsInChildren<EchoReactiveSurface>(true);
            foreach (EchoReactiveSurface surface in existingSurfaces)
            {
                Destroy(surface);
            }
        }

        public void Configure(SphereCollider outer, SphereCollider inner)
        {
            outerArea = outer;
            innerArea = inner;
        }

        private void Awake()
        {
            FindPlayer();
            FindEnvironmentRigs();
        }

        private void Update()
        {
            if (player == null)
            {
                FindPlayer();
            }

            if (auroraRig == null || snowfallRig == null)
            {
                FindEnvironmentRigs();
            }

            if (player == null || GameManager.Instance == null)
            {
                return;
            }

            float blend = EvaluatePair(outerArea, innerArea, player.position);
            foreach (AreaPair pair in additionalAreas)
                blend = Mathf.Max(blend, EvaluatePair(pair.outer, pair.inner, player.position));

            if (smoothTransition)
            {
                blend = Mathf.SmoothStep(0f, 1f, blend);
            }

            if (Mathf.Abs(blend - lastAppliedBlend) > 0.0001f)
            {
                GameManager.Instance.SetFogColorBlend(GameManager.Instance.GlacierFogColor, blend);
                lastAppliedBlend = blend;
            }

            CurrentAreaBlend = blend;

            // Keep this outside the fog-change guard so a rig discovered after
            // the first frame still receives the current area blend.
            if (auroraRig != null)
            {
                auroraRig.SetAreaBlend(blend);
            }
            if (snowfallRig != null)
            {
                snowfallRig.SetAreaBlend(blend);
            }
        }

        private void FindPlayer()
        {
            FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
            player = controller != null ? controller.transform : null;
        }

        private void FindEnvironmentRigs()
        {
            auroraRig = FindAnyObjectByType<GlacierAuroraRig>();
            snowfallRig = FindAnyObjectByType<GlacierSnowfallRig>();
        }

        private static float GetWorldRadius(SphereCollider sphere)
        {
            Vector3 scale = sphere.transform.lossyScale;
            float largestAxis = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return sphere.radius * largestAxis;
        }

        private static float EvaluatePair(SphereCollider outer, SphereCollider inner, Vector3 position)
        {
            if (outer == null || inner == null || !outer.enabled || !inner.enabled ||
                !outer.gameObject.activeInHierarchy || !inner.gameObject.activeInHierarchy) return 0f;
            float outerMargin = GetWorldRadius(outer) - Vector3.Distance(position, outer.transform.TransformPoint(outer.center));
            if (outerMargin <= 0f) return 0f;
            float innerDistance = Vector3.Distance(position, inner.transform.TransformPoint(inner.center)) - GetWorldRadius(inner);
            if (innerDistance <= 0f) return 1f;
            // Matches the original blend for concentric spheres, while honoring
            // both actual boundaries if an inner sphere is shifted slightly.
            return Mathf.Clamp01(outerMargin / (outerMargin + innerDistance));
        }

        private void OnDisable()
        {
            if (Application.isPlaying && GameManager.Instance != null)
            {
                GameManager.Instance.SetFogColorBlend(GameManager.Instance.GlacierFogColor, 0f);
            }

            if (Application.isPlaying && auroraRig != null)
            {
                auroraRig.SetAreaBlend(0f);
            }
            if (Application.isPlaying && snowfallRig != null)
            {
                snowfallRig.SetAreaBlend(0f);
            }

            lastAppliedBlend = -1f;
            CurrentAreaBlend = 0f;
        }

        private void OnValidate()
        {
            if (outerArea == null)
            {
                outerArea = GetComponent<SphereCollider>();
            }
        }
    }
}
