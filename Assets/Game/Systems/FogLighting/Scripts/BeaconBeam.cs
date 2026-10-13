using UnityEngine;
using UnityEngine.Rendering;
using AnEchoHasNoShape.Echolocation;

namespace AnEchoHasNoShape.FogLighting
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("An Echo Has No Shape/Effects/Beacon Beam")]
    public sealed class BeaconBeam : MonoBehaviour
    {
        [Tooltip("Beam width in world metres. Object scale does not affect it.")]
        [SerializeField, Min(0.01f)] private float thickness = 1.2f;
        [SerializeField, ColorUsage(true, true)] private Color color = new Color(0.55f, 0.9f, 1f, 0.7f);
        [SerializeField, Min(0f)] private float brightness = 2f;
        [Tooltip("A tall beam, not an actual infinite light. The camera far clip still applies.")]
        [SerializeField, Min(1f)] private float height = 2000f;
        [SerializeField, Min(0f)] private float upwardSpeed = 3f;
        [SerializeField, Range(0f, 1f)] private float shimmer = 0.15f;
        [Header("Distance Fade")]
        [Tooltip("Begin fading at this fraction of the viewing camera's far clip distance. Uses radial distance, not screen depth.")]
        [SerializeField, Range(0f, 0.95f)] private float distanceFadeStart = 0.75f;
        [Tooltip("Fully invisible at this fraction of the far clip. Kept below 1 to prevent clipping at screen edges.")]
        [SerializeField, Range(0.01f, 0.99f)] private float distanceFadeEnd = 0.95f;
        [Tooltip("In Play Mode, reveal only after Glacier, City and Woods are all completed. Still visible for editing.")]
        [SerializeField] private bool requireAllRegionsCompleted;

        private GameObject generated;
        private Mesh mesh;
        private Material material;
        private MeshRenderer beamRenderer;
        private MaterialPropertyBlock properties;
        private float lastHeight = -1f, lastWidth = -1f;

        private void Update()
        {
            // Creation happens here, not OnValidate (Unity forbids hierarchy edits there).
            if (generated == null) CreateBeam();
            if (generated == null) return;
            beamRenderer.enabled = !Application.isPlaying || !requireAllRegionsCompleted
                || (GameManager.Instance != null && GameManager.Instance.AllRegionsCompleted);
            float h = Mathf.Max(1f, height), w = Mathf.Max(0.01f, thickness);
            generated.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
            if (lastHeight != h || lastWidth != w)
            {
                mesh.vertices = new[] { new Vector3(-w / 2, 0, 0), new Vector3(w / 2, 0, 0),
                    new Vector3(-w / 2, h, 0), new Vector3(w / 2, h, 0) };
                // Shader turns the ribbon to face each camera; bounds cover every direction.
                mesh.bounds = new Bounds(Vector3.up * h * 0.5f, new Vector3(w, h, w));
                lastHeight = h; lastWidth = w;
            }
            properties.SetColor("_Tint", color);
            float fadeEnd = Mathf.Clamp(distanceFadeEnd, 0.01f, 0.99f);
            properties.SetVector("_DistanceFade", new Vector4(Mathf.Clamp(distanceFadeStart, 0f, fadeEnd - 0.001f), fadeEnd, 0f, 0f));
            properties.SetVector("_Settings", new Vector4(Mathf.Max(0, brightness), Mathf.Max(0, upwardSpeed), Mathf.Clamp01(shimmer), h));
            beamRenderer.SetPropertyBlock(properties);
        }

        private void CreateBeam()
        {
            Shader shader = Resources.Load<Shader>("Rendering/BeaconBeam");
            if (shader == null) return;
            generated = new GameObject("Beacon Beam (Generated)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            generated.AddComponent<EchoRenderingExclusion>();
            mesh = new Mesh { name = "Beacon Ribbon", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new Vector3[4];
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            generated.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            beamRenderer = generated.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = material;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;
            properties = new MaterialPropertyBlock();
            lastHeight = lastWidth = -1f;
        }

        private void OnDisable()
        {
            Release(generated); Release(mesh); Release(material);
            generated = null; mesh = null; material = null;
        }

        private static void Release(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}
