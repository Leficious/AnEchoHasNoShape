using UnityEngine;
using UnityEngine.Serialization;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SirenEchoZone : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Transform sourceA;
        [SerializeField] private Transform sourceB;
        [SerializeField] private Transform icebergRoot;

        [Header("Deceptive Iceberg Selection")]
        [Tooltip("Chance that a genuinely echoable iceberg is recolored and claimed by each siren echo.")]
        [FormerlySerializedAs("truthfulIcebergRevealChance")]
        [SerializeField, Range(0f, 1f)] private float echoableOverwriteChance = 0.35f;
        [Tooltip("Chance that a genuinely echoable iceberg has its player highlight erased with no pink replacement.")]
        [SerializeField, Range(0f, 1f)] private float echoableEraseChance = 0.4f;
        [Tooltip("Chance that a non-echoable, non-solid iceberg is falsely shown by each siren echo.")]
        [SerializeField, Range(0f, 1f)] private float falseIcebergRevealChance = 0.7f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float firstEchoDelay = 1.5f;
        [SerializeField, Min(0.1f)] private float interval = 7f;
        [SerializeField, Min(0f)] private float intervalJitter = 1f;

        [Header("Siren Pulse")]
        [SerializeField] private Color echoColor = new Color(1f, 0.18f, 0.58f, 1f);
        [SerializeField, Min(0.1f)] private float baseRange = 62f;
        [SerializeField, Min(0f)] private float rangeJitter = 8f;
        [SerializeField, Min(0.1f)] private float baseSpeed = 13f;
        [SerializeField, Min(0f)] private float speedJitter = 1.5f;
        [SerializeField, Min(0.01f)] private float baseBandWidth = 1.5f;
        [SerializeField, Min(0f)] private float bandWidthJitter = 0.35f;
        [SerializeField, Min(0f)] private float baseBrightness = 3.8f;
        [SerializeField, Min(0f)] private float brightnessJitter = 0.5f;
        [SerializeField, Range(1, 3)] private int pulseCount = 2;
        [SerializeField, Min(0.1f)] private float pulseSpacing = 5f;
        [SerializeField, Min(0.01f)] private float trailLength = 2.8f;
        [SerializeField, Min(0.1f)] private float fadeOutDistance = 12f;
        [SerializeField, Range(0f, 1f)] private float fogRevealStrength = 0.9f;
        [SerializeField, Range(0f, 1f)] private float wireframeStrength = 0.32f;
        [SerializeField, Range(0.1f, 3f)] private float wireframeScale = 0.7f;
        [SerializeField, Range(0.25f, 3f)] private float wireframeThickness = 0.9f;

        [Header("Optional Audio")]
        [SerializeField] private AudioClip sirenClip;
        [SerializeField, Range(0f, 1f)] private float sirenVolume = 1f;
        [Tooltip("Extra loudness applied to the siren clip. Values above 1 may clip if the source recording is already loud.")]
        [SerializeField, Range(0f, 3f)] private float sirenGain = 1.65f;
        [Tooltip("Distance from the active siren source at which it remains at full volume.")]
        [SerializeField, Min(0.1f)] private float sirenMinDistance = 28f;
        [Tooltip("Distance beyond which the active siren source can no longer be heard.")]
        [SerializeField, Min(1f)] private float sirenMaxDistance = 220f;

        private int playerColliderCount;
        private bool useSourceA = true;
        private float nextEchoTime;
        private bool pulseActive;
        private Vector3 pulseOrigin;
        private float pulseRadius;
        private float pulseRange;
        private float pulseSpeed;
        private float pulseWidth;
        private float pulseBrightness;

        public void Configure(Transform firstSource, Transform secondSource, Transform icebergs = null)
        {
            sourceA = firstSource;
            sourceB = secondSource;
            icebergRoot = icebergs;
        }

        private void Awake()
        {
            Collider zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;

            if (icebergRoot == null && transform.parent != null)
            {
                icebergRoot = transform.parent.Find("Icebergs");
            }
        }

        private void Update()
        {
            if (playerColliderCount <= 0)
            {
                return;
            }

            if (Time.time >= nextEchoTime)
            {
                EmitNextSirenEcho();
                nextEchoTime = Time.time + Mathf.Max(0.1f, interval + Random.Range(-intervalJitter, intervalJitter));
            }

            if (!pulseActive)
            {
                return;
            }

            pulseRadius += pulseSpeed * Time.deltaTime;
            EchoPulseController.PublishSirenPulse(
                pulseOrigin,
                pulseRadius,
                pulseWidth,
                trailLength,
                pulseBrightness,
                echoColor,
                pulseCount,
                pulseSpacing,
                pulseRange,
                fadeOutDistance,
                fogRevealStrength);

            float finalRingDelay = pulseCount switch
            {
                1 => 0f,
                2 => pulseSpacing,
                _ => pulseSpacing * 2.35f
            };

            if (pulseRadius > pulseRange + finalRingDelay + fadeOutDistance * 2f)
            {
                EchoPulseController.EndSirenPulse();
                pulseActive = false;
            }
        }

        private void EmitNextSirenEcho()
        {
            Transform source = useSourceA ? sourceA : sourceB;
            useSourceA = !useSourceA;

            if (source == null)
            {
                source = useSourceA ? sourceA : sourceB;
            }

            if (source == null)
            {
                return;
            }

            pulseOrigin = source.position;
            pulseRadius = 0f;
            pulseRange = Mathf.Max(0.1f, baseRange + Random.Range(-rangeJitter, rangeJitter));
            pulseSpeed = Mathf.Max(0.1f, baseSpeed + Random.Range(-speedJitter, speedJitter));
            pulseWidth = Mathf.Max(0.01f, baseBandWidth + Random.Range(-bandWidthJitter, bandWidthJitter));
            pulseBrightness = Mathf.Max(0f, baseBrightness + Random.Range(-brightnessJitter, brightnessJitter));
            pulseActive = true;

            RandomizeSirenIcebergSelection();

            EchoPulseController.PublishSirenPulse(
                pulseOrigin,
                pulseRadius,
                pulseWidth,
                trailLength,
                pulseBrightness,
                echoColor,
                pulseCount,
                pulseSpacing,
                pulseRange,
                fadeOutDistance,
                fogRevealStrength);

            if (sirenClip != null)
            {
                PlaySirenAudio(pulseOrigin);
            }
        }

        private void PlaySirenAudio(Vector3 position)
        {
            GameObject audioObject = new GameObject("Siren Song (Runtime)");
            audioObject.transform.position = position;

            AudioSource audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.clip = sirenClip;
            audioSource.volume = sirenVolume;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = sirenMinDistance;
            audioSource.maxDistance = Mathf.Max(sirenMinDistance + 0.1f, sirenMaxDistance);
            audioSource.dopplerLevel = 0f;
            audioSource.playOnAwake = false;
            audioSource.PlayOneShot(sirenClip, sirenGain);

            Destroy(audioObject, sirenClip.length + 0.5f);
        }

        private void RandomizeSirenIcebergSelection()
        {
            if (icebergRoot == null)
            {
                return;
            }

            IcebergStateGroup[] stateGroups = icebergRoot.GetComponentsInChildren<IcebergStateGroup>(true);
            foreach (IcebergStateGroup stateGroup in stateGroups)
            {
                // Roll once per iceberg so every mesh belonging to that iceberg
                // tells the same lie during this pulse.
                foreach (Transform iceberg in stateGroup.transform)
                {
                    bool revealForThisPulse;
                    bool subtractPlayerEcho;

                    if (stateGroup.State == IcebergEchoState.VisibleFalse)
                    {
                        revealForThisPulse = Random.value < falseIcebergRevealChance;
                        subtractPlayerEcho = revealForThisPulse;
                    }
                    else
                    {
                        float outcome = Random.value;
                        revealForThisPulse = outcome < echoableOverwriteChance;
                        subtractPlayerEcho = revealForThisPulse ||
                            outcome < echoableOverwriteChance + echoableEraseChance;
                    }

                    EchoReactiveSurface[] surfaces = iceberg.GetComponentsInChildren<EchoReactiveSurface>(true);
                    foreach (EchoReactiveSurface surface in surfaces)
                    {
                        surface.SetSirenEchoBehavior(revealForThisPulse, subtractPlayerEcho);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerColliderCount++;
            if (playerColliderCount == 1)
            {
                useSourceA = true;
                nextEchoTime = Time.time + firstEchoDelay;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            playerColliderCount = Mathf.Max(0, playerColliderCount - 1);
            if (playerColliderCount == 0)
            {
                EchoPulseController.EndSirenPulse();
                pulseActive = false;
            }
        }

        private static bool IsPlayer(Collider other)
        {
            Transform root = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform.root
                : other.transform.root;
            return root.CompareTag("Player");
        }

        private void OnDisable()
        {
            EchoPulseController.EndSirenPulse();
            pulseActive = false;
            playerColliderCount = 0;
        }

        private void OnValidate()
        {
            firstEchoDelay = Mathf.Max(0f, firstEchoDelay);
            interval = Mathf.Max(0.1f, interval);
            intervalJitter = Mathf.Clamp(intervalJitter, 0f, interval * 0.9f);
            baseRange = Mathf.Max(0.1f, baseRange);
            rangeJitter = Mathf.Max(0f, rangeJitter);
            baseSpeed = Mathf.Max(0.1f, baseSpeed);
            speedJitter = Mathf.Max(0f, speedJitter);
            baseBandWidth = Mathf.Max(0.01f, baseBandWidth);
            bandWidthJitter = Mathf.Max(0f, bandWidthJitter);
            baseBrightness = Mathf.Max(0f, baseBrightness);
            brightnessJitter = Mathf.Max(0f, brightnessJitter);
            pulseCount = Mathf.Clamp(pulseCount, 1, 3);
            pulseSpacing = Mathf.Max(0.1f, pulseSpacing);
            trailLength = Mathf.Max(0.01f, trailLength);
            fadeOutDistance = Mathf.Max(0.1f, fadeOutDistance);
            echoableOverwriteChance = Mathf.Clamp01(echoableOverwriteChance);
            echoableEraseChance = Mathf.Clamp(echoableEraseChance, 0f, 1f - echoableOverwriteChance);
            falseIcebergRevealChance = Mathf.Clamp01(falseIcebergRevealChance);
            sirenVolume = Mathf.Clamp01(sirenVolume);
            sirenGain = Mathf.Clamp(sirenGain, 0f, 3f);
            sirenMinDistance = Mathf.Max(0.1f, sirenMinDistance);
            sirenMaxDistance = Mathf.Max(sirenMinDistance + 0.1f, sirenMaxDistance);
        }
    }
}
