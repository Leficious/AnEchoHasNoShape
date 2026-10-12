using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    public sealed class WoodsIllusionTree : MonoBehaviour
    {
        // Called by woods completion logic. Never edits a TerrainData asset.
        public static void HideAllIllusions(bool animate = true)
        {
            if (!Application.isPlaying) return;
            WoodsEchoResponse response = WoodsEchoResponse.Instance;
            if (response == null)
            {
                GameObject source = GameObject.Find("WoodsEchoSource");
                if (source == null) source = new GameObject("WoodsEchoSource");
                response = source.GetComponent<WoodsEchoResponse>();
                if (response == null) response = source.AddComponent<WoodsEchoResponse>();
            }
            response.HideIllusions(animate);
        }

        public void HideSceneInstance()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }

        public static bool IsIllusion(Transform target)
        {
            foreach (WoodsIllusionTree marker in target.GetComponentsInParent<WoodsIllusionTree>(true))
                if (marker.enabled) return true;
            return false;
        }
    }
}
