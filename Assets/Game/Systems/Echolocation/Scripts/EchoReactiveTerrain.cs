using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    public sealed class EchoReactiveTerrain : MonoBehaviour
    {
        private const string EchoTerrainShaderName = "An Echo Has No Shape/Terrain/Echo Lit";

        private Terrain sourceTerrain;
        private Material originalMaterial;
        private Material echoMaterial;

        private void Awake()
        {
            sourceTerrain = GetComponent<Terrain>();
            Material shaderReference = Resources.Load<Material>("Rendering/EchoTerrainRuntime");
            Shader echoTerrainShader = shaderReference != null && shaderReference.shader != null
                ? shaderReference.shader
                : Shader.Find(EchoTerrainShaderName);

            if (echoTerrainShader == null)
            {
                Debug.LogError($"Terrain echo shader '{EchoTerrainShaderName}' could not be found.", this);
                enabled = false;
                return;
            }

            originalMaterial = sourceTerrain.materialTemplate;
            echoMaterial = new Material(echoTerrainShader)
            {
                name = $"{gameObject.name} Echo Terrain Material (Runtime)",
                hideFlags = HideFlags.DontSave
            };

            // Preserve any values from an explicitly assigned Terrain material.
            // Terrain layer textures and controls are still supplied by Unity per terrain patch.
            if (originalMaterial != null)
            {
                echoMaterial.CopyPropertiesFromMaterial(originalMaterial);
            }

            // Match the instanced build-reference material even if copying the
            // original terrain material above replaced its instancing setting.
            echoMaterial.enableInstancing = true;
            sourceTerrain.materialTemplate = echoMaterial;
            RefreshEchoIgnoreState();
        }

        public void RefreshEchoIgnoreState()
        {
            if (sourceTerrain == null || echoMaterial == null)
            {
                return;
            }

            sourceTerrain.materialTemplate = IgnoreEcholocation.IsIgnored(transform)
                ? originalMaterial
                : echoMaterial;
        }

        private void OnDestroy()
        {
            if (sourceTerrain != null && sourceTerrain.materialTemplate == echoMaterial)
            {
                sourceTerrain.materialTemplate = originalMaterial;
            }

            if (echoMaterial != null)
            {
                Destroy(echoMaterial);
            }
        }
    }
}
