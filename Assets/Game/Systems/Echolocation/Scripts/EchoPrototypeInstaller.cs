using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    public static class EchoPrototypeInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null && player.GetComponent<EchoPulseController>() == null)
            {
                player.AddComponent<EchoPulseController>();
            }

            MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);

            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer.GetComponent<MeshFilter>() == null ||
                    (renderer.gameObject.hideFlags & HideFlags.DontSave) != 0 ||
                    renderer.transform.root.CompareTag("Player") ||
                    renderer.GetComponentInParent<EchoRenderingExclusion>(true) != null)
                {
                    continue;
                }

                if (IsWaterRenderer(renderer))
                {
                    if (renderer.GetComponent<WorldReverberationWaterSurface>() == null)
                    {
                        renderer.gameObject.AddComponent<WorldReverberationWaterSurface>();
                    }

                    continue;
                }

                if (renderer.GetComponent<EchoReactiveSurface>() != null)
                {
                    continue;
                }

                renderer.gameObject.AddComponent<EchoReactiveSurface>();
            }

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);

            foreach (Terrain terrain in terrains)
            {
                if (!IgnoreEcholocation.IsIgnored(terrain.transform) &&
                    terrain.GetComponent<EchoReactiveTerrain>() == null)
                {
                    terrain.gameObject.AddComponent<EchoReactiveTerrain>();
                }

                TerrainTreeEchoProxy.EnsureFor(terrain);
            }
        }

        private static bool IsWaterRenderer(MeshRenderer renderer)
        {
            if (renderer.gameObject.name.ToLowerInvariant().Contains("water"))
            {
                return true;
            }

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                string description = $"{material.name} {(material.shader != null ? material.shader.name : string.Empty)}";
                if (description.ToLowerInvariant().Contains("water"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
