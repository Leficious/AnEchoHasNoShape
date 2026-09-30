using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnEchoHasNoShape.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PhaseThroughVolume : MonoBehaviour
    {
        private struct RendererState
        {
            public Renderer Renderer;
            public bool WasEnabled;
        }

        [Header("Phase Veil")]
        [SerializeField] private Color veilColor = new Color(0.055f, 0.095f, 0.12f, 1f);
        [SerializeField] private Color shimmerColor = new Color(0.55f, 0.83f, 0.9f, 1f);
        [SerializeField, Range(0f, 1f)] private float maximumOpacity = 0.82f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.3f;
        [SerializeField, Range(0f, 0.06f)] private float distortionStrength = 0.024f;
        [SerializeField, Range(0f, 3f)] private float vignetteStrength = 1.45f;

        [Header("Object Transition")]
        [Tooltip("Briefly waits for the veil to cover the camera before hiding this object's surface.")]
        [SerializeField, Min(0f)] private float rendererHideDelay = 0.14f;
        [SerializeField] private bool hideRenderersWhileInside = true;

        [Header("Sound")]
        [SerializeField, Range(10f, 22000f)] private float muffledCutoffFrequency = 1150f;

        [Header("Activation")]
        [Tooltip("When disabled, this volume cannot begin a phase transition.")]
        [SerializeField] private bool isArmed = true;

        private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
        private readonly List<RendererState> rendererStates = new List<RendererState>();
        private Coroutine hideRoutine;
        private bool renderersHidden;
        private float ignoreEntriesUntil;

        public void SetArmed(bool armed, bool ignoreCurrentOverlap = true)
        {
            isArmed = armed;
            if (!armed)
            {
                playerColliders.Clear();
                FinishPassage();
                return;
            }

            if (ignoreCurrentOverlap)
            {
                // A trigger can be enabled while the player is already inside a
                // large convex hull. Ignore that initial physics notification;
                // the next genuine exit and re-entry will behave normally.
                ignoreEntriesUntil = Time.unscaledTime + 0.35f;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isArmed || Time.unscaledTime < ignoreEntriesUntil ||
                !IsPlayer(other) || !playerColliders.Add(other) || playerColliders.Count != 1)
            {
                return;
            }

            PhasePassageScreenEffect.Enter(
                this,
                veilColor,
                shimmerColor,
                maximumOpacity,
                fadeDuration,
                distortionStrength,
                vignetteStrength,
                muffledCutoffFrequency);

            if (hideRenderersWhileInside)
            {
                hideRoutine = StartCoroutine(HideRenderersAfterDelay());
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!playerColliders.Remove(other) || playerColliders.Count > 0)
            {
                return;
            }

            FinishPassage();
        }

        private IEnumerator HideRenderersAfterDelay()
        {
            float elapsed = 0f;
            while (elapsed < rendererHideDelay)
            {
                if (playerColliders.Count == 0)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            hideRoutine = null;
            HideRenderers();
        }

        private void HideRenderers()
        {
            if (renderersHidden)
            {
                return;
            }

            rendererStates.Clear();
            foreach (Renderer targetRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (targetRenderer == null ||
                    (targetRenderer.gameObject.hideFlags & HideFlags.DontSave) != 0)
                {
                    continue;
                }

                rendererStates.Add(new RendererState
                {
                    Renderer = targetRenderer,
                    WasEnabled = targetRenderer.enabled
                });
                targetRenderer.enabled = false;
            }

            renderersHidden = true;
        }

        private void RestoreRenderers()
        {
            if (!renderersHidden)
            {
                return;
            }

            foreach (RendererState state in rendererStates)
            {
                if (state.Renderer != null)
                {
                    state.Renderer.enabled = state.WasEnabled;
                }
            }

            rendererStates.Clear();
            renderersHidden = false;
        }

        private void FinishPassage()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            RestoreRenderers();
            PhasePassageScreenEffect.Exit(this);
        }

        private static bool IsPlayer(Collider candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (candidate.CompareTag("Player"))
            {
                return true;
            }

            Transform root = candidate.attachedRigidbody != null
                ? candidate.attachedRigidbody.transform.root
                : candidate.transform.root;
            return root != null && root.CompareTag("Player");
        }

        private void OnDisable()
        {
            playerColliders.Clear();
            FinishPassage();
        }

        private void OnValidate()
        {
            maximumOpacity = Mathf.Clamp01(maximumOpacity);
            fadeDuration = Mathf.Max(0.01f, fadeDuration);
            distortionStrength = Mathf.Clamp(distortionStrength, 0f, 0.06f);
            vignetteStrength = Mathf.Clamp(vignetteStrength, 0f, 3f);
            rendererHideDelay = Mathf.Max(0f, rendererHideDelay);
            muffledCutoffFrequency = Mathf.Clamp(muffledCutoffFrequency, 10f, 22000f);
        }
    }

    internal sealed class PhasePassageScreenEffect : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_PhaseOpacity");
        private static readonly int VeilColorId = Shader.PropertyToID("_PhaseVeilColor");
        private static readonly int ShimmerColorId = Shader.PropertyToID("_PhaseShimmerColor");
        private static readonly int DistortionId = Shader.PropertyToID("_PhaseDistortionStrength");
        private static readonly int VignetteId = Shader.PropertyToID("_PhaseVignetteStrength");
        private static PhasePassageScreenEffect instance;

        private readonly HashSet<PhaseThroughVolume> activeVolumes = new HashSet<PhaseThroughVolume>();
        private AudioLowPassFilter lowPassFilter;
        private bool createdLowPassFilter;
        private bool previousFilterEnabled;
        private float previousCutoffFrequency = 22000f;
        private float targetOpacity;
        private float currentOpacity;
        private float maximumOpacity = 0.82f;
        private float fadeDuration = 0.3f;
        private float targetCutoffFrequency = 1150f;
        private Color veilColor = new Color(0.055f, 0.095f, 0.12f, 1f);
        private Color shimmerColor = new Color(0.55f, 0.83f, 0.9f, 1f);
        private float distortionStrength = 0.024f;
        private float vignetteStrength = 1.45f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStaleRuntimeEffects()
        {
            Shader.SetGlobalFloat(OpacityId, 0f);
            PhasePassageScreenEffect[] staleEffects =
                Resources.FindObjectsOfTypeAll<PhasePassageScreenEffect>();
            foreach (PhasePassageScreenEffect staleEffect in staleEffects)
            {
                if (staleEffect != null)
                {
                    DestroyImmediate(staleEffect.gameObject);
                }
            }

            instance = null;
        }

        public static void Enter(
            PhaseThroughVolume source,
            Color veilColor,
            Color shimmerColor,
            float opacity,
            float duration,
            float distortion,
            float vignette,
            float cutoffFrequency)
        {
            PhasePassageScreenEffect effect = GetOrCreate();
            effect.activeVolumes.Add(source);
            effect.maximumOpacity = Mathf.Clamp01(opacity);
            effect.fadeDuration = Mathf.Max(0.01f, duration);
            effect.targetCutoffFrequency = Mathf.Clamp(cutoffFrequency, 10f, 22000f);
            effect.targetOpacity = effect.maximumOpacity;
            effect.veilColor = veilColor;
            effect.shimmerColor = shimmerColor;
            effect.distortionStrength = Mathf.Clamp(distortion, 0f, 0.06f);
            effect.vignetteStrength = Mathf.Clamp(vignette, 0f, 3f);
            effect.ApplyAppearanceGlobals();
            effect.BeginAudioMuffle();
        }

        public static void Exit(PhaseThroughVolume source)
        {
            if (instance == null)
            {
                return;
            }

            instance.activeVolumes.Remove(source);
            if (instance.activeVolumes.Count == 0)
            {
                instance.targetOpacity = 0f;
            }
        }

        private static PhasePassageScreenEffect GetOrCreate()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject root = new GameObject("Phase Passage Screen Effect")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            instance = root.AddComponent<PhasePassageScreenEffect>();
            instance.ApplyAppearanceGlobals();
            Shader.SetGlobalFloat(OpacityId, 0f);
            return instance;
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);
            currentOpacity = Mathf.MoveTowards(currentOpacity, targetOpacity, step);
            Shader.SetGlobalFloat(OpacityId, currentOpacity);
            UpdateAudioMuffle(step);
        }

        private void ApplyAppearanceGlobals()
        {
            Shader.SetGlobalColor(VeilColorId, veilColor);
            Shader.SetGlobalColor(ShimmerColorId, shimmerColor);
            Shader.SetGlobalFloat(DistortionId, distortionStrength);
            Shader.SetGlobalFloat(VignetteId, vignetteStrength);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            float cameraOpacity = renderingCamera != null && renderingCamera.cameraType == CameraType.Game
                ? currentOpacity
                : 0f;
            Shader.SetGlobalFloat(OpacityId, cameraOpacity);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            Shader.SetGlobalFloat(OpacityId, currentOpacity);
        }

        private void BeginAudioMuffle()
        {
            if (lowPassFilter != null)
            {
                lowPassFilter.enabled = true;
                return;
            }

            AudioListener listener = FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                return;
            }

            lowPassFilter = listener.GetComponent<AudioLowPassFilter>();
            if (lowPassFilter == null)
            {
                lowPassFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
                createdLowPassFilter = true;
                previousFilterEnabled = false;
                previousCutoffFrequency = 22000f;
            }
            else
            {
                createdLowPassFilter = false;
                previousFilterEnabled = lowPassFilter.enabled;
                previousCutoffFrequency = lowPassFilter.cutoffFrequency;
            }

            lowPassFilter.enabled = true;
        }

        private void UpdateAudioMuffle(float normalizedStep)
        {
            if (lowPassFilter == null)
            {
                return;
            }

            float desiredCutoff = activeVolumes.Count > 0
                ? targetCutoffFrequency
                : previousCutoffFrequency;
            lowPassFilter.cutoffFrequency = Mathf.MoveTowards(
                lowPassFilter.cutoffFrequency,
                desiredCutoff,
                22000f * normalizedStep);

            if (activeVolumes.Count == 0 && currentOpacity <= 0.001f &&
                Mathf.Abs(lowPassFilter.cutoffFrequency - previousCutoffFrequency) < 1f)
            {
                lowPassFilter.cutoffFrequency = previousCutoffFrequency;
                lowPassFilter.enabled = createdLowPassFilter ? false : previousFilterEnabled;
                lowPassFilter = null;
            }
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(OpacityId, 0f);
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
