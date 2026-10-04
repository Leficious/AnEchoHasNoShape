using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EchoReactiveSurface : MonoBehaviour
    {
        private const string EchoShaderName = "An Echo Has No Shape/Echo Reveal Overlay";
        private static readonly int UseObjectEchoColorId = Shader.PropertyToID("_UseObjectEchoColor");
        private static readonly int ObjectEchoColorId = Shader.PropertyToID("_ObjectEchoColor");
        private static readonly int UseRevealDurationOverrideId = Shader.PropertyToID("_UseRevealDurationOverride");
        private static readonly int TimedRevealAmountId = Shader.PropertyToID("_TimedRevealAmount");
        private static readonly int TimedRevealShimmerStrengthId = Shader.PropertyToID("_TimedRevealShimmerStrength");
        private static readonly int TimedRevealShimmerSpeedId = Shader.PropertyToID("_TimedRevealShimmerSpeed");
        private static readonly int PlayerEchoMaskId = Shader.PropertyToID("_PlayerEchoMask");
        private static readonly int SirenEchoMaskId = Shader.PropertyToID("_SirenEchoMask");
        private static readonly int SirenSubtractionMaskId = Shader.PropertyToID("_SirenSubtractionMask");
        private static readonly int AlphaTextureId = Shader.PropertyToID("_EchoAlphaTexture");
        private static readonly int UseAlphaClipId = Shader.PropertyToID("_EchoUseAlphaClip");
        private static readonly int AlphaCutoffId = Shader.PropertyToID("_EchoAlphaCutoff");
        private static readonly int ObjectWireframeMultiplierId = Shader.PropertyToID("_ObjectWireframeMultiplier");
        private static readonly int EchoOpacityId = Shader.PropertyToID("_EchoOpacity");

        [SerializeField, Min(0f)] private float surfaceOffset = 0.006f;
        [SerializeField, Min(0f)] private float outlineWidth = 0.025f;
        [SerializeField, Min(0f)] private float revealMultiplier = 1f;
        [SerializeField, Range(0f, 2f)] private float diffusionStrength = 1f;

        private GameObject ringOverlayObject;
        private GameObject outlineOverlayObject;
        private Material[] ringMaterials;
        private Material[] outlineMaterials;
        private MeshRenderer sourceRenderer;
        private float revealedDuration = -1f;
        private float fadeOutDuration;
        private float shimmerStrength;
        private float shimmerSpeed;
        private float revealHoldUntilTime;
        private float revealFadeUntilTime;
        private int lastTriggeredPulseSequence = -1;
        private float timedRevealAmount;
        private EchoRevealState sharedRevealState;
        private float sirenEchoMask = 1f;
        private float sirenSubtractionMask = 1f;

        private void Awake()
        {
            Material shaderReference = Resources.Load<Material>("Rendering/EchoRevealRuntime");
            Shader echoShader = shaderReference != null && shaderReference.shader != null
                ? shaderReference.shader
                : Shader.Find(EchoShaderName);
            if (echoShader == null)
            {
                Debug.LogError($"Echo shader '{EchoShaderName}' could not be found.", this);
                enabled = false;
                return;
            }

            MeshFilter sourceFilter = GetComponent<MeshFilter>();
            sourceRenderer = GetComponent<MeshRenderer>();
            int materialCount = Mathf.Max(1, sourceRenderer.sharedMaterials.Length);

            Material[] sourceMaterials = sourceRenderer.sharedMaterials;
            EchoSurfaceProfile profile = GetComponentInParent<EchoSurfaceProfile>(true);
            ringMaterials = CreateMaterials(echoShader, sourceMaterials, materialCount, false);
            outlineMaterials = CreateMaterials(echoShader, sourceMaterials, materialCount, true);
            ApplyProfile(ringMaterials, profile, false);
            ApplyProfile(outlineMaterials, profile, true);
            ringOverlayObject = CreateOverlay("Echo Moving Rings", sourceFilter.sharedMesh, ringMaterials);
            outlineOverlayObject = CreateOverlay("Echo Accumulated Outline", sourceFilter.sharedMesh, outlineMaterials);
            RegisterOverlaysWithLodGroup();
            RefreshEchoColorOverride();
            RefreshEchoIgnoreState();
        }

        private void Update()
        {
            if (sharedRevealState != null && sharedRevealState.isActiveAndEnabled)
            {
                float sharedAmount = sharedRevealState.RevealAmount;
                if (!Mathf.Approximately(sharedAmount, timedRevealAmount))
                {
                    timedRevealAmount = sharedAmount;
                    ApplyTimedRevealState();
                }

                return;
            }

            if (revealedDuration < 0f || sourceRenderer == null)
            {
                return;
            }

            float nextRevealAmount = 0f;
            if (Time.time < revealHoldUntilTime)
            {
                nextRevealAmount = 1f;
            }
            else if (fadeOutDuration > 0f && Time.time < revealFadeUntilTime)
            {
                float fadeProgress = Mathf.InverseLerp(revealHoldUntilTime, revealFadeUntilTime, Time.time);
                nextRevealAmount = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
            }

            if (!Mathf.Approximately(nextRevealAmount, timedRevealAmount))
            {
                timedRevealAmount = nextRevealAmount;
                ApplyTimedRevealState();
            }

            if (!EchoPulseController.IsPulseActive ||
                lastTriggeredPulseSequence == EchoPulseController.ActivePulseSequence)
            {
                return;
            }

            Vector3 nearestPoint = sourceRenderer.bounds.ClosestPoint(EchoPulseController.ActivePulseOrigin);
            float distanceToSurface = Vector3.Distance(EchoPulseController.ActivePulseOrigin, nearestPoint);

            if (distanceToSurface <= EchoPulseController.ActivePulseRadius &&
                distanceToSurface <= EchoPulseController.ActivePulseRange)
            {
                lastTriggeredPulseSequence = EchoPulseController.ActivePulseSequence;
                revealHoldUntilTime = Time.time + revealedDuration;
                revealFadeUntilTime = revealedDuration > 0f
                    ? revealHoldUntilTime + fadeOutDuration
                    : revealHoldUntilTime;
                timedRevealAmount = revealedDuration > 0f ? 1f : 0f;
                ApplyTimedRevealState();
            }
        }

        public void RefreshEchoIgnoreState()
        {
            float playerEchoMask = IgnoreEcholocation.IsIgnored(transform) ? 0f : 1f;
            ApplyFloat(ringMaterials, PlayerEchoMaskId, playerEchoMask);
            ApplyFloat(outlineMaterials, PlayerEchoMaskId, playerEchoMask);
        }

        /// <summary>
        /// Controls only the deceptive siren echo. The player's echo eligibility
        /// remains governed by IgnoreEcholocation.
        /// </summary>
        public void SetSirenEchoBehavior(bool shouldReveal, bool shouldSubtractPlayerEcho)
        {
            sirenEchoMask = shouldReveal ? 1f : 0f;
            sirenSubtractionMask = shouldSubtractPlayerEcho ? 1f : 0f;
            ApplyFloat(ringMaterials, SirenEchoMaskId, sirenEchoMask);
            ApplyFloat(outlineMaterials, SirenEchoMaskId, sirenEchoMask);
            ApplyFloat(ringMaterials, SirenSubtractionMaskId, sirenSubtractionMask);
            ApplyFloat(outlineMaterials, SirenSubtractionMaskId, sirenSubtractionMask);
        }

        public void RefreshEchoColorOverride()
        {
            EchoColorOverride colorOverride = GetComponentInParent<EchoColorOverride>(true);
            sharedRevealState = GetComponentInParent<EchoRevealState>(true);
            bool useOverride = colorOverride != null && colorOverride.isActiveAndEnabled;
            bool useSharedReveal = sharedRevealState != null && sharedRevealState.isActiveAndEnabled;
            Color color = useOverride ? colorOverride.EchoColor : Color.white;
            revealedDuration = useSharedReveal
                ? 0f
                : (useOverride ? Mathf.Max(0f, colorOverride.RevealedDuration) : -1f);
            fadeOutDuration = useOverride ? Mathf.Max(0f, colorOverride.FadeOutDuration) : 0f;
            shimmerStrength = useOverride
                ? Mathf.Clamp(colorOverride.ShimmerStrength, 0f, 0.5f)
                : (useSharedReveal ? 0.1f : 0f);
            shimmerSpeed = useOverride ? Mathf.Max(0.1f, colorOverride.ShimmerSpeed) : 2f;
            revealHoldUntilTime = 0f;
            revealFadeUntilTime = 0f;
            timedRevealAmount = useSharedReveal ? sharedRevealState.RevealAmount : 0f;
            lastTriggeredPulseSequence = -1;

            ApplyEchoColor(ringMaterials, useOverride, color);
            ApplyEchoColor(outlineMaterials, useOverride, color);
            ApplyTimedRevealState();
        }

        private void ApplyTimedRevealState()
        {
            ApplyTimedRevealStateTo(outlineMaterials);
            // Architecture fill and its depth mask share the outline lifetime.
            ApplyTimedRevealStateTo(ringMaterials);
        }

        public void SetCityReveal(float amount, Color color)
        {
            ApplyFloat(ringMaterials, Shader.PropertyToID("_CityRevealAmount"), amount);
            ApplyFloat(outlineMaterials, Shader.PropertyToID("_CityRevealAmount"), amount);
            SetCityColor(ringMaterials, color);
            SetCityColor(outlineMaterials, color);
        }

        public void SetCityPerspective(int perspective)
        {
            int id = Shader.PropertyToID("_CityPerspective");
            ApplyFloat(ringMaterials, id, perspective);
            ApplyFloat(outlineMaterials, id, perspective);
        }

        public void SetCityCharacterColor(Color color, bool allowCompletionGold = true)
        {
            float colorLock = allowCompletionGold ? 1f : 2f;
            ApplyFloat(ringMaterials, Shader.PropertyToID("_LockCityCharacterColor"), colorLock);
            ApplyFloat(outlineMaterials, Shader.PropertyToID("_LockCityCharacterColor"), colorLock);
            SetCityColor(ringMaterials, color);
            SetCityColor(outlineMaterials, color);
        }

        private static void SetCityColor(Material[] materials, Color color)
        {
            if (materials == null) return;
            foreach (Material material in materials)
                if (material != null) material.SetColor("_CityRevealColor", color);
        }

        private void ApplyTimedRevealStateTo(Material[] materials)
        {
            if (materials == null)
            {
                return;
            }

            foreach (Material material in materials)
            {
                if (material != null)
                {
                    material.SetFloat(UseRevealDurationOverrideId, revealedDuration >= 0f ? 1f : 0f);
                    material.SetFloat(TimedRevealAmountId, timedRevealAmount);
                    material.SetFloat(TimedRevealShimmerStrengthId, shimmerStrength);
                    material.SetFloat(TimedRevealShimmerSpeedId, shimmerSpeed);
                }
            }
        }

        private static void ApplyEchoColor(Material[] materials, bool useOverride, Color color)
        {
            if (materials == null)
            {
                return;
            }

            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                material.SetFloat(UseObjectEchoColorId, useOverride ? 1f : 0f);
                material.SetColor(ObjectEchoColorId, color);
            }
        }

        private Material[] CreateMaterials(Shader shader, Material[] sourceMaterials, int count, bool outlineMode)
        {
            Material[] materials = new Material[count];
            for (int index = 0; index < count; index++)
            {
                Material material = new Material(shader)
                {
                    name = $"{gameObject.name} Echo {(outlineMode ? "Outline" : "Ring")} (Runtime)"
                };

                material.SetFloat("_Extrusion", outlineMode ? outlineWidth : surfaceOffset);
                material.SetFloat("_RevealMode", outlineMode ? 1f : 0f);
                material.SetFloat("_Cull", outlineMode ? (float)CullMode.Front : (float)CullMode.Back);
                Material sourceMaterial = sourceMaterials != null && index < sourceMaterials.Length
                    ? sourceMaterials[index]
                    : null;
                bool vegetation = IsVegetationMaterial(sourceMaterial);
                material.SetFloat("_RevealMultiplier", revealMultiplier * (vegetation ? 0.62f : 1f));
                if (vegetation)
                {
                    // Leaf cards should read as a soft translucent mass instead
                    // of reaching the same solid white as terrain and stone.
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat(EchoOpacityId, 0.32f);
                }
                material.SetFloat("_DiffusionStrength", diffusionStrength);
                material.SetFloat(PlayerEchoMaskId, 1f);
                material.SetFloat(SirenEchoMaskId, sirenEchoMask);
                material.SetFloat(SirenSubtractionMaskId, sirenSubtractionMask);
                ConfigureSourceAlpha(material, sourceMaterial, vegetation);
                materials[index] = material;
            }

            return materials;
        }

        private static void ConfigureSourceAlpha(Material echoMaterial, Material sourceMaterial, bool vegetation)
        {
            if (sourceMaterial == null)
            {
                echoMaterial.SetFloat(UseAlphaClipId, 0f);
                return;
            }

            Texture alphaTexture = null;
            string[] textureProperties = { "_BaseTexture", "_BASETEXTURE", "_BaseMap", "_MainTex" };
            foreach (string property in textureProperties)
            {
                if (sourceMaterial.HasProperty(property) && sourceMaterial.GetTexture(property) != null)
                {
                    alphaTexture = sourceMaterial.GetTexture(property);
                    break;
                }
            }

            bool alphaClipped = vegetation ||
                (sourceMaterial.HasProperty("_AlphaClip") && sourceMaterial.GetFloat("_AlphaClip") > 0.5f) ||
                (sourceMaterial.HasProperty("_AlphaCutoffEnable") && sourceMaterial.GetFloat("_AlphaCutoffEnable") > 0.5f);
            float cutoff = sourceMaterial.HasProperty("_AlphaCutoff")
                ? sourceMaterial.GetFloat("_AlphaCutoff")
                : (sourceMaterial.HasProperty("_Cutoff") ? sourceMaterial.GetFloat("_Cutoff") : 0.45f);

            if (alphaTexture != null) echoMaterial.SetTexture(AlphaTextureId, alphaTexture);
            echoMaterial.SetFloat(UseAlphaClipId, alphaTexture != null && alphaClipped ? 1f : 0f);
            echoMaterial.SetFloat(AlphaCutoffId, Mathf.Clamp01(cutoff));
        }

        private static bool IsVegetationMaterial(Material material)
        {
            if (material == null) return false;
            string description = $"{material.name} {(material.shader != null ? material.shader.name : string.Empty)}".ToLowerInvariant();
            bool namedCard = description.Contains("foliage") || description.Contains("leaf") ||
                             description.Contains("leaves") || description.Contains("grass") ||
                             description.Contains("plant") || description.Contains("flower");
            bool transparentVegetation = description.Contains("vegetation") && material.renderQueue >= 2450;
            return namedCard || transparentVegetation;
        }

        public void RefreshSurfaceProfile()
        {
            EchoSurfaceProfile profile = GetComponentInParent<EchoSurfaceProfile>(true);
            ApplyProfile(ringMaterials, profile, false);
            ApplyProfile(outlineMaterials, profile, true);
        }

        private void ApplyProfile(Material[] materials, EchoSurfaceProfile profile, bool outlineMode)
        {
            if (materials == null) return;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null) continue;
                bool architecture = profile != null && profile.SoftArchitecture;
                material.SetFloat("_SoftArchitecture", architecture ? 1f : 0f);
                material.SetShaderPassEnabled("EchoArchitectureDepth", architecture && !outlineMode);
                if (architecture)
                {
                    material.SetColor("_ArchitectureTint", profile.FillTint);
                    material.SetFloat("_ArchitectureBrightness", outlineMode ? profile.OutlineBrightness : profile.FillBrightness);
                    material.SetFloat("_ArchitectureShading", outlineMode ? 0f : profile.DirectionalShading);
                    Bounds bounds = sourceRenderer.bounds;
                    material.SetVector("_ArchitectureGroundFade", new Vector4(
                        bounds.min.y, Mathf.Max(0.001f, bounds.size.y * profile.GroundFadeHeightFraction),
                        profile.GroundFadeHeightFraction > 0f ? 1f : 0f, 0f));
                }
                Material source = sourceRenderer != null && index < sourceRenderer.sharedMaterials.Length
                    ? sourceRenderer.sharedMaterials[index] : null;
                bool vegetation = IsVegetationMaterial(source);
                float profileReveal = profile != null ? profile.RevealMultiplier : 1f;
                float profileWire = profile != null ? profile.WireframeMultiplier : 1f;
                float profileOutline = profile != null ? profile.OutlineWidthMultiplier : 1f;
                material.SetFloat("_RevealMultiplier", revealMultiplier * profileReveal * (vegetation ? 0.62f : 1f));
                material.SetFloat(ObjectWireframeMultiplierId, profileWire * (vegetation ? 0.45f : 1f));
                if (outlineMode)
                {
                    material.SetFloat("_Extrusion", outlineWidth * profileOutline * (vegetation ? 0.45f : 1f));
                }
            }
        }

        private GameObject CreateOverlay(string objectName, Mesh mesh, Material[] materials)
        {
            GameObject overlay = new GameObject(objectName)
            {
                layer = gameObject.layer,
                hideFlags = HideFlags.DontSave
            };

            overlay.transform.SetParent(transform, false);

            MeshFilter overlayFilter = overlay.AddComponent<MeshFilter>();
            overlayFilter.sharedMesh = mesh;

            MeshRenderer overlayRenderer = overlay.AddComponent<MeshRenderer>();
            overlayRenderer.sharedMaterials = materials;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return overlay;
        }

        private void RegisterOverlaysWithLodGroup()
        {
            LODGroup lodGroup = GetComponentInParent<LODGroup>();
            if (lodGroup == null || sourceRenderer == null) return;

            Renderer ringRenderer = ringOverlayObject != null ? ringOverlayObject.GetComponent<Renderer>() : null;
            Renderer outlineRenderer = outlineOverlayObject != null ? outlineOverlayObject.GetComponent<Renderer>() : null;
            LOD[] lods = lodGroup.GetLODs();
            bool changed = false;

            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] current = lods[lodIndex].renderers;
                bool ownsSource = System.Array.IndexOf(current, sourceRenderer) >= 0;
                if (!ownsSource) continue;

                List<Renderer> renderers = new List<Renderer>(current);
                if (ringRenderer != null && !renderers.Contains(ringRenderer)) renderers.Add(ringRenderer);
                if (outlineRenderer != null && !renderers.Contains(outlineRenderer)) renderers.Add(outlineRenderer);
                lods[lodIndex].renderers = renderers.ToArray();
                changed = true;
                break;
            }

            if (changed)
            {
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
        }

        private void OnDestroy()
        {
            if (ringOverlayObject != null)
            {
                Destroy(ringOverlayObject);
            }

            if (outlineOverlayObject != null)
            {
                Destroy(outlineOverlayObject);
            }

            DestroyMaterials(ringMaterials);
            DestroyMaterials(outlineMaterials);
        }

        private static void DestroyMaterials(Material[] materials)
        {
            if (materials == null)
            {
                return;
            }

            foreach (Material material in materials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
        }

        private static void ApplyFloat(Material[] materials, int propertyId, float value)
        {
            if (materials == null)
            {
                return;
            }

            foreach (Material material in materials)
            {
                if (material != null)
                {
                    material.SetFloat(propertyId, value);
                }
            }
        }

        private void OnValidate()
        {
            surfaceOffset = Mathf.Max(0f, surfaceOffset);
            outlineWidth = Mathf.Max(0f, outlineWidth);
            revealMultiplier = Mathf.Max(0f, revealMultiplier);
            diffusionStrength = Mathf.Clamp(diffusionStrength, 0f, 2f);
        }
    }
}
