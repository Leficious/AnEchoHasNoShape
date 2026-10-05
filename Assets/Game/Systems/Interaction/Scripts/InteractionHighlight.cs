using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AnEchoHasNoShape.Echolocation;

namespace AnEchoHasNoShape.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionHighlight : MonoBehaviour
    {
        private sealed class HighlightProxy
        {
            public Renderer Source;
            public Renderer Highlight;
            public GameObject GameObject;
        }

        private readonly List<HighlightProxy> proxies = new List<HighlightProxy>();
        private Material highlightMaterial;
        private Shader currentShader;
        private bool isVisible;
        private float visibility;
        private float targetVisibility;
        private float fadeDuration = 0.18f;
        private CityCharacterInteractable echoCharacter;
        private float integratedStrength;

        public void Show(
            Shader shader,
            Color color,
            float strength,
            float shimmerSpeed,
            float duration)
        {
            echoCharacter = GetComponent<CityCharacterInteractable>();
            if (echoCharacter != null && echoCharacter.HasIntegratedHighlight)
            {
                // Echo characters render after fog; a separate transparent proxy
                // would be composited at a different stage and wash out their color.
                integratedStrength = Mathf.Clamp01(strength);
                fadeDuration = Mathf.Max(0.01f, duration);
                isVisible = true;
                targetVisibility = 1f;
                return;
            }
            if (shader == null)
            {
                return;
            }

            EnsureMaterial(shader);
            EnsureProxies();
            highlightMaterial.SetColor("_HighlightColor", color);
            highlightMaterial.SetFloat("_HighlightStrength", Mathf.Max(0f, strength));
            highlightMaterial.SetFloat("_ShimmerSpeed", Mathf.Max(0.1f, shimmerSpeed));
            fadeDuration = Mathf.Max(0.01f, duration);
            isVisible = true;
            targetVisibility = 1f;
            RefreshProxyVisibility();
        }

        public void Hide()
        {
            isVisible = false;
            targetVisibility = 0f;
            RefreshProxyVisibility();
        }

        private void LateUpdate()
        {
            if (!Mathf.Approximately(visibility, targetVisibility))
            {
                visibility = Mathf.MoveTowards(
                    visibility,
                    targetVisibility,
                    Time.unscaledDeltaTime / fadeDuration);

                if (highlightMaterial != null)
                {
                    highlightMaterial.SetFloat("_Visibility", visibility);
                }
            }

            if (echoCharacter != null && echoCharacter.HasIntegratedHighlight)
                echoCharacter.SetInteractionHighlight(visibility * integratedStrength);
            RefreshProxyVisibility();
        }

        private void EnsureMaterial(Shader shader)
        {
            if (highlightMaterial != null && currentShader == shader)
            {
                return;
            }

            if (highlightMaterial != null)
            {
                Destroy(highlightMaterial);
            }

            currentShader = shader;
            highlightMaterial = new Material(shader)
            {
                name = "Interaction Highlight (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            highlightMaterial.SetFloat("_Visibility", visibility);

            foreach (HighlightProxy proxy in proxies)
            {
                AssignMaterial(proxy.Highlight);
            }
        }

        private void EnsureProxies()
        {
            if (proxies.Count > 0)
            {
                return;
            }

            foreach (MeshRenderer source in GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null ||
                    (source.gameObject.hideFlags & HideFlags.DontSave) != 0)
                {
                    continue;
                }

                GameObject proxyObject = CreateProxyObject(source.transform);
                MeshFilter proxyFilter = proxyObject.AddComponent<MeshFilter>();
                MeshRenderer proxyRenderer = proxyObject.AddComponent<MeshRenderer>();
                proxyFilter.sharedMesh = sourceFilter.sharedMesh;
                ConfigureProxyRenderer(source, proxyRenderer);
                proxies.Add(new HighlightProxy
                {
                    Source = source,
                    Highlight = proxyRenderer,
                    GameObject = proxyObject
                });
            }

            foreach (SkinnedMeshRenderer source in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (source.sharedMesh == null ||
                    (source.gameObject.hideFlags & HideFlags.DontSave) != 0)
                {
                    continue;
                }

                GameObject proxyObject = CreateProxyObject(source.transform);
                SkinnedMeshRenderer proxyRenderer = proxyObject.AddComponent<SkinnedMeshRenderer>();
                proxyRenderer.sharedMesh = source.sharedMesh;
                proxyRenderer.rootBone = source.rootBone;
                proxyRenderer.bones = source.bones;
                proxyRenderer.localBounds = source.localBounds;
                proxyRenderer.quality = source.quality;
                proxyRenderer.updateWhenOffscreen = source.updateWhenOffscreen;
                ConfigureProxyRenderer(source, proxyRenderer);
                proxies.Add(new HighlightProxy
                {
                    Source = source,
                    Highlight = proxyRenderer,
                    GameObject = proxyObject
                });
            }
        }

        private static GameObject CreateProxyObject(Transform sourceTransform)
        {
            GameObject proxyObject = new GameObject("Interaction Highlight")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = sourceTransform.gameObject.layer
            };
            proxyObject.transform.SetParent(sourceTransform, false);
            return proxyObject;
        }

        private void ConfigureProxyRenderer(Renderer source, Renderer highlight)
        {
            highlight.shadowCastingMode = ShadowCastingMode.Off;
            highlight.receiveShadows = false;
            highlight.lightProbeUsage = LightProbeUsage.Off;
            highlight.reflectionProbeUsage = ReflectionProbeUsage.Off;
            highlight.allowOcclusionWhenDynamic = false;
            highlight.renderingLayerMask = source.renderingLayerMask;
            highlight.sortingLayerID = source.sortingLayerID;
            highlight.sortingOrder = source.sortingOrder + 1;
            AssignMaterial(highlight);
            highlight.enabled = false;
        }

        private void AssignMaterial(Renderer renderer)
        {
            if (renderer == null || highlightMaterial == null)
            {
                return;
            }

            int materialCount = GetSubMeshCount(renderer);
            Material[] materials = new Material[materialCount];
            for (int index = 0; index < materials.Length; index++)
            {
                materials[index] = highlightMaterial;
            }

            renderer.sharedMaterials = materials;
        }

        private static int GetSubMeshCount(Renderer renderer)
        {
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    return Mathf.Max(1, filter.sharedMesh.subMeshCount);
                }
            }
            else if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                return Mathf.Max(1, skinned.sharedMesh.subMeshCount);
            }

            return 1;
        }

        private void RefreshProxyVisibility()
        {
            foreach (HighlightProxy proxy in proxies)
            {
                if (proxy.Highlight != null)
                {
                    bool fadeIsVisible = isVisible || visibility > 0.001f;
                    proxy.Highlight.enabled = !(echoCharacter != null && echoCharacter.HasIntegratedHighlight)
                        && fadeIsVisible && proxy.Source != null && proxy.Source.enabled;
                }
            }
        }

        private void OnDisable()
        {
            Hide();
            if (echoCharacter != null) echoCharacter.SetInteractionHighlight(0f);
        }

        private void OnDestroy()
        {
            if (echoCharacter != null) echoCharacter.SetInteractionHighlight(0f);
            if (highlightMaterial != null)
            {
                Destroy(highlightMaterial);
            }

            foreach (HighlightProxy proxy in proxies)
            {
                if (proxy.GameObject != null)
                {
                    Destroy(proxy.GameObject);
                }
            }

            proxies.Clear();
        }
    }
}
