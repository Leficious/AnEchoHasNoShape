using System.Collections.Generic;
using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>Muse-only vegetation. Scene instances swap their materials;
    /// terrain-painted instances are handled by TerrainMuseVegetationProxy.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("An Echo Has No Shape/Echolocation/City Muse Vegetation")]
    public sealed class CityMuseVegetation : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float opacity = 0.8f;
        [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.35f;
        private readonly List<Renderer> sources = new List<Renderer>();
        private readonly List<Material[]> originals = new List<Material[]>();
        private readonly List<bool> originalEnabled = new List<bool>();
        private readonly List<Material> generated = new List<Material>();

        public Material CreateRuntimeMaterial(Shader shader, Material original)
        {
            var material = new Material(shader) { name = "Muse Vegetation (Runtime)", hideFlags = HideFlags.DontSave };
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_Cutoff", alphaCutoff);
            if (original != null)
                foreach (string property in new[] { "_BaseTexture", "_BASETEXTURE", "_BaseMap", "_MainTex" })
                    if (original.HasProperty(property) && original.GetTexture(property) != null)
                    {
                        material.SetTexture("_MainTex", original.GetTexture(property));
                        material.SetTextureScale("_MainTex", original.GetTextureScale(property));
                        material.SetTextureOffset("_MainTex", original.GetTextureOffset(property));
                        break;
                    }
            return material;
        }

        private void Start()
        {
            Shader shader = Resources.Load<Shader>("Rendering/CityMuseVegetation");
            if (shader == null) { Debug.LogError("Missing CityMuseVegetation shader.", this); return; }
            foreach (Renderer source in GetComponentsInChildren<Renderer>(true))
            {
                if (!(source is MeshRenderer) && !(source is SkinnedMeshRenderer)) continue;
                if (source.GetComponentInParent<CityMuseVegetation>(true) != this) continue;
                Material[] original = source.sharedMaterials;
                Material[] replacement = new Material[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    Material material = CreateRuntimeMaterial(shader, original[i]);
                    generated.Add(material);
                    replacement[i] = material;
                }
                sources.Add(source);
                originals.Add(original);
                originalEnabled.Add(source.enabled);
                source.sharedMaterials = replacement;
                // CityVisibility may have hidden these sources; the shader now owns visibility.
                source.enabled = true;
                EchoReactiveSurface overlay = source.GetComponent<EchoReactiveSurface>();
                if (overlay != null) overlay.SetOverlayRendering(false);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < sources.Count; i++)
                if (sources[i] != null)
                {
                    sources[i].sharedMaterials = originals[i];
                    sources[i].enabled = originalEnabled[i];
                }
            foreach (Material material in generated) if (material != null) Destroy(material);
        }
    }
}
