using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>Terrain details don't execute prefab behaviours. Render marked
    /// layers as patch-sized echo meshes, hiding originals only on a runtime copy.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Terrain))]
    public sealed class TerrainMuseVegetationProxy : MonoBehaviour
    {
        private Terrain terrain;
        private TerrainData original, copy;
        private GameObject root;
        private readonly List<Mesh> meshes = new();
        private readonly List<Material> materials = new();
        private readonly List<MeshRenderer> draws = new();
        private Camera view;
        private static readonly Dictionary<TerrainData, TerrainData> releasedCopies = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => releasedCopies.Clear();

        // Woods completion can temporarily clone our runtime terrain again.
        // If its owner outlives this component, restore the asset, not a dead copy.
        public static TerrainData ResolveRestoredData(TerrainData data)
        {
            if (ReferenceEquals(data, null)) return null;
            return releasedCopies.TryGetValue(data, out var restored) ? restored : data;
        }

        public static void EnsureFor(Terrain target)
        {
            if (target == null || target.terrainData == null || target.GetComponent<TerrainMuseVegetationProxy>() != null) return;
            foreach (var detail in target.terrainData.detailPrototypes)
                if (detail.prototype != null && detail.prototype.GetComponentInChildren<CityMuseVegetation>(true) != null)
                {
                    target.gameObject.AddComponent<TerrainMuseVegetationProxy>();
                    return;
                }
        }

        private void Start()
        {
            terrain = GetComponent<Terrain>();
            original = terrain.terrainData;
            Shader shader = Resources.Load<Shader>("Rendering/CityMuseVegetation");
            if (original == null || shader == null) return;
            root = new GameObject("Muse Terrain Flowers (Runtime)") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            root.transform.SetParent(transform, false);
            root.AddComponent<EchoRenderingExclusion>();
            var details = original.detailPrototypes;
            for (int layer = 0; layer < details.Length; layer++)
            {
                var prefab = details[layer].prototype;
                var marker = prefab != null ? prefab.GetComponent<CityMuseVegetation>() : null;
                if (marker == null || !marker.enabled || !details[layer].usePrototypeMesh) continue;
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                bool valid = filters.Length > 0;
                foreach (var filter in filters)
                {
                    if (filter.sharedMesh == null || (!Application.isEditor && !filter.sharedMesh.isReadable)
                        || filter.GetComponent<MeshRenderer>() == null) valid = false;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer != null && filter.sharedMesh != null)
                    {
                        var slots = renderer.sharedMaterials;
                        if (slots.Length < filter.sharedMesh.subMeshCount) valid = false;
                        foreach (var slot in slots) if (slot == null) valid = false;
                    }
                }
                if (!valid)
                {
                    Debug.LogError($"Cannot build Muse details for {prefab.name}; meshes require Read/Write for player builds.", this);
                    continue; // Never hide a layer we cannot render.
                }
                int[,] density = original.GetDetailLayer(0, 0, original.detailWidth, original.detailHeight, layer);
                bool populated = false;
                foreach (int value in density) if (value > 0) { populated = true; break; }
                if (!populated) continue;
                var layerMaterials = new Dictionary<Material, Material>();
                int patches = original.detailPatchCount;
                int patchSize = original.detailResolutionPerPatch;
                for (int z = 0; z < patches; z++) for (int x = 0; x < patches; x++)
                {
                    bool occupied = false;
                    for (int dz = 0; dz < patchSize && !occupied; dz++)
                        for (int dx = 0; dx < patchSize; dx++)
                            if (density[z * patchSize + dz, x * patchSize + dx] > 0) { occupied = true; break; }
                    if (!occupied) continue;
                    var instances = original.ComputeDetailInstanceTransforms(x, z, layer, terrain.detailObjectDensity, out _);
                    foreach (var filter in filters)
                    {
                        Mesh source = filter.sharedMesh;
                        var sourceMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                        Matrix4x4 local = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        for (int sub = 0; sub < source.subMeshCount; sub++)
                        {
                            Material sourceMaterial = sourceMaterials.Length > sub ? sourceMaterials[sub] : null;
                            if (sourceMaterial == null) continue;
                            if (!layerMaterials.TryGetValue(sourceMaterial, out Material echo))
                            {
                                echo = marker.CreateRuntimeMaterial(shader, sourceMaterial);
                                layerMaterials.Add(sourceMaterial, echo); materials.Add(echo);
                            }
                            // Bound the size of each temporary combine array/mesh.
                            for (int start = 0; start < instances.Length; start += 128)
                            {
                                var combine = new CombineInstance[Mathf.Min(128, instances.Length - start)];
                                for (int i = 0; i < combine.Length; i++)
                                {
                                    var t = instances[start + i];
                                    combine[i] = new CombineInstance { mesh = source, subMeshIndex = sub,
                                        transform = Matrix4x4.TRS(new Vector3(t.posX, t.posY, t.posZ),
                                            Quaternion.Euler(0, t.rotationY * Mathf.Rad2Deg, 0), new Vector3(t.scaleXZ, t.scaleY, t.scaleXZ)) * local };
                                }
                                var mesh = new Mesh { name = prefab.name + " Muse Patch", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                                mesh.CombineMeshes(combine, true, true); meshes.Add(mesh);
                                var go = new GameObject(prefab.name + " Echo Patch") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
                                go.transform.SetParent(root.transform, false);
                                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                                var draw = go.AddComponent<MeshRenderer>(); draw.sharedMaterial = echo;
                                draw.shadowCastingMode = ShadowCastingMode.Off; draw.receiveShadows = false;
                                draw.lightProbeUsage = LightProbeUsage.Off; draw.reflectionProbeUsage = ReflectionProbeUsage.Off;
                                draws.Add(draw);
                            }
                        }
                    }
                }
                if (copy == null) { copy = Instantiate(original); copy.name = original.name + " (Muse Runtime)"; copy.hideFlags = HideFlags.DontSave; }
                copy.SetDetailLayer(0, 0, layer, new int[original.detailHeight, original.detailWidth]);
            }
            if (copy != null) terrain.terrainData = copy;
        }

        private void LateUpdate()
        {
            if (view == null) view = Camera.main;
            if (view == null || terrain == null) return;
            float distance = terrain.detailObjectDistance;
            foreach (var draw in draws)
                if (draw != null) draw.enabled = terrain.enabled && terrain.drawTreesAndFoliage
                    && draw.bounds.SqrDistance(view.transform.position) < distance * distance;
        }

        private void OnDestroy()
        {
            if (copy != null)
            {
                releasedCopies[copy] = original;
                if (terrain != null && terrain.terrainData == copy) terrain.terrainData = original;
                Destroy(copy);
            }
            if (root != null) Destroy(root);
            foreach (Mesh mesh in meshes) Destroy(mesh);
            foreach (Material material in materials) Destroy(material);
        }
    }
}
