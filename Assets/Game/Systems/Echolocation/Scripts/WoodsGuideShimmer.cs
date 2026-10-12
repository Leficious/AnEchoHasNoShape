using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent, RequireComponent(typeof(SplineContainer))]
    public sealed class WoodsGuideShimmer : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float width = 0.12f;
        [SerializeField, Min(0.01f)] private float speed = 3f;
        [SerializeField, Min(0.1f)] private float pulseSpacing = 9f;
        [SerializeField, Min(0.1f)] private float fadeInSeconds = 2f;
        [Tooltip("Distance in metres over which each open spline end fades to transparent.")]
        [SerializeField, Min(0f)] private float endpointFadeDistance = 3f;
        [SerializeField] private float heightOffset = 0.08f;
        [SerializeField] private Color color = new Color(0.65f, 0.95f, 0.76f, 0.8f);
        private GameObject generated;
        private Material material;
        private readonly List<Mesh> meshes = new();

        public static void RevealRoute()
        {
            foreach (SplineContainer container in FindObjectsByType<SplineContainer>(FindObjectsInactive.Include))
            {
                if (!container.name.Equals("WoodsGuideSpline", System.StringComparison.OrdinalIgnoreCase) &&
                    !container.name.Equals("WoodsGuideSpline2", System.StringComparison.OrdinalIgnoreCase)) continue;
                var guide = container.GetComponent<WoodsGuideShimmer>();
                if (guide == null) guide = container.gameObject.AddComponent<WoodsGuideShimmer>();
                guide.Reveal();
            }
        }

        public void Reveal()
        {
            if (!Application.isPlaying || generated != null) return;
            Shader shader = Resources.Load<Shader>("Rendering/WoodsGuideShimmer");
            if (shader == null) { Debug.LogError("Missing woods guide shader.", this); return; }
            material = new Material(shader) { hideFlags = HideFlags.DontSave };
            material.SetColor("_Color", color);
            material.SetVector("_Motion", new Vector4(speed, Mathf.Max(0.1f, pulseSpacing), Time.time, Mathf.Max(0.1f, fadeInSeconds)));
            generated = new GameObject("Woods Guide (Runtime)") { hideFlags = HideFlags.DontSave };
            generated.AddComponent<EchoRenderingExclusion>();
            var container = GetComponent<SplineContainer>();
            for (int s = 0; s < container.Splines.Count; s++)
            {
                if (container.Splines[s].Count < 2) continue;
                int steps = Mathf.Clamp(Mathf.CeilToInt(container.CalculateLength(s) * 4f), 32, 16000);
                var points = new Vector3[steps + 1];
                for (int i = 0; i <= steps; i++)
                    points[i] = (Vector3)container.EvaluatePosition(s, i / (float)steps) + Vector3.up * heightOffset;
                var vertices = new Vector3[(steps + 1) * 2];
                var uv = new Vector2[vertices.Length];
                var indices = new int[steps * 6];
                float distance = 0f;
                for (int i = 0; i <= steps; i++)
                {
                    if (i > 0) distance += Vector3.Distance(points[i - 1], points[i]);
                    Vector3 tangent = points[Mathf.Min(i + 1, steps)] - points[Mathf.Max(i - 1, 0)];
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                    vertices[i * 2] = points[i] - side * width * 0.5f;
                    vertices[i * 2 + 1] = points[i] + side * width * 0.5f;
                    uv[i * 2] = new Vector2(distance, 0f);
                    uv[i * 2 + 1] = new Vector2(distance, 1f);
                    if (i == steps) continue;
                    int k = i * 6, v = i * 2;
                    indices[k] = v; indices[k + 1] = v + 2; indices[k + 2] = v + 1;
                    indices[k + 3] = v + 1; indices[k + 4] = v + 2; indices[k + 5] = v + 3;
                }
                var mesh = new Mesh { name = "Woods Guide Ribbon", hideFlags = HideFlags.DontSave };
                mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices; mesh.RecalculateBounds();
                meshes.Add(mesh);
                var ribbon = new GameObject("Route " + s);
                ribbon.transform.SetParent(generated.transform, false);
                ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = ribbon.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var properties = new MaterialPropertyBlock();
                properties.SetVector("_RouteFade", new Vector4(distance,
                    container.Splines[s].Closed ? 0f : Mathf.Min(Mathf.Max(0f, endpointFadeDistance), distance * 0.5f), 0f, 0f));
                renderer.SetPropertyBlock(properties);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void OnDestroy()
        {
            if (generated != null) Destroy(generated);
            if (material != null) Destroy(material);
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
