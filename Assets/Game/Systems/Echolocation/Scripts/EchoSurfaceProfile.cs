using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>
    /// Optional per-object tuning for vegetation, large irregular meshes, or
    /// any asset that needs a softer echo treatment.
    /// </summary>
    [AddComponentMenu("An Echo Has No Shape/Echolocation/Echo Surface Profile")]
    [DisallowMultipleComponent]
    public sealed class EchoSurfaceProfile : MonoBehaviour
    {
        [SerializeField, Range(0f, 2f)] private float revealMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float wireframeMultiplier = 1f;
        [SerializeField, Range(0f, 2f)] private float outlineWidthMultiplier = 1f;

        public float RevealMultiplier => revealMultiplier;
        public float WireframeMultiplier => wireframeMultiplier;
        public float OutlineWidthMultiplier => outlineWidthMultiplier;

        private void OnValidate()
        {
            revealMultiplier = Mathf.Clamp(revealMultiplier, 0f, 2f);
            wireframeMultiplier = Mathf.Clamp01(wireframeMultiplier);
            outlineWidthMultiplier = Mathf.Clamp(outlineWidthMultiplier, 0f, 2f);
        }
    }
}
