using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("An Echo Has No Shape/Echolocation/City Visibility")]
    public sealed class CityVisibility : MonoBehaviour
    {
        [Tooltip("Visible for set dressing in Edit Mode; only their echo overlays are visible in game. Does not disable objects or colliders.")]
        [SerializeField] private MeshRenderer[] hiddenAtRuntime = new MeshRenderer[0];

        private void OnEnable()
        {
            if (Application.isPlaying) ApplyRuntimeVisibility();
        }

        public void ApplyRuntimeVisibility()
        {
            foreach (MeshRenderer source in hiddenAtRuntime)
                if (source != null && source.transform.IsChildOf(transform)) source.enabled = false;
        }

        public MeshRenderer[] HiddenAtRuntime => hiddenAtRuntime;
    }
}
