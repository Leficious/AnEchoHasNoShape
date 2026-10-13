using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    [AddComponentMenu("An Echo Has No Shape/Echolocation/World Reverberation Controller")]
    [DisallowMultipleComponent]
    public sealed class WorldReverberationController : MonoBehaviour
    {
        private static readonly int OriginId = Shader.PropertyToID("_WorldReverbOrigin");
        private static readonly int RadiusId = Shader.PropertyToID("_WorldReverbRadius");
        private static readonly int WidthId = Shader.PropertyToID("_WorldReverbWidth");
        private static readonly int TrailLengthId = Shader.PropertyToID("_WorldReverbTrailLength");
        private static readonly int IntensityId = Shader.PropertyToID("_WorldReverbIntensity");
        private static readonly int ColorId = Shader.PropertyToID("_WorldReverbColor");
        private static readonly int ActiveId = Shader.PropertyToID("_WorldReverbActive");
        private static readonly int RangeId = Shader.PropertyToID("_WorldReverbRange");
        private static readonly int FadeDistanceId = Shader.PropertyToID("_WorldReverbFadeDistance");
        private static readonly int FogRevealStrengthId = Shader.PropertyToID("_WorldReverbFogRevealStrength");

        public static WorldReverberationController Instance { get; private set; }

        [Header("World Pulse")]
        [Tooltip("Maximum radius of the world-scale reverberation.")]
        [SerializeField, Min(1f)] private float range = 450f;

        [Tooltip("Expansion speed. This does not affect the player's normal echo.")]
        [SerializeField, Min(0.1f)] private float speed = 52f;

        [SerializeField, Min(0.1f)] private float bandWidth = 7f;
        [SerializeField, Min(0.1f)] private float trailLength = 24f;
        [SerializeField, Min(0.1f)] private float fadeOutDistance = 70f;
        [SerializeField, Min(0f)] private float brightness = 4.5f;
        [SerializeField, ColorUsage(true, true)] private Color reverberationColor = new Color(1f, 0.66f, 0.2f, 1f);
        [SerializeField, Range(0f, 1f)] private float fogRevealStrength = 0.85f;

        [Header("Global Echo Audio")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Optional sound played once when a global echo begins. Leave empty for silence.")]
        [UnityEngine.Serialization.FormerlySerializedAs("reverberationClip")]
        [SerializeField] private AudioClip globalEchoSound;
        [SerializeField, Range(0f, 1f)] private float globalEchoVolume = 1f;

        [Header("After All Three Regions")]
        [SerializeField, Min(0.1f)] private float shortPulseDuration = 3f;
        [Tooltip("Seconds between short pulse starts, not the gap after a pulse ends.")]
        [SerializeField] private Vector2 shortPulseInterval = new Vector2(6f, 7f);
        [Tooltip("Optional short echo sound. The long global echo sound is not repeated.")]
        [SerializeField] private AudioClip shortEchoSound;

        private float radius;
        private bool isActive;
        private float activeSpeed;
        private bool repeating;
        private bool endingOwnsPulses;
        private float nextShortPulseAt = float.PositiveInfinity;

        public bool IsActive => isActive;
        public bool HasLingeringPulse => isActive || (audioSource != null && audioSource.isPlaying);
        public Color ReverberationColor => reverberationColor;
        public float PulseSpeed => speed;
        public float PulseDuration => (range + fadeOutDistance) / Mathf.Max(0.1f, speed);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("More than one WorldReverberationController exists.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                    audioSource.loop = false;
                }
            }
        }

        private void OnEnable()
        {
            PublishInactive();
        }

        private void Update()
        {
            bool completed = !endingOwnsPulses && GameManager.Instance != null && GameManager.Instance.AllRegionsCompleted;
            if (!completed)
            {
                repeating = false;
                nextShortPulseAt = float.PositiveInfinity;
            }
            else if (!repeating && !isActive)
            {
                // The last full-length completion echo must finish before repetition starts.
                repeating = true;
                nextShortPulseAt = Time.time;
            }

            if (repeating && !isActive && Time.time >= nextShortPulseAt)
            {
                TriggerShortReverberation(shortPulseDuration);
                nextShortPulseAt = Time.time + Random.Range(shortPulseInterval.x, shortPulseInterval.y);
            }

            if (!isActive)
            {
                return;
            }

            radius += activeSpeed * Time.deltaTime;
            Publish();

            if (radius > range + fadeOutDistance)
            {
                isActive = false;
                PublishInactive();
            }
        }

        [ContextMenu("Trigger World Reverberation")]
        public void TriggerReverberation()
        {
            if (endingOwnsPulses) return;
            repeating = false;
            nextShortPulseAt = float.PositiveInfinity;
            activeSpeed = speed;
            radius = 0f;
            isActive = true;
            Publish();

            GameManager.Instance?.PlayEchoCameraRumble(PulseDuration);

            if (audioSource != null && globalEchoSound != null)
            {
                audioSource.PlayOneShot(globalEchoSound, globalEchoVolume);
            }
        }

        public void StopReverberation()
        {
            isActive = false;
            radius = 0f;
            PublishInactive();
            if (audioSource != null) audioSource.Stop();
        }

        public void BeginEndingSequence()
        {
            endingOwnsPulses = true;
            repeating = false;
            nextShortPulseAt = float.PositiveInfinity;
            // Reserve future pulses for the ending; let the current wave and sound finish.
        }

        public void SetEndingAudioFade(float volume)
        {
            if (endingOwnsPulses && audioSource != null) audioSource.volume = Mathf.Clamp01(volume);
        }

        public void TriggerShortReverberation(float duration, float shakeStrength = 1f)
        {
            radius = 0f;
            activeSpeed = (range + fadeOutDistance) / Mathf.Max(0.1f, duration);
            isActive = true;
            Publish();
            GameManager.Instance?.PlayEchoCameraRumble(duration, shakeStrength);
            if (audioSource != null && shortEchoSound != null)
                audioSource.PlayOneShot(shortEchoSound, globalEchoVolume);
        }

        private void Publish()
        {
            Shader.SetGlobalVector(OriginId, transform.position);
            Shader.SetGlobalFloat(RadiusId, radius);
            Shader.SetGlobalFloat("_WorldReverbSpeed", activeSpeed);
            Shader.SetGlobalFloat(WidthId, bandWidth);
            Shader.SetGlobalFloat(TrailLengthId, trailLength);
            Shader.SetGlobalFloat(IntensityId, brightness);
            Shader.SetGlobalColor(ColorId, reverberationColor);
            Shader.SetGlobalFloat(RangeId, range);
            Shader.SetGlobalFloat(FadeDistanceId, fadeOutDistance);
            Shader.SetGlobalFloat(FogRevealStrengthId, fogRevealStrength);
            Shader.SetGlobalFloat(ActiveId, 1f);
        }

        private static void PublishInactive()
        {
            Shader.SetGlobalFloat(ActiveId, 0f);
        }

        private void OnDisable()
        {
            isActive = false;
            repeating = false;
            nextShortPulseAt = float.PositiveInfinity;
            PublishInactive();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                PublishInactive();
                Instance = null;
            }
        }

        private void OnValidate()
        {
            range = Mathf.Max(1f, range);
            speed = Mathf.Max(0.1f, speed);
            bandWidth = Mathf.Max(0.1f, bandWidth);
            trailLength = Mathf.Max(0.1f, trailLength);
            fadeOutDistance = Mathf.Max(0.1f, fadeOutDistance);
            brightness = Mathf.Max(0f, brightness);
            fogRevealStrength = Mathf.Clamp01(fogRevealStrength);
            shortPulseDuration = Mathf.Max(0.1f, shortPulseDuration);
            shortPulseInterval.x = Mathf.Max(shortPulseDuration, shortPulseInterval.x);
            shortPulseInterval.y = Mathf.Max(shortPulseInterval.x, shortPulseInterval.y);
        }
    }
}
