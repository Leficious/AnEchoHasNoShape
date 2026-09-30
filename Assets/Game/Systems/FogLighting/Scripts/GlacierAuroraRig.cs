using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AnEchoHasNoShape.FogLighting
{
    /// <summary>
    /// Procedurally builds several animated aurora curtains and a sparse set of
    /// real URP lights. The ribbons render before AERO, while the lights are
    /// sampled by AERO's volumetric pass and illuminate the fog itself.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GlacierAuroraRig : MonoBehaviour
    {
        [Serializable]
        private sealed class RibbonProfile
        {
            public string name = "Aurora Ribbon";
            public Vector3 localPosition = new Vector3(0f, 75f, 80f);
            public Vector3 localEulerAngles;
            [Min(10f)] public float length = 160f;
            [Min(5f)] public float height = 48f;
            public float curvature = 28f;
            [Range(0f, 20f)] public float irregularity = 5f;
            public float seed = 1f;
            [ColorUsage(true, true)] public Color lowerColor = new Color(0.08f, 0.38f, 0.5f, 1f);
            [ColorUsage(true, true)] public Color upperColor = new Color(0.28f, 0.82f, 0.76f, 1f);
            [Range(0f, 1f)] public float opacity = 0.42f;
        }

        private sealed class GeneratedRibbon
        {
            public Transform transform;
            public Vector3 baseLocalPosition;
            public Quaternion baseLocalRotation;
            public float phase;
        }

        private sealed class GeneratedFogLight
        {
            public Transform transform;
            public Light light;
            public Vector3 baseLocalPosition;
            public float baseIntensity;
            public Color lowerColor;
            public Color upperColor;
            public float phase;
        }

        private const string ShaderName = "An Echo Has No Shape/Glacier Aurora Ribbon";

        [Header("Aurora Curtains")]
        [SerializeField] private RibbonProfile[] ribbons = CreateDefaultProfiles();
        [SerializeField, Range(8, 128)] private int horizontalSegments = 64;
        [SerializeField, Range(2, 24)] private int verticalSegments = 10;

        [Header("Animation")]
        [SerializeField, Range(0.001f, 0.2f)] private float noiseScale = 0.035f;
        [SerializeField, Range(0f, 2f)] private float noiseSpeed = 0.16f;
        [SerializeField, Range(0f, 8f)] private float displacement = 2.4f;
        [SerializeField, Range(0f, 8f)] private float emission = 2.6f;
        [Tooltip("Slow world-space drift of each entire curtain.")]
        [SerializeField, Range(0f, 20f)] private float spatialDrift = 3.5f;
        [SerializeField, Range(0f, 1f)] private float spatialDriftSpeed = 0.08f;
        [SerializeField, Range(0f, 10f)] private float rotationalDrift = 1.4f;

        [Header("Fog Illumination")]
        [SerializeField] private bool illuminateFog = true;
        [SerializeField, Range(1, 4)] private int lightsPerRibbon = 3;
        [SerializeField, Min(0f)] private float lightIntensity = 7f;
        [SerializeField, Min(1f)] private float lightRange = 78f;
        [Tooltip("Places the fog lights beneath the visual curtains.")]
        [SerializeField] private float lightVerticalOffset = -14f;
        [Tooltip("Irregular intensity and color variation in the volumetric glow.")]
        [SerializeField, Range(0f, 1f)] private float lightShimmerAmount = 0.38f;
        [SerializeField, Range(0f, 4f)] private float lightShimmerSpeed = 0.72f;
        [Tooltip("How far each fog light wanders beneath its curtain.")]
        [SerializeField, Range(0f, 40f)] private float lightDriftDistance = 11f;
        [SerializeField, Range(0f, 1f)] private float lightDriftSpeed = 0.11f;

        private GameObject generatedRoot;
        private readonly List<Material> generatedMaterials = new List<Material>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<GeneratedRibbon> generatedRibbons = new List<GeneratedRibbon>();
        private readonly List<GeneratedFogLight> generatedFogLights = new List<GeneratedFogLight>();
        private float areaVisibilityBlend = 1f;

#if UNITY_EDITOR
        private bool rebuildQueued;
#endif

        private void OnEnable()
        {
            areaVisibilityBlend = Application.isPlaying ? 0f : 1f;
            Rebuild();
        }

        private void OnDisable()
        {
            ReleaseGeneratedObjects();
        }

        private void OnDestroy()
        {
            ReleaseGeneratedObjects();
        }

        private void Update()
        {
            AnimateGeneratedContent(GetAnimationTime());
        }

        private void OnValidate()
        {
            horizontalSegments = Mathf.Clamp(horizontalSegments, 8, 128);
            verticalSegments = Mathf.Clamp(verticalSegments, 2, 24);
            noiseScale = Mathf.Clamp(noiseScale, 0.001f, 0.2f);
            noiseSpeed = Mathf.Clamp(noiseSpeed, 0f, 2f);
            displacement = Mathf.Clamp(displacement, 0f, 8f);
            emission = Mathf.Clamp(emission, 0f, 8f);
            spatialDrift = Mathf.Clamp(spatialDrift, 0f, 20f);
            spatialDriftSpeed = Mathf.Clamp01(spatialDriftSpeed);
            rotationalDrift = Mathf.Clamp(rotationalDrift, 0f, 10f);
            lightsPerRibbon = Mathf.Clamp(lightsPerRibbon, 1, 4);
            lightIntensity = Mathf.Max(0f, lightIntensity);
            lightRange = Mathf.Max(1f, lightRange);
            lightShimmerAmount = Mathf.Clamp01(lightShimmerAmount);
            lightShimmerSpeed = Mathf.Clamp(lightShimmerSpeed, 0f, 4f);
            lightDriftDistance = Mathf.Clamp(lightDriftDistance, 0f, 40f);
            lightDriftSpeed = Mathf.Clamp01(lightDriftSpeed);

            if (ribbons == null || ribbons.Length == 0)
            {
                ribbons = CreateDefaultProfiles();
            }

            foreach (RibbonProfile profile in ribbons)
            {
                if (profile == null)
                {
                    continue;
                }

                profile.length = Mathf.Max(10f, profile.length);
                profile.height = Mathf.Max(5f, profile.height);
                profile.irregularity = Mathf.Clamp(profile.irregularity, 0f, 20f);
                profile.opacity = Mathf.Clamp01(profile.opacity);
            }

            QueueRebuild();
        }

        [ContextMenu("Regenerate Aurora")]
        public void Rebuild()
        {
            ReleaseGeneratedObjects();
            if (!isActiveAndEnabled || ribbons == null || ribbons.Length == 0)
            {
                return;
            }

            Material shaderReference = Resources.Load<Material>("Rendering/GlacierAuroraRuntime");
            Shader shader = shaderReference != null && shaderReference.shader != null
                ? shaderReference.shader
                : Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Aurora shader '{ShaderName}' could not be found.", this);
                return;
            }

            generatedRoot = new GameObject("Aurora Generated Content")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = gameObject.layer
            };
            generatedRoot.transform.SetParent(transform, false);

            for (int index = 0; index < ribbons.Length; index++)
            {
                RibbonProfile profile = ribbons[index];
                if (profile != null)
                {
                    BuildRibbon(profile, index, shader);
                }
            }
        }

        /// <summary>
        /// Uses the same outer-to-inner glacier-area blend as the fog color.
        /// Zero hides the ribbons and disables their lights; one is full strength.
        /// </summary>
        public void SetAreaBlend(float blend)
        {
            float clampedBlend = Mathf.Clamp01(blend);
            if (Mathf.Abs(clampedBlend - areaVisibilityBlend) < 0.0001f)
            {
                return;
            }

            areaVisibilityBlend = clampedBlend;
            foreach (Material material in generatedMaterials)
            {
                if (material != null)
                {
                    material.SetFloat("_AreaBlend", areaVisibilityBlend);
                }
            }

            bool lightsVisible = !Application.isPlaying || areaVisibilityBlend > 0.001f;
            foreach (GeneratedFogLight generatedLight in generatedFogLights)
            {
                if (generatedLight.light != null)
                {
                    generatedLight.light.enabled = lightsVisible;
                }
            }
        }

        [ContextMenu("Restore Default Aurora Layout")]
        private void RestoreDefaults()
        {
            ribbons = CreateDefaultProfiles();
            Rebuild();
        }

        private void BuildRibbon(RibbonProfile profile, int index, Shader shader)
        {
            GameObject ribbonObject = new GameObject(string.IsNullOrWhiteSpace(profile.name)
                ? $"Aurora Ribbon {index + 1}"
                : profile.name)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = gameObject.layer
            };
            ribbonObject.transform.SetParent(generatedRoot.transform, false);
            ribbonObject.transform.localPosition = profile.localPosition;
            ribbonObject.transform.localRotation = Quaternion.Euler(profile.localEulerAngles);
            generatedRibbons.Add(new GeneratedRibbon
            {
                transform = ribbonObject.transform,
                baseLocalPosition = profile.localPosition,
                baseLocalRotation = ribbonObject.transform.localRotation,
                phase = profile.seed * 1.731f + index * 2.417f
            });

            Mesh mesh = BuildRibbonMesh(profile, index);
            generatedMeshes.Add(mesh);
            MeshFilter filter = ribbonObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            Material material = new Material(shader)
            {
                name = $"{profile.name} Material (Generated)",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetColor("_LowerColor", profile.lowerColor);
            material.SetColor("_UpperColor", profile.upperColor);
            material.SetFloat("_Opacity", profile.opacity);
            material.SetFloat("_NoiseScale", noiseScale);
            material.SetFloat("_NoiseSpeed", noiseSpeed);
            material.SetFloat("_Displacement", displacement);
            material.SetFloat("_Emission", emission);
            material.SetFloat("_Seed", profile.seed);
            material.SetFloat("_AreaBlend", areaVisibilityBlend);
            generatedMaterials.Add(material);

            MeshRenderer renderer = ribbonObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            if (illuminateFog)
            {
                BuildFogLights(ribbonObject.transform, profile);
            }
        }

        private Mesh BuildRibbonMesh(RibbonProfile profile, int profileIndex)
        {
            int columns = horizontalSegments + 1;
            int rows = verticalSegments + 1;
            Vector3[] vertices = new Vector3[columns * rows];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[horizontalSegments * verticalSegments * 6];

            for (int x = 0; x < columns; x++)
            {
                float u = x / (float)horizontalSegments;
                float centered = u - 0.5f;
                float horizontalPosition = centered * profile.length;
                float curvedDepth = Mathf.Sin(centered * Mathf.PI) * profile.curvature;
                float irregularDepth = Mathf.Sin(
                    u * Mathf.PI * 2.7f + profile.seed * 1.91f) * profile.irregularity;
                float irregularHeight = Mathf.Sin(
                    u * Mathf.PI * 2.1f + profile.seed * 0.73f) * profile.irregularity * 0.34f;

                for (int y = 0; y < rows; y++)
                {
                    float v = y / (float)verticalSegments;
                    int vertexIndex = y * columns + x;
                    vertices[vertexIndex] = new Vector3(
                        horizontalPosition,
                        (v - 0.5f) * profile.height + irregularHeight,
                        curvedDepth + irregularDepth * Mathf.Lerp(0.55f, 1f, v));
                    uvs[vertexIndex] = new Vector2(u, v);
                }
            }

            int triangleIndex = 0;
            for (int y = 0; y < verticalSegments; y++)
            {
                for (int x = 0; x < horizontalSegments; x++)
                {
                    int bottomLeft = y * columns + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + columns;
                    int topRight = topLeft + 1;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = bottomRight;
                }
            }

            Mesh mesh = new Mesh
            {
                name = $"Aurora Ribbon {profileIndex + 1} (Generated)",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(displacement * 2f + 2f);
            mesh.bounds = bounds;
            return mesh;
        }

        private void BuildFogLights(Transform ribbonTransform, RibbonProfile profile)
        {
            for (int lightIndex = 0; lightIndex < lightsPerRibbon; lightIndex++)
            {
                float t = (lightIndex + 1f) / (lightsPerRibbon + 1f);
                float centered = t - 0.5f;
                GameObject lightObject = new GameObject($"Aurora Fog Light {lightIndex + 1}")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = gameObject.layer
                };
                lightObject.transform.SetParent(ribbonTransform, false);
                lightObject.transform.localPosition = new Vector3(
                    centered * profile.length,
                    lightVerticalOffset,
                    Mathf.Sin(centered * Mathf.PI) * profile.curvature);

                Light auroraLight = lightObject.AddComponent<Light>();
                auroraLight.type = LightType.Point;
                auroraLight.color = Color.Lerp(profile.lowerColor, profile.upperColor, 0.62f);
                auroraLight.intensity = lightIntensity * Mathf.Lerp(0.82f, 1f, 1f - Mathf.Abs(centered) * 2f);
                auroraLight.range = lightRange;
                auroraLight.shadows = LightShadows.None;
                auroraLight.renderMode = LightRenderMode.Auto;
                auroraLight.cullingMask = ~0;
                auroraLight.enabled = !Application.isPlaying || areaVisibilityBlend > 0.001f;

                generatedFogLights.Add(new GeneratedFogLight
                {
                    transform = lightObject.transform,
                    light = auroraLight,
                    baseLocalPosition = lightObject.transform.localPosition,
                    baseIntensity = auroraLight.intensity,
                    lowerColor = profile.lowerColor,
                    upperColor = profile.upperColor,
                    phase = profile.seed * 2.193f + lightIndex * 1.977f
                });
            }
        }

        private void AnimateGeneratedContent(float time)
        {
            float curtainTime = time * spatialDriftSpeed * Mathf.PI * 2f;
            foreach (GeneratedRibbon ribbon in generatedRibbons)
            {
                if (ribbon.transform == null)
                {
                    continue;
                }

                float primary = Mathf.Sin(curtainTime + ribbon.phase);
                float secondary = Mathf.Sin(curtainTime * 0.63f + ribbon.phase * 1.73f);
                float depth = Mathf.Cos(curtainTime * 0.81f + ribbon.phase * 0.71f);
                ribbon.transform.localPosition = ribbon.baseLocalPosition + new Vector3(
                    primary * spatialDrift,
                    secondary * spatialDrift * 0.26f,
                    depth * spatialDrift * 0.48f);
                ribbon.transform.localRotation = ribbon.baseLocalRotation * Quaternion.Euler(
                    secondary * rotationalDrift,
                    depth * rotationalDrift * 0.55f,
                    primary * rotationalDrift * 0.72f);
            }

            float shimmerTime = time * lightShimmerSpeed * Mathf.PI * 2f;
            float driftTime = time * lightDriftSpeed * Mathf.PI * 2f;
            foreach (GeneratedFogLight generatedLight in generatedFogLights)
            {
                if (generatedLight.transform == null || generatedLight.light == null)
                {
                    continue;
                }

                float broadShimmer = Mathf.Sin(shimmerTime + generatedLight.phase);
                float fineShimmer = Mathf.Sin(shimmerTime * 2.37f + generatedLight.phase * 1.31f);
                float shimmer = broadShimmer * 0.68f + fineShimmer * 0.32f;
                generatedLight.light.intensity = generatedLight.baseIntensity
                    * Mathf.Max(0.12f, 1f + shimmer * lightShimmerAmount)
                    * areaVisibilityBlend;

                float colorShift = 0.5f + broadShimmer * 0.18f;
                generatedLight.light.color = Color.Lerp(
                    generatedLight.lowerColor,
                    generatedLight.upperColor,
                    colorShift);

                float horizontalDrift = Mathf.Sin(driftTime + generatedLight.phase);
                float verticalDrift = Mathf.Sin(driftTime * 0.57f + generatedLight.phase * 1.49f);
                float depthDrift = Mathf.Cos(driftTime * 0.83f + generatedLight.phase * 0.77f);
                generatedLight.transform.localPosition = generatedLight.baseLocalPosition + new Vector3(
                    horizontalDrift * lightDriftDistance,
                    verticalDrift * lightDriftDistance * 0.18f,
                    depthDrift * lightDriftDistance * 0.36f);
            }
        }

        private static float GetAnimationTime()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return (float)EditorApplication.timeSinceStartup;
            }
