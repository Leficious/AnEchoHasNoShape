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
        [SerializeField, Range(0f, 250f)] private float emissionRate = 72f;
        [SerializeField] private Vector3 spawnArea = new Vector3(34f, 2f, 34f);
        [SerializeField, Range(2f, 30f)] private float spawnHeight = 13f;
        [SerializeField, Range(1f, 20f)] private float particleLifetime = 8f;
        [SerializeField, Range(0.1f, 8f)] private float fallSpeed = 2.1f;
        [SerializeField] private Vector2 particleSize = new Vector2(0.045f, 0.12f);
        [SerializeField] private Color snowColor = new Color(0.83f, 0.93f, 1f, 0.72f);

        [Header("Air Movement")]
        [SerializeField] private Vector2 horizontalWind = new Vector2(0.28f, 0.11f);
        [SerializeField, Range(0f, 2f)] private float turbulence = 0.36f;
        [SerializeField, Range(0.01f, 2f)] private float turbulenceScale = 0.28f;

        private Transform player;
        private GameObject generatedObject;
        private ParticleSystem snowfall;
        private Material generatedMaterial;
        private float areaBlend;

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

            if (player == null)
            {
                FindPlayer();
            }

            if (player != null)
            {
                Vector3 playerPosition = player.position;
                generatedObject.transform.position = new Vector3(
                    playerPosition.x,
                    playerPosition.y + spawnHeight,
                    playerPosition.z);
            }
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
            emission.rateOverTime = emissionRate * areaBlend;

            if (areaBlend > 0.001f)
            {
                if (!snowfall.isPlaying)
                {
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
            main.startColor = snowColor;
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
            shape.scale = new Vector3(
                Mathf.Max(1f, spawnArea.x),
                Mathf.Max(0.1f, spawnArea.y),
                Mathf.Max(1f, spawnArea.z));

            ParticleSystem.VelocityOverLifetimeModule velocity = snowfall.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = horizontalWind.x;
            velocity.y = -fallSpeed;
            velocity.z = horizontalWind.y;

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
