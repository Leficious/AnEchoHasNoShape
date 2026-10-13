using AnEchoHasNoShape.Echolocation;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnEchoHasNoShape.FogLighting
{
    [DisallowMultipleComponent]
    [AddComponentMenu("An Echo Has No Shape/Effects/Region Atmosphere VFX")]
    public sealed class RegionAtmosphereVfx : MonoBehaviour
    {
        public enum Region { City, Woods }
        [SerializeField] private Region region;
        [Header("Appearance (Live In Play Mode)")]
        [SerializeField, Range(1, 200)] private int count = 48;
        [Tooltip("Woods: color and opacity. City: only alpha controls opacity; RGB automatically follows white / Muse / Ruler / Architect / gold city echoes.")]
        [SerializeField] private Color tint = new Color(0.65f, 0.85f, 0.75f, 0.3f);
        [SerializeField, Min(0.005f)] private float width = 0.025f;
        [SerializeField, Min(0.1f)] private float length = 2.4f;
        [SerializeField, Min(1f)] private float radius = 18f;
        [SerializeField, Min(1f)] private float height = 12f;
        [SerializeField, Min(1f)] private float lifetime = 14f;
        [Header("Ground Placement")]
        [SerializeField, Min(0f)] private float groundClearance = 0.35f;
        [Tooltip("Higher values concentrate spawning closer to the ground.")]
        [SerializeField, Range(1f, 5f)] private float groundBias = 3f;
        [Header("Variation")]
        [SerializeField, Range(0f, 0.9f)] private float variation = 0.45f;
        [Tooltip("Chance of a city streak becoming a small bundle of parallel strokes.")]
        [SerializeField, Range(0f, 1f)] private float parallelStreakChance = 0.6f;
        [Header("Motion")]
        [SerializeField] private Vector3 velocity = new Vector3(0.3f, 0.02f, 0.1f);
        [SerializeField, Min(0f)] private float flowAmplitude = 0.3f;
        [Tooltip("Woods: bend frequency along the travelled wind path; motion follows Velocity. City: wave animation speed.")]
        [SerializeField, Min(0f)] private float flowSpeed = 0.7f;
        [SerializeField, Range(0f, 2f)] private float echoResponse = 0.5f;
        [Header("Distance Fade")]
        [SerializeField, Min(0f)] private float fadeStart = 3f;
        [SerializeField, Min(0.1f)] private float fadeEnd = 20f;
        [SerializeField, Range(0.1f, 5f)] private float fadePower = 1.5f;

        private const int Segments = 16;
        private struct Strand { public Vector3 position; public float age, phase, scale, width, opacity, life, speed, flow, frequency, travelled; public bool bundle; }
        private Strand[] strands;
        private Vector3[] vertices;
        private Color[] colors;
        private Mesh mesh;
        private Material material;
        private GameObject generated;
        private MeshRenderer draw;
        private Camera view;
        private System.Random random;
        private bool wasVisible;
        private Terrain[] terrains;
        private Vector3 lastWindDirection = Vector3.right;
        // Recycle only beyond the visible fade, including the ribbon's full extent.
        private float CoverageRadius => Mathf.Max(radius, fadeEnd + length + flowAmplitude * 2f + 1f);

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            random = new System.Random(region == Region.City ? 721 : 931);
            terrains = Terrain.activeTerrains;
            Build();
        }

        private float Random01() => (float)random.NextDouble();
        private float Vary() => Mathf.Lerp(1f - variation, 1f + variation, Random01());

        private float GroundAt(Vector3 position, float fallback)
        {
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 p = position - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (p.x >= 0 && p.z >= 0 && p.x <= size.x && p.z <= size.z)
                    return terrain.SampleHeight(position) + terrain.transform.position.y;
            }
            return fallback;
        }

        private void Spawn(ref Strand strand, Vector3 center, bool stagger)
        {
            float coverage = CoverageRadius;
            strand.position = center + new Vector3((Random01() * 2 - 1) * coverage,
                0f, (Random01() * 2 - 1) * coverage);
            strand.phase = Random01() * Mathf.PI * 2f;
            strand.scale = Vary(); strand.width = Vary(); strand.opacity = Mathf.Min(1f, Vary());
            strand.life = Vary(); strand.speed = Vary(); strand.flow = Vary(); strand.frequency = Vary();
            strand.bundle = region == Region.City && Random01() < parallelStreakChance;
            strand.age = stagger ? Random01() * lifetime * strand.life : 0f;
            strand.travelled = strand.age * velocity.magnitude * strand.speed;
            strand.position.y = GroundAt(strand.position, center.y - 1.7f) + groundClearance
                + (region == Region.City ? length * strand.scale * 0.5f : flowAmplitude)
                + Mathf.Pow(Random01(), groundBias) * height;
        }

        private void Build()
        {
            Release();
            Shader shader = Resources.Load<Shader>("Rendering/RegionAtmosphere");
            if (shader == null) { Debug.LogError("Missing region atmosphere shader.", this); return; }
            view = Camera.main;
            int n = Mathf.Clamp(count, 1, 200);
            strands = new Strand[n];
            vertices = new Vector3[n * (Segments + 1) * 2];
            colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[n * Segments * 6];
            for (int k = 0; k < n; k++)
            {
                Spawn(ref strands[k], view != null ? view.transform.position : transform.position, true);
                int offset = k * (Segments + 1) * 2;
                for (int j = 0; j <= Segments; j++)
                {
                    uv[offset + j * 2] = new Vector2(0, (float)j / Segments);
                    uv[offset + j * 2 + 1] = new Vector2(1, (float)j / Segments);
                    if (j == Segments) continue;
                    int t = (k * Segments + j) * 6, v = offset + j * 2;
                    triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                    triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                }
            }
            mesh = new Mesh { name = region + " Atmosphere Mesh", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic(); mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            material = new Material(shader) { hideFlags = HideFlags.DontSave };
            generated = new GameObject(region + " Atmosphere (Generated)") { hideFlags = HideFlags.HideAndDontSave };
            generated.AddComponent<EchoRenderingExclusion>();
            generated.AddComponent<MeshFilter>().sharedMesh = mesh;
            draw = generated.AddComponent<MeshRenderer>();
            draw.enabled = false;
            wasVisible = false;
            draw.sharedMaterial = material;
            draw.shadowCastingMode = ShadowCastingMode.Off;
            draw.receiveShadows = false;
            draw.lightProbeUsage = LightProbeUsage.Off;
            draw.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void LateUpdate()
        {
            if (material == null) return;
            if (view == null) view = Camera.main;
            if (view == null) return;
            if (strands.Length != Mathf.Clamp(count, 1, 200)) Build();
            var manager = GameManager.Instance;
            float blend = manager == null ? 0f : region == Region.City ? manager.CityAreaBlend : manager.WoodsAreaBlend;
            draw.enabled = blend > 0.001f;
            if (!draw.enabled) { wasVisible = false; return; }
            Vector3 center = view.transform.position;
            // An already-aged population is ready at the region boundary: no
            // multi-second wait for newly born strands to become visible.
            if (!wasVisible)
                for (int k = 0; k < strands.Length; k++) Spawn(ref strands[k], center, true);
            wasVisible = true;
            generated.transform.position = center;
            material.SetColor("_Tint", tint);
            material.SetVector("_Fade", new Vector4(fadeStart, Mathf.Max(fadeStart + 0.01f, fadeEnd), fadePower, blend));
            material.SetVector("_Style", new Vector4((float)region, echoResponse, 1, 0));
            float windSpeed = velocity.magnitude;
            if (windSpeed > 0.0001f) lastWindDirection = velocity / windSpeed;
            Vector3 windSide = Vector3.Cross(Vector3.up, lastWindDirection);
            if (windSide.sqrMagnitude < 0.001f) windSide = Vector3.Cross(Vector3.right, lastWindDirection);
            windSide.Normalize();
            Vector3 windLift = Vector3.Cross(lastWindDirection, windSide).normalized;
            for (int k = 0; k < strands.Length; k++)
            {
                ref Strand s = ref strands[k];
                s.age += Time.deltaTime;
                if (s.age >= lifetime * s.life)
                    Spawn(ref s, center, false);
                s.position += velocity * (Time.deltaTime * s.speed);
                s.travelled += windSpeed * s.speed * Time.deltaTime;
                // World-space motion, with recycling at the invisible edges of
                // a player-centered volume. Sprinting cannot leave it behind.
                float coverage = CoverageRadius;
                Vector3 previous = s.position;
                s.position.x = center.x + Mathf.Repeat(s.position.x - center.x + coverage, coverage * 2f) - coverage;
                s.position.z = center.z + Mathf.Repeat(s.position.z - center.z + coverage, coverage * 2f) - coverage;
                if (Mathf.Abs(previous.x - s.position.x) > coverage || Mathf.Abs(previous.z - s.position.z) > coverage)
                    s.position.y += GroundAt(s.position, center.y - 1.7f) - GroundAt(previous, center.y - 1.7f);
                float age = s.age / Mathf.Max(1f, lifetime * s.life);
                float alpha = s.opacity * Mathf.SmoothStep(0, 1, age / 0.18f) * (1 - Mathf.SmoothStep(0, 1, (age - 0.75f) / 0.25f));
                Vector3 axis = region == Region.City ? Vector3.up : lastWindDirection;
                Vector3 bend = region == Region.City ? Vector3.right : Vector3.up;
                for (int j = 0; j <= Segments; j++)
                {
                    float u = (float)j / Segments;
                    // Two travelling waves deform the entire strand, not just its center.
                    float phase = u * 5f * s.frequency - Time.time * flowSpeed * s.speed + s.phase;
                    float wave = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 1.7f + Time.time * flowSpeed * 0.3f);
                    Vector3 p = s.position + axis * ((u - 0.5f) * length * s.scale) + bend * (wave * flowAmplitude * s.flow);
                    Vector3 tangent = axis + bend * (flowAmplitude * 5f * Mathf.Cos(phase) / Mathf.Max(0.1f, length * s.scale));
                    if (region == Region.Woods)
                    {
                        // Sample one continuous winding travel path along the
                        // ribbon. Position, shape and tangent share the same
                        // distance coordinate, so it flows forward rather than
                        // waving sideways on an unrelated timer.
                        float along = (u - 0.5f) * length * s.scale;
                        float frequency = 5f * s.frequency * flowSpeed / Mathf.Max(0.1f, length * s.scale);
                        float pathPhase = (s.travelled + along) * frequency + s.phase;
                        float secondary = pathPhase * 1.63f + s.phase;
                        float amplitude = flowAmplitude * s.flow;
                        Vector3 offset = windSide * (Mathf.Sin(pathPhase) + 0.25f * Mathf.Sin(secondary))
                            + windLift * (0.3f * Mathf.Sin(pathPhase * 0.73f + s.phase));
                        Vector3 derivative = windSide * (Mathf.Cos(pathPhase) + 0.4075f * Mathf.Cos(secondary))
                            + windLift * (0.219f * Mathf.Cos(pathPhase * 0.73f + s.phase));
                        p = s.position + axis * along + offset * amplitude;
                        tangent = axis + derivative * (amplitude * frequency);
                    }
                    Vector3 side = Vector3.Cross(tangent, center - p).normalized;
                    if (side.sqrMagnitude < 0.01f) side = view.transform.right;
                    int v = (k * (Segments + 1) + j) * 2;
                    float halfWidth = width * s.width * 0.5f * (s.bundle ? 5f : 1f);
                    // A fine trailing end and slightly fuller leading stroke.
                    if (region == Region.Woods) halfWidth *= Mathf.Lerp(0.25f, 1f, Mathf.Sin(u * Mathf.PI));
                    vertices[v] = p - center - side * halfWidth;
                    vertices[v + 1] = p - center + side * halfWidth;
                    colors[v] = colors[v + 1] = new Color(s.bundle ? 1 : 0, s.phase / (Mathf.PI * 2f), 1, alpha);
                }
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.RecalculateBounds();
        }

        private void OnDisable() => Release();
        private void Release()
        {
            if (generated != null) Destroy(generated);
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            generated = null; mesh = null; material = null;
        }
    }
}