#endif
            return Time.time;
        }

        private void ReleaseGeneratedObjects()
        {
            DestroyGenerated(generatedRoot);
            generatedRoot = null;

            foreach (Material material in generatedMaterials)
            {
                DestroyGenerated(material);
            }
            generatedMaterials.Clear();

            foreach (Mesh mesh in generatedMeshes)
            {
                DestroyGenerated(mesh);
            }
            generatedMeshes.Clear();
            generatedRibbons.Clear();
            generatedFogLights.Clear();
        }

        private static void DestroyGenerated(UnityEngine.Object target)
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

        private void QueueRebuild()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (rebuildQueued)
                {
                    return;
                }

                rebuildQueued = true;
                EditorApplication.delayCall += () =>
                {
                    rebuildQueued = false;
                    if (this != null && isActiveAndEnabled)
                    {
                        Rebuild();
                    }
                };
                return;
            }
#endif
            Rebuild();
        }

        private static RibbonProfile[] CreateDefaultProfiles()
        {
            return new[]
            {
                new RibbonProfile
                {
                    name = "Aurora Curtain A",
                    localPosition = new Vector3(18f, 76f, 58f),
                    localEulerAngles = new Vector3(0f, -16f, -3f),
                    length = 182f,
                    height = 54f,
                    curvature = 31f,
                    irregularity = 5.5f,
                    seed = 1.37f,
                    lowerColor = new Color(0.055f, 0.31f, 0.48f, 1f),
                    upperColor = new Color(0.3f, 0.84f, 0.75f, 1f),
                    opacity = 0.48f
                },
                new RibbonProfile
                {
                    name = "Aurora Curtain B",
                    localPosition = new Vector3(-32f, 92f, 102f),
                    localEulerAngles = new Vector3(2f, 21f, 4f),
                    length = 148f,
                    height = 43f,
                    curvature = -24f,
                    irregularity = 4.2f,
                    seed = 4.81f,
                    lowerColor = new Color(0.09f, 0.28f, 0.54f, 1f),
                    upperColor = new Color(0.32f, 0.67f, 0.88f, 1f),
                    opacity = 0.4f
                },
                new RibbonProfile
                {
                    name = "Aurora Curtain C",
                    localPosition = new Vector3(44f, 108f, 138f),
                    localEulerAngles = new Vector3(-2f, -31f, 2f),
                    length = 124f,
                    height = 36f,
                    curvature = 19f,
                    irregularity = 3.5f,
                    seed = 8.26f,
                    lowerColor = new Color(0.12f, 0.34f, 0.47f, 1f),
                    upperColor = new Color(0.46f, 0.76f, 0.82f, 1f),
                    opacity = 0.34f
                },
                new RibbonProfile
                {
                    name = "Aurora Curtain Left",
                    localPosition = new Vector3(-160f, 82f, 78f),
                    localEulerAngles = new Vector3(1f, 12f, -5f),
                    length = 116f,
                    height = 45f,
                    curvature = 21f,
                    irregularity = 4.8f,
                    seed = 11.42f,
                    lowerColor = new Color(0.055f, 0.3f, 0.45f, 1f),
                    upperColor = new Color(0.27f, 0.88f, 0.7f, 1f),
                    opacity = 0.38f
                },
                new RibbonProfile
                {
                    name = "Aurora Curtain Right",
                    localPosition = new Vector3(165f, 94f, 112f),
                    localEulerAngles = new Vector3(-1f, -18f, 5f),
                    length = 108f,
                    height = 40f,
                    curvature = -18f,
                    irregularity = 4.1f,
                    seed = 15.73f,
                    lowerColor = new Color(0.085f, 0.27f, 0.52f, 1f),
                    upperColor = new Color(0.4f, 0.72f, 0.9f, 1f),
                    opacity = 0.35f
                }
            };
        }
    }
}
