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

        [Header("Architecture Treatment")]
        [SerializeField] private bool softArchitecture;
        [SerializeField] private Color fillTint = Color.white;
        [SerializeField, Range(0f, 1f)] private float fillBrightness = 0.25f;
        [SerializeField, Range(0f, 1f)] private float outlineBrightness = 0.8f;
        [SerializeField, Range(0f, 1f)] private float directionalShading = 0.45f;
        public bool SoftArchitecture => softArchitecture;
        public Color FillTint => fillTint;
        public float FillBrightness => fillBrightness;
        public float OutlineBrightness => outlineBrightness;
        public float DirectionalShading => directionalShading;
        [Tooltip("Fraction of each mesh's height over which its echo fades in from the bottom. Zero disables the gradient.")]
        [SerializeField, Range(0f, 1f)] private float groundFadeHeightFraction = 0.25f;
        public float GroundFadeHeightFraction => groundFadeHeightFraction;

        public void ConfigureSoftArchitecture()
        {
            softArchitecture = true;
            wireframeMultiplier = 0f;
            foreach (EchoReactiveSurface surface in GetComponentsInChildren<EchoReactiveSurface>(true))
                surface.RefreshSurfaceProfile();
        }

        private void OnValidate()
        {
            revealMultiplier = Mathf.Clamp(revealMultiplier, 0f, 2f);
            wireframeMultiplier = Mathf.Clamp01(wireframeMultiplier);
            outlineWidthMultiplier = Mathf.Clamp(outlineWidthMultiplier, 0f, 2f);
            if (Application.isPlaying)
                foreach (EchoReactiveSurface surface in GetComponentsInChildren<EchoReactiveSurface>(true))
                    surface.RefreshSurfaceProfile();
        }
    }
}
