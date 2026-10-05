using UnityEngine;
using UnityEngine.Events;

namespace AnEchoHasNoShape.Echolocation
{
    [AddComponentMenu("An Echo Has No Shape/Echolocation/Echo Reveal State")]
    [DisallowMultipleComponent]
    public sealed class EchoRevealState : MonoBehaviour
    {
        [Header("Reveal Window")]
        [Tooltip("Seconds the object remains fully revealed after the echo reaches it.")]
        [SerializeField, Min(0f)] private float revealedDuration = 4f;

        [Tooltip("Seconds the reveal takes to fade after the full reveal window.")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.9f;

        [Tooltip("Once discovered, keep this object revealed and interactable for the rest of the scene.")]
        [SerializeField] private bool remainRevealedAfterFirstEcho;

        [Header("Events")]
        [SerializeField] private UnityEvent onRevealed;
        [SerializeField] private UnityEvent onHidden;

        private MeshRenderer[] meshRenderers;
        private SkinnedMeshRenderer[] skinnedRenderers;
        private Terrain[] terrains;
        private Collider[] colliders;
        private int lastTriggeredPulseSequence = -1;
        private float revealHoldUntilTime;
        private float revealFadeUntilTime;
        private float revealAmount;
        private bool permanentlyRevealed;

        public float RevealAmount => revealAmount;
        public bool IsRevealed => revealAmount > 0.01f;

        private void Awake()
        {
            CacheBoundsSources();
        }

        private void OnEnable()
        {
            RefreshChildSurfaces();
        }

        private void Update()
        {
            if (IgnoreEcholocation.IsIgnored(transform))
            {
                SetRevealAmount(0f);
                return;
            }

            UpdateRevealAmount();

            if (permanentlyRevealed ||
                !EchoPulseController.IsPulseActive ||
                lastTriggeredPulseSequence == EchoPulseController.ActivePulseSequence ||
                !TryGetWorldBounds(out Bounds bounds))
            {
                return;
            }

            Vector3 nearestPoint = bounds.ClosestPoint(EchoPulseController.ActivePulseOrigin);
            float distanceToSurface = Vector3.Distance(EchoPulseController.ActivePulseOrigin, nearestPoint);

            if (distanceToSurface <= EchoPulseController.ActivePulseRadius &&
                distanceToSurface <= EchoPulseController.ActivePulseRange)
            {
                lastTriggeredPulseSequence = EchoPulseController.ActivePulseSequence;
                Reveal();
            }
        }

        public void ConfigureWindow(float holdSeconds, float fadeSeconds)
        {
            revealedDuration = Mathf.Max(0f, holdSeconds);
            fadeOutDuration = Mathf.Max(0f, fadeSeconds);
        }

        public void Reveal()
        {
            if (remainRevealedAfterFirstEcho)
            {
                permanentlyRevealed = true;
                SetRevealAmount(1f);
                return;
            }

            revealHoldUntilTime = Time.time + revealedDuration;
            revealFadeUntilTime = revealedDuration > 0f
                ? revealHoldUntilTime + fadeOutDuration
                : revealHoldUntilTime;
            SetRevealAmount(revealedDuration > 0f ? 1f : 0f);
        }

        public void ConsumeReveal()
        {
            permanentlyRevealed = false;
            revealHoldUntilTime = 0f;
            revealFadeUntilTime = 0f;
            SetRevealAmount(0f);
        }

        private void UpdateRevealAmount()
        {
            if (permanentlyRevealed)
            {
                SetRevealAmount(1f);
                return;
            }

            float nextAmount = 0f;
            if (Time.time < revealHoldUntilTime)
            {
                nextAmount = 1f;
            }
            else if (fadeOutDuration > 0f && Time.time < revealFadeUntilTime)
            {
                float progress = Mathf.InverseLerp(revealHoldUntilTime, revealFadeUntilTime, Time.time);
                nextAmount = 1f - Mathf.SmoothStep(0f, 1f, progress);
            }

            SetRevealAmount(nextAmount);
        }

        private void SetRevealAmount(float amount)
        {
            amount = Mathf.Clamp01(amount);
            bool wasRevealed = IsRevealed;
            revealAmount = amount;
            bool isNowRevealed = IsRevealed;

            if (!wasRevealed && isNowRevealed)
            {
                onRevealed?.Invoke();
            }
            else if (wasRevealed && !isNowRevealed)
            {
                onHidden?.Invoke();
            }
        }

        private void CacheBoundsSources()
        {
            meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            terrains = GetComponentsInChildren<Terrain>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private bool TryGetWorldBounds(out Bounds combinedBounds)
        {
            combinedBounds = default;
            bool hasBounds = false;

            if (meshRenderers == null)
            {
                CacheBoundsSources();
            }

            foreach (MeshRenderer renderer in meshRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Encapsulate(ref combinedBounds, ref hasBounds, renderer.bounds);
            }

            foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
                if (renderer != null) Encapsulate(ref combinedBounds, ref hasBounds, renderer.bounds);

            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Bounds localBounds = terrain.terrainData.bounds;
                Vector3 scale = terrain.transform.lossyScale;
                Vector3 size = Vector3.Scale(localBounds.size,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                Encapsulate(ref combinedBounds, ref hasBounds,
                    new Bounds(terrain.transform.TransformPoint(localBounds.center), size));
            }

            if (!hasBounds)
            {
                foreach (Collider targetCollider in colliders)
                {
                    if (targetCollider != null)
                    {
                        Encapsulate(ref combinedBounds, ref hasBounds, targetCollider.bounds);
                    }
                }
            }

            return hasBounds;
        }

        private static void Encapsulate(ref Bounds combinedBounds, ref bool hasBounds, Bounds bounds)
        {
            if (!hasBounds)
            {
                combinedBounds = bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(bounds);
            }
        }

        private void RefreshChildSurfaces()
        {
            EchoReactiveSurface[] surfaces = GetComponentsInChildren<EchoReactiveSurface>(true);
            foreach (EchoReactiveSurface surface in surfaces)
            {
                surface.RefreshEchoColorOverride();
            }
        }

        private void OnValidate()
        {
            revealedDuration = Mathf.Max(0f, revealedDuration);
            fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        }
    }
}
