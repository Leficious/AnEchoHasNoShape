using UnityEngine;
using AnEchoHasNoShape.Echolocation;

namespace AnEchoHasNoShape.FogLighting
{
    [DisallowMultipleComponent]
    public sealed class GlacierFogBlendZone : MonoBehaviour
    {
        [Header("Blend Areas")]
        [SerializeField] private SphereCollider outerArea;
        [SerializeField] private SphereCollider innerArea;

        [Header("Glacier Fog")]
        [SerializeField] private bool smoothTransition = true;

        private Transform player;
        private float lastAppliedBlend = -1f;

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
        }

        private static void ExcludeTriggerVisualization(GameObject triggerObject)
        {
            if (triggerObject.GetComponent<EchoRenderingExclusion>() == null)
            {
                triggerObject.AddComponent<EchoRenderingExclusion>();
            }

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
        }

        private void Update()
        {
            if (player == null)
            {
                FindPlayer();
            }

            if (player == null || outerArea == null || innerArea == null || GameManager.Instance == null)
            {
                return;
            }

            Vector3 outerCenter = outerArea.transform.TransformPoint(outerArea.center);
            Vector3 innerCenter = innerArea.transform.TransformPoint(innerArea.center);
            float outerRadius = GetWorldRadius(outerArea);
            float innerRadius = GetWorldRadius(innerArea);

            // The authored glacier volumes are concentric. Averaging their centers
            // makes the blend remain stable if one is nudged by a small amount.
            Vector3 blendCenter = (outerCenter + innerCenter) * 0.5f;
            float distance = Vector3.Distance(player.position, blendCenter);
            float blend = Mathf.InverseLerp(outerRadius, innerRadius, distance);

            if (smoothTransition)
            {
                blend = Mathf.SmoothStep(0f, 1f, blend);
            }

            if (Mathf.Abs(blend - lastAppliedBlend) > 0.0001f)
            {
                GameManager.Instance.SetFogColorBlend(GameManager.Instance.GlacierFogColor, blend);
                lastAppliedBlend = blend;
            }
        }

        private void FindPlayer()
        {
            FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
            player = controller != null ? controller.transform : null;
        }

        private static float GetWorldRadius(SphereCollider sphere)
        {
            Vector3 scale = sphere.transform.lossyScale;
            float largestAxis = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return sphere.radius * largestAxis;
        }

        private void OnDisable()
        {
            if (Application.isPlaying && GameManager.Instance != null)
            {
                GameManager.Instance.SetFogColorBlend(GameManager.Instance.GlacierFogColor, 0f);
            }

            lastAppliedBlend = -1f;
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
