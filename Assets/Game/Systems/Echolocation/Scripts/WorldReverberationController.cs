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

        [Header("Optional Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip reverberationClip;

        private float radius;
        private bool isActive;

        public bool IsActive => isActive;
        public Color ReverberationColor => reverberationColor;
        public float PulseSpeed => speed;

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
            }
        }

        private void OnEnable()
        {
            PublishInactive();
        }

        private void Update()
        {
            if (!isActive)
            {
                return;
            }

            radius += speed * Time.deltaTime;
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
            radius = 0f;
            isActive = true;
            Publish();

            if (audioSource != null && reverberationClip != null)
            {
                audioSource.PlayOneShot(reverberationClip);
            }
        }

        public void StopReverberation()
        {
            isActive = false;
            radius = 0f;
            PublishInactive();
            if (audioSource != null) audioSource.Stop();
        }

        private void Publish()
        {
            Shader.SetGlobalVector(OriginId, transform.position);
            Shader.SetGlobalFloat(RadiusId, radius);
            Shader.SetGlobalFloat("_WorldReverbSpeed", speed);
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
        }
    }
}
