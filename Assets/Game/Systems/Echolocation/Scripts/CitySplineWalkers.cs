using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent, RequireComponent(typeof(SplineContainer))]
    public sealed class CitySplineWalkers : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float speed = 1.4f;
        [SerializeField, Min(0.5f)] private float spacing = 7f;
        [SerializeField, Min(0.1f)] private float fadeDistance = 3f;
        [SerializeField, Min(0.1f)] private float height = 1.8f;
        [SerializeField, Min(0.05f)] private float diameter = 0.55f;
        [SerializeField] private float groundOffset;
        [SerializeField] private Color color = new Color(0.7f, 0.85f, 0.9f, 0.8f);
        [SerializeField, Range(1, 128)] private int maxWalkersPerSpline = 48;
        [Header("Figure Variation")]
        [Tooltip("Maximum world-space offset on each horizontal axis. Never offsets the feet vertically.")]
        [SerializeField, Min(0f)] private float horizontalOffset = 0.4f;
        [Tooltip("Uniform size variation; 0.12 means up to 12 percent larger or smaller.")]
        [SerializeField, Range(0f, 0.4f)] private float sizeVariation = 0.12f;
        [Tooltip("Starting-distance jitter as a fraction of each figure's spacing. Kept below half to preserve ordering.")]
        [SerializeField, Range(0f, 0.4f)] private float spacingVariation = 0.2f;
        [SerializeField] private int randomSeed = 173;
        [Header("Character Models")]
        [Tooltip("Equal probability for each assigned prefab. Empty list uses the cylinder placeholder.")]
        [SerializeField] private GameObject[] characterPrefabs = new GameObject[0];
        [SerializeField, HideInInspector] private bool characterPrefabsConfigured;
        private sealed class Route
        {
            public Vector3[] points;
            public float[] distances;
            public float length;
            public bool closed;
            public Transform[] walkers;
            public Renderer[][] renderers;
            public float[] modelHeights;
            public Vector2[] offsets;
            public float[] sizes;
            public float[] spacingJitter;
        }
        private readonly List<Route> routes = new List<Route>();
        private GameObject generated;
        private Material material;
        private MaterialPropertyBlock properties;
        private float travel, age;

        private void OnEnable()
        {
            if (Application.isPlaying) Rebuild();
        }

        [ContextMenu("Rebuild Walkers (Play Mode)")]
        public void Rebuild()
        {
            if (!Application.isPlaying) return;
            CleanUp();
            Shader shader = Resources.Load<Shader>("Rendering/CitySplineGhost");
            if (shader == null) { Debug.LogError("Missing CitySplineGhost shader.", this); return; }
            material = new Material(shader);
            properties = new MaterialPropertyBlock();
            generated = new GameObject("Spline Walkers (Runtime)");
            generated.hideFlags = HideFlags.DontSave;
            generated.AddComponent<EchoRenderingExclusion>();
            var container = GetComponent<SplineContainer>();
            var measuredPrefabs = new Dictionary<GameObject, Bounds>();
            travel = age = 0f;
            for (int spline = 0; spline < container.Splines.Count; spline++)
            {
                if (container.Splines[spline].Count < 2) continue;
                const int samples = 512;
                var route = new Route { points = new Vector3[samples + 1], distances = new float[samples + 1], closed = container.Splines[spline].Closed };
                for (int i = 0; i <= samples; i++)
                {
                    route.points[i] = container.EvaluatePosition(spline, i / (float)samples);
                    if (i > 0) route.distances[i] = route.distances[i - 1] + Vector3.Distance(route.points[i - 1], route.points[i]);
                }
                route.length = route.distances[samples];
                if (route.length < 0.01f) continue;
                int count = Mathf.Clamp(Mathf.CeilToInt(route.length / Mathf.Max(0.5f, spacing)), 1, maxWalkersPerSpline);
                route.walkers = new Transform[count];
                route.renderers = new Renderer[count][];
                route.modelHeights = new float[count];
                route.offsets = new Vector2[count];
                route.sizes = new float[count];
                route.spacingJitter = new float[count];
                // Local RNG keeps rebuilds repeatable without affecting gameplay randomness.
                var random = new System.Random(unchecked(randomSeed + spline * 7919
                    + Mathf.RoundToInt(route.points[0].x * 100f) * 31
                    + Mathf.RoundToInt(route.points[0].z * 100f)));
                for (int i = 0; i < count; i++)
                {
                    route.offsets[i] = new Vector2(RandomSigned(random), RandomSigned(random));
                    route.sizes[i] = RandomSigned(random);
                    route.spacingJitter[i] = RandomSigned(random);
                    GameObject walker = new GameObject();
                    walker.name = $"Walker {spline + 1}-{i + 1}";
                    walker.hideFlags = HideFlags.DontSave;
                    walker.transform.SetParent(generated.transform, false);
                    var choices = new List<GameObject>();
                    foreach (GameObject prefab in characterPrefabs)
                        if (prefab != null) choices.Add(prefab);
                    GameObject model;
                    GameObject chosenPrefab = null;
                    if (choices.Count > 0)
                    {
                        chosenPrefab = choices[random.Next(choices.Count)];
                        model = Instantiate(chosenPrefab, walker.transform, false);
                        model.transform.localPosition = Vector3.zero;
                        model.transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        model.transform.SetParent(walker.transform, false);
                        model.transform.localScale = new Vector3(diameter, 0.9f, diameter);
                    }
                    foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
                    {
                        body.isKinematic = true;
                        body.detectCollisions = false;
                    }
                    foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
                    route.walkers[i] = walker.transform;
                    route.renderers[i] = model.GetComponentsInChildren<Renderer>(true);
                    Bounds bounds = default;
                    bool cachedBounds = chosenPrefab != null && measuredPrefabs.TryGetValue(chosenPrefab, out bounds);
                    bool hasBounds = cachedBounds;
                    foreach (Renderer renderer in route.renderers[i])
                    {
                        if (!cachedBounds && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        {
                            Bounds visibleBounds = MeasureVisibleBounds(renderer);
                            if (!hasBounds) bounds = visibleBounds;
                            else bounds.Encapsulate(visibleBounds);
                            hasBounds = true;
                        }
                        var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                        for (int slot = 0; slot < materials.Length; slot++) materials[slot] = material;
                        renderer.sharedMaterials = materials;
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                    }
                    route.modelHeights[i] = hasBounds ? Mathf.Max(0.01f, bounds.size.y) : 1.8f;
                    if (chosenPrefab != null && hasBounds && !cachedBounds) measuredPrefabs[chosenPrefab] = bounds;
                    if (hasBounds) model.transform.localPosition -= Vector3.up * bounds.min.y;
                }
                routes.Add(route);
            }
            UpdateWalkers();
        }

        private void Update()
        {
            travel += Mathf.Max(0f, speed) * Time.deltaTime;
            age += Time.deltaTime;
            UpdateWalkers();
        }

        private void UpdateWalkers()
        {
            // Generated figures live outside the authored hierarchy, so explicitly
            // forward the spline's group identity to their shared ghost shader.
            int perspective = 0;
            for (Transform parent = transform; parent != null; parent = parent.parent)
            {
                if (parent.name.Equals("RulerGroup", System.StringComparison.OrdinalIgnoreCase)) { perspective = 2; break; }
                if (parent.name.Equals("MuseGroup", System.StringComparison.OrdinalIgnoreCase)) { perspective = 1; break; }
                if (parent.name.Equals("ArchitectGroup", System.StringComparison.OrdinalIgnoreCase)) { perspective = 3; break; }
            }
            foreach (Route route in routes)
            for (int i = 0; i < route.walkers.Length; i++)
            {
                float interval = route.length / route.walkers.Length;
                float distance = Mathf.Repeat(travel + (i + route.spacingJitter[i] * Mathf.Clamp(spacingVariation, 0f, 0.4f)) * interval, route.length);
                int upper = System.Array.BinarySearch(route.distances, distance);
                if (upper < 0) upper = ~upper;
                upper = Mathf.Clamp(upper, 1, route.points.Length - 1);
                Vector3 direction = route.points[upper] - route.points[upper - 1];
                float t = Mathf.InverseLerp(route.distances[upper - 1], route.distances[upper], distance);
                Transform walker = route.walkers[i];
                float scale = 1f + route.sizes[i] * Mathf.Clamp(sizeVariation, 0f, 0.4f);
                Vector2 offset = route.offsets[i] * Mathf.Max(0f, horizontalOffset);
                // Wrappers are foot-anchored; preserve authored proportions (including the boy).
                walker.position = Vector3.Lerp(route.points[upper - 1], route.points[upper], t)
                    + new Vector3(offset.x, groundOffset, offset.y);
                Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up);
                if (forward.sqrMagnitude > 0.00001f) walker.rotation = Quaternion.LookRotation(forward, Vector3.up);
                float modelScale = scale * Mathf.Max(0.01f, height) / 1.8f;
                walker.localScale = Vector3.one * modelScale;
                float fade = Mathf.Min(Mathf.Max(0.1f, fadeDistance), route.length * 0.5f);
                float alpha = route.closed ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Min(distance, route.length - distance) / fade);
                Color tint = color;
                tint.a *= alpha * Mathf.SmoothStep(0f, 1f, age);
                properties.SetColor("_Tint", tint);
                properties.SetFloat("_CityPerspective", perspective);
                properties.SetVector("_FigureHeight", new Vector4(walker.position.y,
                    route.modelHeights[i] * modelScale, 0f, 0f));
                foreach (Renderer renderer in route.renderers[i]) renderer.SetPropertyBlock(properties);
            }
        }

        private static Bounds MeasureVisibleBounds(Renderer renderer)
        {
            if (!(renderer is SkinnedMeshRenderer skinned)) return renderer.bounds;
            // Animation culling bounds can extend well below the feet. Measure the
            // actual pose once per prefab/rebuild, not per figure or per frame.
            Mesh pose = new Mesh();
            try
            {
                skinned.BakeMesh(pose);
                Vector3[] vertices = pose.vertices;
                if (vertices.Length == 0) return renderer.bounds;
                Bounds bounds = new Bounds(skinned.transform.TransformPoint(vertices[0]), Vector3.zero);
                for (int i = 1; i < vertices.Length; i++)
                    bounds.Encapsulate(skinned.transform.TransformPoint(vertices[i]));
                return bounds;
            }
            finally { Destroy(pose); }
        }

        private static float RandomSigned(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

        private void OnDisable() => CleanUp();
        private void CleanUp()
        {
            if (generated != null) { generated.SetActive(false); Destroy(generated); }
            if (material != null) Destroy(material);
            routes.Clear();
        }
    }
}
