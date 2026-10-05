using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    public sealed class CityPerspectiveGroup : MonoBehaviour
    {
        private CitySequenceController sequence;
        private int perspective;
        private MeshRenderer[] sources;
        private bool[] originalEnabled;
        private SkinnedMeshRenderer[] skinnedSources;
        private Material[][] originalSkinnedMaterials;
        private Material skinnedEchoMaterial;
        public bool IsVisibleAt(Vector3 position) => sequence != null && sequence.PerspectiveVisibility(perspective, position) > 0.1f;

        public void Configure(CitySequenceController controller, int identity)
        {
            sequence = controller;
            perspective = identity;
            EchoReactiveSurface[] surfaces = GetComponentsInChildren<EchoReactiveSurface>(true);
            sources = new MeshRenderer[surfaces.Length];
            originalEnabled = new bool[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                sources[i] = surfaces[i].GetComponent<MeshRenderer>();
                originalEnabled[i] = sources[i].enabled;
                sources[i].enabled = false;
                surfaces[i].SetCityPerspective(identity);
            }
            // Animated birds (and other skinned props) cannot use the MeshFilter
            // overlays. Keep their rig intact and gate their own draw by city waves.
            skinnedSources = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            originalSkinnedMaterials = new Material[skinnedSources.Length][];
            if (skinnedSources.Length > 0)
            {
                Shader shader = Resources.Load<Shader>("Rendering/CitySkinnedEcho");
                if (shader == null)
                {
                    Debug.LogError("Missing city skinned echo shader.", this);
                    return;
                }
                skinnedEchoMaterial = new Material(shader) { name = "City Perspective Skinned Echo (Runtime)" };
                skinnedEchoMaterial.SetFloat("_CityPerspective", identity);
                for (int i = 0; i < skinnedSources.Length; i++)
                {
                    originalSkinnedMaterials[i] = skinnedSources[i].sharedMaterials;
                    Material[] materials = new Material[Mathf.Max(1, originalSkinnedMaterials[i].Length)];
                    for (int slot = 0; slot < materials.Length; slot++) materials[slot] = skinnedEchoMaterial;
                    skinnedSources[i].sharedMaterials = materials;
                }
            }
        }

        private void OnDestroy()
        {
            if (skinnedSources != null)
                for (int i = 0; i < skinnedSources.Length; i++)
                    if (skinnedSources[i] != null && originalSkinnedMaterials[i] != null)
                        skinnedSources[i].sharedMaterials = originalSkinnedMaterials[i];
            if (skinnedEchoMaterial != null) Destroy(skinnedEchoMaterial);
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].enabled = originalEnabled[i];
        }
    }
}
