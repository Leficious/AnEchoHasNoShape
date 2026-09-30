using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>
    /// Builds runtime-only combined meshes for Terrain tree instances. Unity renders
    /// terrain trees internally, so they do not expose scene MeshRenderers for the
    /// normal EchoReactiveSurface installer to discover.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    public sealed class TerrainTreeEchoProxy : MonoBehaviour
    {
        private const string ProxyRootName = "Terrain Tree Echo Proxies (Runtime)";

        private Terrain sourceTerrain;
        private GameObject proxyRoot;
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();

        public static void EnsureFor(Terrain terrain)
        {
            if (terrain == null || IgnoreEcholocation.IsIgnored(terrain.transform))
            {
                return;
            }

            TerrainTreeEchoProxy proxy = terrain.GetComponent<TerrainTreeEchoProxy>();
            if (proxy == null)
            {
                proxy = terrain.gameObject.AddComponent<TerrainTreeEchoProxy>();
            }

            proxy.Build();
        }

        private void Awake()
        {
            sourceTerrain = GetComponent<Terrain>();
        }

        public void Build()
        {
            if (proxyRoot != null)
            {
                return;
            }

            sourceTerrain ??= GetComponent<Terrain>();
            TerrainData terrainData = sourceTerrain != null ? sourceTerrain.terrainData : null;
            if (terrainData == null || terrainData.treeInstanceCount == 0)
            {
                return;
            }

            TreePrototype[] prototypes = terrainData.treePrototypes;
            TreeInstance[] instances = terrainData.treeInstances;

            proxyRoot = new GameObject(ProxyRootName)
            {
                hideFlags = HideFlags.DontSave,
                layer = gameObject.layer
            };
            proxyRoot.transform.SetParent(transform, false);

            for (int prototypeIndex = 0; prototypeIndex < prototypes.Length; prototypeIndex++)
            {
                GameObject prefab = prototypes[prototypeIndex].prefab;
                if (prefab == null)
                {
                    continue;
                }

                foreach (MeshRenderer sourceRenderer in GetFirstLodRenderers(prefab))
                {
                    MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                    Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
                    if (sourceMesh == null)
                    {
                        continue;
                    }

                    Material[] sourceMaterials = sourceRenderer.sharedMaterials;
                    int subMeshCount = Mathf.Min(sourceMesh.subMeshCount, Mathf.Max(1, sourceMaterials.Length));
                    Matrix4x4 meshToPrefab = prefab.transform.worldToLocalMatrix * sourceRenderer.transform.localToWorldMatrix;

                    for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                    {
                        Material sourceMaterial = sourceMaterials.Length > subMeshIndex
                            ? sourceMaterials[subMeshIndex]
                            : null;
                        BuildSubMeshProxy(
                            terrainData,
                            instances,
                            prototypeIndex,
                            sourceMesh,
                            subMeshIndex,
                            meshToPrefab,
                            sourceMaterial,
                            prefab.name);
                    }
                }
            }
        }

        private void BuildSubMeshProxy(
            TerrainData terrainData,
            TreeInstance[] instances,
            int prototypeIndex,
            Mesh sourceMesh,
            int subMeshIndex,
            Matrix4x4 meshToPrefab,
            Material sourceMaterial,
            string prototypeName)
        {
            var combines = new List<CombineInstance>();

            foreach (TreeInstance instance in instances)
            {
                if (instance.prototypeIndex != prototypeIndex)
                {
                    continue;
                }

                Vector3 localPosition = Vector3.Scale(instance.position, terrainData.size);
                Quaternion localRotation = Quaternion.Euler(0f, instance.rotation * Mathf.Rad2Deg, 0f);
                Vector3 localScale = new Vector3(instance.widthScale, instance.heightScale, instance.widthScale);

                combines.Add(new CombineInstance
                {
                    mesh = sourceMesh,
                    subMeshIndex = subMeshIndex,
                    transform = Matrix4x4.TRS(localPosition, localRotation, localScale) * meshToPrefab
                });
            }

            if (combines.Count == 0)
            {
                return;
            }

            Mesh combinedMesh = new Mesh
            {
                name = $"{prototypeName} Tree Echo Mesh (Runtime)",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.DontSave
            };
            combinedMesh.CombineMeshes(combines.ToArray(), true, true, false);
            combinedMesh.RecalculateBounds();
            generatedMeshes.Add(combinedMesh);

            GameObject proxy = new GameObject($"{prototypeName} Echo {subMeshIndex}")
            {
                hideFlags = HideFlags.DontSave,
                layer = gameObject.layer
            };
            proxy.transform.SetParent(proxyRoot.transform, false);

            MeshFilter filter = proxy.AddComponent<MeshFilter>();
            filter.sharedMesh = combinedMesh;

            MeshRenderer renderer = proxy.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sourceMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            proxy.AddComponent<EchoReactiveSurface>();

            // EchoReactiveSurface has already copied the mesh/material data into
            // its two overlay children. The source proxy itself must stay hidden.
            renderer.enabled = false;
        }

        private static IEnumerable<MeshRenderer> GetFirstLodRenderers(GameObject prefab)
        {
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                LODGroup lodGroup = renderer.GetComponentInParent<LODGroup>();
                if (lodGroup == null || IsInFirstLod(renderer, lodGroup))
                {
                    yield return renderer;
                }
            }
        }

        private static bool IsInFirstLod(Renderer renderer, LODGroup lodGroup)
        {
            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length == 0)
            {
                return true;
            }

            foreach (Renderer firstLodRenderer in lods[0].renderers)
            {
                if (firstLodRenderer == renderer)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in generatedMeshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }

            generatedMeshes.Clear();
        }
    }
}
