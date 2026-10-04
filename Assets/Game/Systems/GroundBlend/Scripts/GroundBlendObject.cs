using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnEchoHasNoShape.GroundBlend
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GroundBlendObject : MonoBehaviour
    {
        private const string OverlayShaderName = "An Echo Has No Shape/Ground Blend Overlay";

        private sealed class BlendProxy
        {
            public Renderer Source;
            public Renderer Overlay;
            public GameObject GameObject;
        }

        [Tooltip("Renderers receiving the blend. Empty means every renderer below this object.")]
        [SerializeField] private Renderer[] targetRenderers;

        [Header("Blend Shape")]
        [Min(0f)] [SerializeField] private float blendHeight = 0.8f;
        [Min(0.001f)] [SerializeField] private float blendFalloff = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float blendStrength = 1f;
        [Min(0.001f)] [SerializeField] private float noiseScale = 1.5f;
        [Range(0f, 1f)] [SerializeField] private float noiseStrength = 0.2f;
        [Range(0f, 1f)] [SerializeField] private float vertexColorInfluence;
        [Tooltip("Maximum opacity of the terrain overlay. Lower values preserve more of the original material.")]
        [Range(0f, 1f)] [SerializeField] private float maximumOpacity = 0.55f;
        [Tooltip("Higher values sample a softer terrain mip and avoid stamping terrain detail onto vertical faces.")]
        [Range(0f, 6f)] [SerializeField] private float sampleMip = 2f;
        [SerializeField] private Color groundTint = Color.white;

        [Header("Placement")]
        [Tooltip("Added to the renderer bounds bottom. Use this if the mesh bounds extend below the visible contact point.")]
        [SerializeField] private float bottomOffset;
        [Tooltip("Refresh bounds every frame for moving props. Leave off for static scenery.")]
        [SerializeField] private bool followMovingObject;

        private static readonly int BottomYId = Shader.PropertyToID("_GroundBlendBottomY");
        private static readonly int HeightId = Shader.PropertyToID("_GroundBlendHeight");
        private static readonly int FalloffId = Shader.PropertyToID("_GroundBlendFalloff");
        private static readonly int StrengthId = Shader.PropertyToID("_GroundBlendStrength");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_GroundBlendNoiseScale");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_GroundBlendNoiseStrength");
        private static readonly int VertexColorId = Shader.PropertyToID("_VertexColorInfluence");
        private static readonly int MaximumOpacityId = Shader.PropertyToID("_GroundBlendMaximumOpacity");
        private static readonly int SampleMipId = Shader.PropertyToID("_GroundSampleMip");
        private static readonly int GroundTintId = Shader.PropertyToID("_GroundTint");

        private readonly List<BlendProxy> proxies = new List<BlendProxy>();
        private Material overlayMaterial;
        private MaterialPropertyBlock propertyBlock;

        private void OnEnable()
        {
            CacheRenderersIfNeeded();
            EnsureOverlayMaterial();
            EnsureProxies();
            Apply();
        }

        private void LateUpdate()
        {
            RefreshProxyVisibility();
            if (followMovingObject) Apply();
        }

        private void OnValidate()
        {
            blendHeight = Mathf.Max(0f, blendHeight);
            blendFalloff = Mathf.Max(0.001f, blendFalloff);
            noiseScale = Mathf.Max(0.001f, noiseScale);
            // Unity does not allow creating the proxy GameObjects from OnValidate.
            // Existing proxies can still receive live Inspector value changes.
            if (proxies.Count > 0) ApplyProxyProperties();
        }

        [ContextMenu("Refresh Ground Blend")]
        public void Apply()
        {
            CacheRenderersIfNeeded();
            EnsureOverlayMaterial();
            EnsureProxies();
            ApplyProxyProperties();
        }

        private void ApplyProxyProperties()
        {
            propertyBlock ??= new MaterialPropertyBlock();

            foreach (BlendProxy proxy in proxies)
            {
                if (proxy.Source == null || proxy.Overlay == null) continue;
                proxy.Overlay.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(BottomYId, proxy.Source.bounds.min.y + bottomOffset);
                propertyBlock.SetFloat(HeightId, blendHeight);
                propertyBlock.SetFloat(FalloffId, blendFalloff);
                // Treat a zero-height blend as explicitly disabled. Previously the
                // falloff alone could still cover the object, which was surprising.
                propertyBlock.SetFloat(StrengthId, blendHeight > 0.0001f ? blendStrength : 0f);
                propertyBlock.SetFloat(NoiseScaleId, noiseScale);
                propertyBlock.SetFloat(NoiseStrengthId, noiseStrength);
                propertyBlock.SetFloat(VertexColorId, vertexColorInfluence);
                propertyBlock.SetFloat(MaximumOpacityId, maximumOpacity);
                propertyBlock.SetFloat(SampleMipId, sampleMip);
                propertyBlock.SetColor(GroundTintId, groundTint);
                proxy.Overlay.SetPropertyBlock(propertyBlock);
            }

            RefreshProxyVisibility();
        }

        private void EnsureOverlayMaterial()
        {
            if (overlayMaterial != null) return;
            Shader shader = Shader.Find(OverlayShaderName);
            if (shader == null) return;
            overlayMaterial = new Material(shader)
            {
                name = "Ground Blend Overlay (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void EnsureProxies()
        {
            if (overlayMaterial == null || proxies.Count > 0) return;

            foreach (Renderer source in targetRenderers)
            {
                if (source == null || (source.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;

                if (source is MeshRenderer meshRenderer)
                {
                    MeshFilter sourceFilter = meshRenderer.GetComponent<MeshFilter>();
                    if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;
                    GameObject proxyObject = CreateProxyObject(source.transform);
                    MeshFilter proxyFilter = proxyObject.AddComponent<MeshFilter>();
                    MeshRenderer proxyRenderer = proxyObject.AddComponent<MeshRenderer>();
                    proxyFilter.sharedMesh = sourceFilter.sharedMesh;
                    ConfigureProxyRenderer(source, proxyRenderer, sourceFilter.sharedMesh.subMeshCount);
                    proxies.Add(new BlendProxy { Source = source, Overlay = proxyRenderer, GameObject = proxyObject });
                }
                else if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                {
                    GameObject proxyObject = CreateProxyObject(source.transform);
                    SkinnedMeshRenderer proxyRenderer = proxyObject.AddComponent<SkinnedMeshRenderer>();
                    proxyRenderer.sharedMesh = skinned.sharedMesh;
                    proxyRenderer.rootBone = skinned.rootBone;
                    proxyRenderer.bones = skinned.bones;
                    proxyRenderer.localBounds = skinned.localBounds;
                    proxyRenderer.quality = skinned.quality;
                    proxyRenderer.updateWhenOffscreen = skinned.updateWhenOffscreen;
                    ConfigureProxyRenderer(source, proxyRenderer, skinned.sharedMesh.subMeshCount);
                    proxies.Add(new BlendProxy { Source = source, Overlay = proxyRenderer, GameObject = proxyObject });
                }
            }
        }

        private static GameObject CreateProxyObject(Transform sourceTransform)
        {
            GameObject proxyObject = new GameObject("Ground Blend Overlay")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = sourceTransform.gameObject.layer
            };
            proxyObject.transform.SetParent(sourceTransform, false);
            return proxyObject;
        }

        private void ConfigureProxyRenderer(Renderer source, Renderer overlay, int subMeshCount)
        {
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlay.allowOcclusionWhenDynamic = false;
            overlay.renderingLayerMask = source.renderingLayerMask;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
            Material[] materials = new Material[Mathf.Max(1, subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = overlayMaterial;
            overlay.sharedMaterials = materials;
        }

        private void RefreshProxyVisibility()
        {
            foreach (BlendProxy proxy in proxies)
            {
                if (proxy.Overlay != null)
                    proxy.Overlay.enabled = isActiveAndEnabled && proxy.Source != null && proxy.Source.enabled;
            }
        }

        private void CacheRenderersIfNeeded()
        {
            if (targetRenderers != null && targetRenderers.Length > 0) return;
            List<Renderer> sources = new List<Renderer>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if ((renderer.gameObject.hideFlags & HideFlags.DontSave) == 0) sources.Add(renderer);
            }
            targetRenderers = sources.ToArray();
        }

        private void OnDisable() => RefreshProxyVisibility();

        private void OnDestroy()
        {
            foreach (BlendProxy proxy in proxies) DestroyOwnedObject(proxy.GameObject);
            proxies.Clear();
            DestroyOwnedObject(overlayMaterial);
        }

        private static void DestroyOwnedObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
