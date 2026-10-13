using AnEchoHasNoShape.Echolocation;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnEchoHasNoShape.FogLighting
{
    /// <summary>
    /// A lightweight, player-following snowfall volume. Visibility and emission
    /// are driven by GlacierFogBlendZone's outer-to-inner area blend.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlacierSnowfallRig : MonoBehaviour
    {
        private const string ShaderName = "An Echo Has No Shape/Glacier Snow Particle";

        [Header("Snowfall")]
        [SerializeField, Range(0f, 250f)] private float emissionRate = 90f;
        [SerializeField] private Vector3 spawnArea = new Vector3(34f, 2f, 34f);
        [SerializeField, Range(2f, 30f)] private float spawnHeight = 13f;
        [SerializeField, Range(1f, 20f)] private float particleLifetime = 8f;
        [SerializeField, Range(0.1f, 8f)] private float fallSpeed = 2.1f;
        [SerializeField] private Vector2 particleSize = new Vector2(0.055f, 0.145f);
        [SerializeField] private Color snowColor = new Color(0.9f, 0.96f, 1f, 0.85f);

        [Header("Distance Fade (Live Preview In Play Mode)")]
        [Tooltip("Camera distance in metres where flakes begin fading.")]
        [SerializeField, Min(0f)] private float distanceFadeStart = 2f;
        [Tooltip("Camera distance in metres where flakes become invisible.")]
        [SerializeField, Min(0.01f)] private float distanceFadeEnd = 24f;
        [Tooltip("1 is a smooth fade. Higher values fade more strongly; lower values retain visibility longer.")]
        [SerializeField, Range(0.1f, 5f)] private float distanceFadePower = 2f;

        [Header("Air Movement")]
        [SerializeField] private Vector2 horizontalWind = new Vector2(0.28f, 0.11f);
        [SerializeField, Range(0f, 2f)] private float turbulence = 0.36f;
        [SerializeField, Range(0.01f, 2f)] private float turbulenceScale = 0.28f;

        private Transform player;
        private GameObject generatedObject;
        private ParticleSystem snowfall;
        private Material generatedMaterial;
        private float areaBlend;
        private ParticleSystem.Particle[] particleBuffer;

        private Vector3 CoverageSize => new Vector3(
            Mathf.Max(spawnArea.x, (distanceFadeEnd + 2f) * 2f),
            Mathf.Max(0.1f, spawnArea.y),
            Mathf.Max(spawnArea.z, (distanceFadeEnd + 2f) * 2f));

        private void PositionVolume()
        {
            if (player != null && generatedObject != null)
                generatedObject.transform.position = player.position + Vector3.up * spawnHeight;
        }

        private void RecycleSnow()
        {
            if (snowfall == null || player == null || !snowfall.isPlaying) return;
            int capacity = snowfall.main.maxParticles;
            if (particleBuffer == null || particleBuffer.Length < capacity)
                particleBuffer = new ParticleSystem.Particle[capacity];
            int count = snowfall.GetParticles(particleBuffer);
            Vector3 size = CoverageSize;
            Vector3 center = player.position;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = particleBuffer[i].position;
                p.x = center.x + Mathf.Repeat(p.x - center.x + size.x * 0.5f, size.x) - size.x * 0.5f;
                p.z = center.z + Mathf.Repeat(p.z - center.z + size.z * 0.5f, size.z) - size.z * 0.5f;
                particleBuffer[i].position = p;
            }
            snowfall.SetParticles(particleBuffer, count);
        }
        private static readonly int DistanceFadeId = Shader.PropertyToID("_DistanceFade");

        private void ApplyDistanceFade()
        {
            if (generatedMaterial == null) return;
            float start = Mathf.Max(0f, distanceFadeStart);
            generatedMaterial.SetVector(DistanceFadeId, new Vector4(start,
                Mathf.Max(start + 0.01f, distanceFadeEnd), Mathf.Clamp(distanceFadePower, 0.1f, 5f), 0f));
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            areaBlend = 0f;
            FindPlayer();
            BuildSnowfall();
        }

        private void OnDisable()
        {
            ReleaseGeneratedObjects();
        }

        private void OnDestroy()
        {
            ReleaseGeneratedObjects();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || generatedObject == null)
            {
                return;
            }

            ApplyDistanceFade();

            if (player == null)
            {
                FindPlayer();
            }

            PositionVolume();
            if (snowfall != null)
            {
                var shape = snowfall.shape;
                shape.scale = CoverageSize;
            }
            RecycleSnow();
        }

        public void SetAreaBlend(float blend)
        {
            areaBlend = Mathf.Clamp01(blend);
            if (generatedMaterial != null)
            {
                generatedMaterial.SetFloat("_AreaBlend", areaBlend);
            }

            if (snowfall == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = snowfall.emission;
            // Region opacity handles the fade; keep a populated volume even
            // at the outer edge instead of multiplying two fades together.
            emission.rateOverTime = emissionRate;

            if (areaBlend > 0.001f)
            {
                if (!snowfall.isPlaying)
                {
                    PositionVolume();
                    // Pre-simulate at the actual entry position, not the rig's
                    // scene origin. Only runs on entry, never every frame.
                    snowfall.Simulate(particleLifetime * 1.18f, true, true, true);
                    snowfall.Play();
                }
            }
            else if (snowfall.isPlaying || snowfall.particleCount > 0)
            {
                snowfall.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void BuildSnowfall()
        {
            ReleaseGeneratedObjects();

            Material shaderReference = Resources.Load<Material>("Rendering/GlacierSnowRuntime");
            Shader shader = shaderReference != null && shaderReference.shader != null
                ? shaderReference.shader
                : Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Snow shader '{ShaderName}' could not be found.", this);
                return;
            }

            generatedObject = new GameObject("Glacier Snowfall (Generated)")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = gameObject.layer
            };
            generatedObject.transform.SetParent(transform, true);
            generatedObject.AddComponent<EchoRenderingExclusion>();

            snowfall = generatedObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = snowfall.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                particleLifetime * 0.82f,
                particleLifetime * 1.18f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Min(particleSize.x, particleSize.y),
                Mathf.Max(particleSize.x, particleSize.y));
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(snowColor.r * 0.85f, snowColor.g * 0.9f, snowColor.b, snowColor.a * 0.6f), snowColor);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = Mathf.Max(
                128,
                Mathf.CeilToInt(emissionRate * particleLifetime * 1.5f));

            ParticleSystem.EmissionModule emission = snowfall.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = snowfall.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = CoverageSize;

            ParticleSystem.VelocityOverLifetimeModule velocity = snowfall.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(horizontalWind.x - 0.15f, horizontalWind.x + 0.15f);
            velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed * 1.3f, -fallSpeed * 0.7f);
            velocity.z = new ParticleSystem.MinMaxCurve(horizontalWind.y - 0.15f, horizontalWind.y + 0.15f);

            ParticleSystem.NoiseModule noise = snowfall.noise;
            noise.enabled = turbulence > 0.001f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = turbulence;
            noise.frequency = turbulenceScale;
            noise.scrollSpeed = 0.14f;
            noise.octaveCount = 2;
            noise.damping = true;

            Gradient lifetimeFade = new Gradient();
            lifetimeFade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(1f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = snowfall.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = lifetimeFade;

            generatedMaterial = new Material(shader)
            {
                name = "Glacier Snow Material (Generated)",
                hideFlags = HideFlags.HideAndDontSave
            };
            generatedMaterial.SetColor("_Tint", Color.white);
            generatedMaterial.SetFloat("_AreaBlend", areaBlend);

            ApplyDistanceFade();

            ParticleSystemRenderer particleRenderer = generatedObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.alignment = ParticleSystemRenderSpace.Facing;
            particleRenderer.sharedMaterial = generatedMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.lightProbeUsage = LightProbeUsage.Off;
            particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particleRenderer.sortingFudge = 0.15f;

            SetAreaBlend(areaBlend);
        }

        private void FindPlayer()
        {
            FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
            player = controller != null ? controller.transform : null;
        }

        private void ReleaseGeneratedObjects()
        {
            if (generatedObject != null)
            {
                DestroyGenerated(generatedObject);
            }
            generatedObject = null;
            snowfall = null;

            if (generatedMaterial != null)
            {
                DestroyGenerated(generatedMaterial);
            }
            generatedMaterial = null;
        }

        private static void DestroyGenerated(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnValidate()
        {
            distanceFadeStart = Mathf.Max(0f, distanceFadeStart);
            distanceFadeEnd = Mathf.Max(distanceFadeStart + 0.01f, distanceFadeEnd);
            distanceFadePower = Mathf.Clamp(distanceFadePower, 0.1f, 5f);
            emissionRate = Mathf.Clamp(emissionRate, 0f, 250f);
            spawnArea.x = Mathf.Max(1f, spawnArea.x);
            spawnArea.y = Mathf.Max(0.1f, spawnArea.y);
            spawnArea.z = Mathf.Max(1f, spawnArea.z);
            spawnHeight = Mathf.Clamp(spawnHeight, 2f, 30f);
            particleLifetime = Mathf.Clamp(particleLifetime, 1f, 20f);
            fallSpeed = Mathf.Clamp(fallSpeed, 0.1f, 8f);
            particleSize.x = Mathf.Max(0.005f, particleSize.x);
            particleSize.y = Mathf.Max(particleSize.x, particleSize.y);
            turbulence = Mathf.Clamp(turbulence, 0f, 2f);
            turbulenceScale = Mathf.Clamp(turbulenceScale, 0.01f, 2f);
        }
    }
}
