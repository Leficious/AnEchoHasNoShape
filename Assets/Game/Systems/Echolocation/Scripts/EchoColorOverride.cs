using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    [AddComponentMenu("An Echo Has No Shape/Echolocation/Echo Color Override")]
    [DisallowMultipleComponent]
    public sealed class EchoColorOverride : MonoBehaviour
    {
        [Tooltip("Color used when echolocation reveals this object and its children.")]
        [SerializeField, ColorUsage(true, true)]
        private Color echoColor = new Color(1f, 0.55f, 0.18f, 1f);

        [Tooltip("Seconds the accumulated outline remains after an echo reaches this object. Zero disables lingering reveal.")]
        [SerializeField, Min(0f)] private float revealedDuration = 4f;

        [Tooltip("Seconds used to fade the accumulated outline after its revealed duration ends.")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.9f;

        [Tooltip("Strength of the subtle brightness shimmer while the object remains revealed.")]
        [SerializeField, Range(0f, 0.5f)] private float shimmerStrength = 0.12f;

        [Tooltip("Speed of the reveal shimmer.")]
        [SerializeField, Range(0.1f, 8f)] private float shimmerSpeed = 2.2f;

        public Color EchoColor => echoColor;
        public float RevealedDuration => revealedDuration;
        public float FadeOutDuration => fadeOutDuration;
        public float ShimmerStrength => shimmerStrength;
        public float ShimmerSpeed => shimmerSpeed;

        public void SetEchoColor(Color color)
        {
            echoColor = color;
            RefreshSurfaces();
        }

        public void SetRevealedDuration(float duration)
        {
            revealedDuration = Mathf.Max(0f, duration);
            RefreshSurfaces();
        }

        private void OnEnable()
        {
            RefreshSurfaces();
        }

        private void OnDisable()
        {
            RefreshSurfaces();
        }

        private void OnValidate()
        {
            revealedDuration = Mathf.Max(0f, revealedDuration);
            fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
            shimmerStrength = Mathf.Clamp(shimmerStrength, 0f, 0.5f);
            shimmerSpeed = Mathf.Clamp(shimmerSpeed, 0.1f, 8f);
            RefreshSurfaces();
        }

        private void RefreshSurfaces()
        {
            EchoReactiveSurface[] surfaces = GetComponentsInChildren<EchoReactiveSurface>(true);
            foreach (EchoReactiveSurface surface in surfaces)
            {
                surface.RefreshEchoColorOverride();
            }
        }
    }
}
