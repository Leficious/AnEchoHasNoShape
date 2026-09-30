using UnityEngine;
using AnEchoHasNoShape.Interaction;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    public sealed class EchoPulseController : MonoBehaviour
    {
        private static readonly int PulseOriginId = Shader.PropertyToID("_EchoPulseOrigin");
        private static readonly int PulseRadiusId = Shader.PropertyToID("_EchoPulseRadius");
        private static readonly int PulseWidthId = Shader.PropertyToID("_EchoPulseWidth");
        private static readonly int TrailLengthId = Shader.PropertyToID("_EchoTrailLength");
        private static readonly int PulseIntensityId = Shader.PropertyToID("_EchoPulseIntensity");
        private static readonly int PulseColorId = Shader.PropertyToID("_EchoPulseColor");
        private static readonly int PulseActiveId = Shader.PropertyToID("_EchoPulseActive");
        private static readonly int PulseCountId = Shader.PropertyToID("_EchoPulseCount");
        private static readonly int PulseSpacingId = Shader.PropertyToID("_EchoPulseSpacing");
        private static readonly int PulseRangeId = Shader.PropertyToID("_EchoPulseRange");
        private static readonly int FadeOutDistanceId = Shader.PropertyToID("_EchoFadeOutDistance");
        private static readonly int FogRevealStrengthId = Shader.PropertyToID("_EchoFogRevealStrength");
        private static readonly int WireframeStrengthId = Shader.PropertyToID("_EchoWireframeStrength");
        private static readonly int WireframeScaleId = Shader.PropertyToID("_EchoWireframeScale");
        private static readonly int WireframeThicknessId = Shader.PropertyToID("_EchoWireframeThickness");
        private static readonly int SirenOriginId = Shader.PropertyToID("_SirenEchoPulseOrigin");
        private static readonly int SirenRadiusId = Shader.PropertyToID("_SirenEchoPulseRadius");
        private static readonly int SirenWidthId = Shader.PropertyToID("_SirenEchoPulseWidth");
        private static readonly int SirenTrailLengthId = Shader.PropertyToID("_SirenEchoTrailLength");
        private static readonly int SirenIntensityId = Shader.PropertyToID("_SirenEchoPulseIntensity");
        private static readonly int SirenColorId = Shader.PropertyToID("_SirenEchoPulseColor");
        private static readonly int SirenActiveId = Shader.PropertyToID("_SirenEchoPulseActive");
        private static readonly int SirenCountId = Shader.PropertyToID("_SirenEchoPulseCount");
        private static readonly int SirenSpacingId = Shader.PropertyToID("_SirenEchoPulseSpacing");
        private static readonly int SirenRangeId = Shader.PropertyToID("_SirenEchoPulseRange");
        private static readonly int SirenFadeDistanceId = Shader.PropertyToID("_SirenEchoFadeOutDistance");
        private static readonly int SirenFogRevealId = Shader.PropertyToID("_SirenEchoFogRevealStrength");

        [Header("Input")]
        [SerializeField] private KeyCode echoKey = KeyCode.E;
        [SerializeField, Min(0f)] private float cooldown = 5f;

        [Header("Pulse")]
        [SerializeField, Min(0.1f)] private float range = 50f;
        [SerializeField, Min(0.1f)] private float speed = 14f;
        [SerializeField, Range(1, 3)] private int pulseCount = 2;
        [SerializeField, Min(0.1f)] private float pulseSpacing = 5f;
        [SerializeField, Min(0.01f)] private float bandWidth = 1.25f;
        [SerializeField, Min(0.01f)] private float trailLength = 2.5f;
        [SerializeField, Min(0.1f)] private float fadeOutDistance = 10f;
        [SerializeField, Min(0f)] private float brightness = 3.5f;
        [SerializeField] private Color echoColor = new Color(0.72f, 0.86f, 0.92f, 1f);
        [Tooltip("How strongly an active echo ring parts volumetric fog at revealed surfaces.")]
        [SerializeField, Range(0f, 1f)] private float fogRevealStrength = 0.9f;

        [Header("Surface Wireframe")]
        [Tooltip("Brightness of the faint triangulated construction lines inside the echo.")]
        [SerializeField, Range(0f, 1f)] private float wireframeStrength = 0.28f;

        [Tooltip("World-space frequency of the wire pattern. Higher values make smaller cells.")]
        [SerializeField, Range(0.1f, 3f)] private float wireframeScale = 0.7f;

        [Tooltip("Screen-space thickness of the wire lines.")]
        [SerializeField, Range(0.25f, 3f)] private float wireframeThickness = 0.85f;

        [SerializeField] private Transform emissionOrigin;
        [SerializeField] private float fallbackOriginHeight = 0.75f;

        [Header("Optional Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip echoClip;

        private bool pulseIsActive;
        private float pulseRadius;
        private float nextAvailableTime;
        private Vector3 pulseOrigin;
        private int pulseToken = -1;

        private static int currentPulseToken;
        private static int nextPulseToken;

        public static bool IsPulseActive { get; private set; }
        public static Vector3 ActivePulseOrigin { get; private set; }
        public static float ActivePulseRadius { get; private set; }
        public static float ActivePulseRange { get; private set; }
        public static int ActivePulseSequence { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetSharedPulseChannel()
        {
            currentPulseToken = 0;
            nextPulseToken = 0;
            IsPulseActive = false;
            ActivePulseOrigin = Vector3.zero;
            ActivePulseRadius = 0f;
            ActivePulseRange = 0f;
            ActivePulseSequence = 0;
            Shader.SetGlobalFloat(PulseActiveId, 0f);
            Shader.SetGlobalFloat(SirenActiveId, 0f);
        }

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void OnEnable()
        {
            pulseIsActive = false;
            pulseToken = -1;
        }

        private void Update()
        {
            bool inputIsAvailable = Time.timeScale > 0f &&
                                    (InteractionTextPanel.Instance == null || !InteractionTextPanel.Instance.IsOpen);

            bool echoIsUnlocked = GameManager.Instance == null || GameManager.Instance.EchoUnlocked;

            if (inputIsAvailable && echoIsUnlocked && Input.GetKeyDown(echoKey) && Time.time >= nextAvailableTime)
            {
                TriggerPulse();
                ScreenPromptUI.DismissEchoTutorial();
            }

            if (!pulseIsActive)
            {
                return;
            }

            if (!OwnsSharedPulse(pulseToken))
            {
                pulseIsActive = false;
                return;
            }

            pulseRadius += speed * Time.deltaTime;
            PublishPulse();

            float finalRingDelay = pulseCount switch
            {
                1 => 0f,
                2 => pulseSpacing,
                _ => pulseSpacing * 2.35f
            };
            float outlineFadeDistance = fadeOutDistance * 2f;
            if (pulseRadius > range + finalRingDelay + outlineFadeDistance)
            {
                pulseIsActive = false;
                EndSharedPulse(pulseToken);
                pulseToken = -1;
            }
        }

        private void OnDisable()
        {
            EndSharedPulse(pulseToken);
            pulseToken = -1;
            pulseIsActive = false;
        }

        public void TriggerPulse()
        {
            pulseOrigin = emissionOrigin != null
                ? emissionOrigin.position
                : transform.position + Vector3.up * fallbackOriginHeight;

            pulseRadius = 0f;
            pulseIsActive = true;
            pulseToken = BeginSharedPulse(pulseOrigin, range);
            nextAvailableTime = Time.time + cooldown;
            PublishPulse();

            if (audioSource != null && echoClip != null)
            {
                audioSource.PlayOneShot(echoClip);
            }
        }

        private void PublishPulse()
        {
            PublishSharedPulse(
                pulseToken,
                pulseOrigin,
                pulseRadius,
                bandWidth,
                trailLength,
                brightness,
                echoColor,
                pulseCount,
                pulseSpacing,
                range,
                fadeOutDistance,
                fogRevealStrength,
                wireframeStrength,
                wireframeScale,
                wireframeThickness);
        }

        public static int BeginSharedPulse(Vector3 origin, float pulseRange)
        {
            currentPulseToken = ++nextPulseToken;
            IsPulseActive = true;
            ActivePulseOrigin = origin;
            ActivePulseRadius = 0f;
            ActivePulseRange = pulseRange;
            ActivePulseSequence++;
            return currentPulseToken;
        }

        public static bool OwnsSharedPulse(int token)
        {
            return token > 0 && token == currentPulseToken;
        }

        public static bool PublishSharedPulse(
            int token,
            Vector3 origin,
            float radius,
            float width,
            float trailLength,
            float intensity,
            Color color,
            int count,
            float spacing,
            float pulseRange,
            float fadeDistance,
            float pulseFogRevealStrength,
            float pulseWireframeStrength,
            float pulseWireframeScale,
            float pulseWireframeThickness)
        {
            if (!OwnsSharedPulse(token))
            {
                return false;
            }

            IsPulseActive = true;
            ActivePulseOrigin = origin;
            ActivePulseRadius = radius;
            ActivePulseRange = pulseRange;

            Shader.SetGlobalVector(PulseOriginId, origin);
            Shader.SetGlobalFloat(PulseRadiusId, radius);
            Shader.SetGlobalFloat(PulseWidthId, width);
            Shader.SetGlobalFloat(TrailLengthId, trailLength);
            Shader.SetGlobalFloat(PulseIntensityId, intensity);
            Shader.SetGlobalColor(PulseColorId, color);
            Shader.SetGlobalFloat(PulseCountId, count);
            Shader.SetGlobalFloat(PulseSpacingId, spacing);
            Shader.SetGlobalFloat(PulseRangeId, pulseRange);
            Shader.SetGlobalFloat(FadeOutDistanceId, fadeDistance);
            Shader.SetGlobalFloat(FogRevealStrengthId, pulseFogRevealStrength);
            Shader.SetGlobalFloat(WireframeStrengthId, pulseWireframeStrength);
            Shader.SetGlobalFloat(WireframeScaleId, pulseWireframeScale);
            Shader.SetGlobalFloat(WireframeThicknessId, pulseWireframeThickness);
            Shader.SetGlobalFloat(PulseActiveId, 1f);
            return true;
        }

        public static void EndSharedPulse(int token)
        {
            if (!OwnsSharedPulse(token))
            {
                return;
            }

            currentPulseToken = 0;
            IsPulseActive = false;
            Shader.SetGlobalFloat(PulseActiveId, 0f);
        }

        public static void PublishSirenPulse(
            Vector3 origin,
            float radius,
            float width,
            float sirenTrailLength,
            float intensity,
            Color color,
            int count,
            float spacing,
            float pulseRange,
            float fadeDistance,
            float pulseFogRevealStrength)
        {
            Shader.SetGlobalVector(SirenOriginId, origin);
            Shader.SetGlobalFloat(SirenRadiusId, radius);
            Shader.SetGlobalFloat(SirenWidthId, width);
            Shader.SetGlobalFloat(SirenTrailLengthId, sirenTrailLength);
            Shader.SetGlobalFloat(SirenIntensityId, intensity);
            Shader.SetGlobalColor(SirenColorId, color);
            Shader.SetGlobalFloat(SirenCountId, count);
            Shader.SetGlobalFloat(SirenSpacingId, spacing);
            Shader.SetGlobalFloat(SirenRangeId, pulseRange);
            Shader.SetGlobalFloat(SirenFadeDistanceId, fadeDistance);
            Shader.SetGlobalFloat(SirenFogRevealId, pulseFogRevealStrength);
            Shader.SetGlobalFloat(SirenActiveId, 1f);
        }

        public static void EndSirenPulse()
        {
            Shader.SetGlobalFloat(SirenActiveId, 0f);
        }

        private void OnValidate()
        {
            cooldown = Mathf.Max(0f, cooldown);
            range = Mathf.Max(0.1f, range);
            speed = Mathf.Max(0.1f, speed);
            pulseCount = Mathf.Clamp(pulseCount, 1, 3);
            pulseSpacing = Mathf.Max(0.1f, pulseSpacing);
            bandWidth = Mathf.Max(0.01f, bandWidth);
            trailLength = Mathf.Max(0.01f, trailLength);
            fadeOutDistance = Mathf.Clamp(fadeOutDistance, 0.1f, range);
            brightness = Mathf.Max(0f, brightness);
            fogRevealStrength = Mathf.Clamp01(fogRevealStrength);
            wireframeStrength = Mathf.Clamp01(wireframeStrength);
            wireframeScale = Mathf.Clamp(wireframeScale, 0.1f, 3f);
            wireframeThickness = Mathf.Clamp(wireframeThickness, 0.25f, 3f);
        }
    }
}
