using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AnEchoHasNoShape.Interaction;

namespace AnEchoHasNoShape.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DeathRespawnTrigger : MonoBehaviour
    {
        private static readonly int WaterEntryOriginId = Shader.PropertyToID("_WaterEntryOrigin");
        private static readonly int WaterEntryStartTimeId = Shader.PropertyToID("_WaterEntryStartTime");
        private static readonly int WaterEntryActiveId = Shader.PropertyToID("_WaterEntryActive");

        [Header("Water Entry")]
        [SerializeField, Min(0f)] private float submergeDuration = 1.1f;
        [Tooltip("Initial downward speed immediately after entering the water.")]
        [SerializeField, Min(0f)] private float sinkSpeed = 3.8f;
        [Tooltip("Downward speed approached once the player is deeper underwater.")]
        [SerializeField, Min(0f)] private float deepSinkSpeed = 0.65f;
        [Tooltip("Depth over which water resistance slows the descent.")]
        [SerializeField, Min(0.01f)] private float sinkSlowdownDepth = 3.2f;
        [Tooltip("How quickly water absorbs existing downward momentum. Independent of sink speed.")]
        [SerializeField, Min(0.01f)] private float verticalWaterResistance = 7.5f;
        [SerializeField, Range(0f, 1f)] private float horizontalDamping = 0.9f;
        [SerializeField] private AudioClip waterEntryClip;
        [SerializeField, Range(0f, 1f)] private float splashVolume = 0.72f;
        [SerializeField] private Color underwaterColor = new Color(0.025f, 0.18f, 0.23f, 1f);
        [SerializeField] private Color underwaterShimmer = new Color(0.22f, 0.78f, 0.88f, 1f);

        [Header("Drowning Camera")]
        [SerializeField, Range(0f, 85f)] private float lookUpAngle = 60f;
        [SerializeField, Min(0.01f)] private float lookUpDuration = 0.58f;
        [SerializeField, Range(0f, 8f)] private float cameraShakeAngle = 1.65f;
        [SerializeField, Min(0f)] private float cameraShakeFrequency = 8.5f;
        [SerializeField, Range(0f, 1f)] private float underwaterDimming = 0.52f;
        [SerializeField, Min(0.01f)] private float underwaterEffectFadeIn = 0.34f;

        [Header("Fade")]
        [SerializeField, Min(0.01f)] private float fadeToBlackDuration = 0.55f;
        [SerializeField, Min(0f)] private float blackHoldDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float fadeFromBlackDuration = 0.7f;

        private Canvas fadeCanvas;
        private CanvasGroup fadeGroup;
        private bool isRespawning;
        private bool listenerVolumeOverrideActive;
        private float listenerVolumeBeforeEntry = 1f;
        private Rigidbody submergedBody;
        private FirstPersonController submergedPlayer;
        private bool previousUseGravity;
        private float previousFallGravityMultiplier = 1f;
        private static AudioClip generatedSplashClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWaterEntryShaderState()
        {
            Shader.SetGlobalFloat(WaterEntryActiveId, 0f);
            generatedSplashClip = null;
        }

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;

            CreateFadeOverlay();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isRespawning)
            {
                return;
            }

            FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
            if (player != null)
            {
                StartCoroutine(RespawnRoutine(player));
            }
        }

        private IEnumerator RespawnRoutine(FirstPersonController player)
        {
            isRespawning = true;
            bool previousMovementState = player.playerCanMove;
            bool previousCameraState = player.cameraCanMove;

            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            Transform respawnPoint = gameManager != null
                ? gameManager.GetCurrentCheckpointTransform()
                : FindFallbackRespawnPoint();

            if (respawnPoint == null)
            {
                Debug.LogError("DeathBox could not find the current checkpoint.", this);
                isRespawning = false;
                yield break;
            }

            player.playerCanMove = false;
            player.cameraCanMove = false;
            listenerVolumeBeforeEntry = AudioListener.volume;
            listenerVolumeOverrideActive = true;
            BeginUnderwaterPhysics(player);

            BeginWaterEntry(player);
            yield return CarryPlayerUnderwater(player);

            yield return Fade(
                0f,
                1f,
                fadeToBlackDuration,
                AudioListener.volume,
                0f);

            EndWaterEntry();
            StopPlayerMotion(player);

            player.TeleportTo(respawnPoint);
            Physics.SyncTransforms();
            RestoreUnderwaterPhysics();

            if (blackHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(blackHoldDuration);
            }

            yield return Fade(
                1f,
                0f,
                fadeFromBlackDuration,
                0f,
                listenerVolumeBeforeEntry);

            AudioListener.volume = listenerVolumeBeforeEntry;
            listenerVolumeOverrideActive = false;

            player.playerCanMove = previousMovementState;
            player.cameraCanMove = previousCameraState;
            isRespawning = false;
        }

        private void BeginWaterEntry(FirstPersonController player)
        {
            Vector3 impactPosition = player.transform.position;
            Renderer waterRenderer = FindWaterRenderer();
            if (waterRenderer != null)
            {
                impactPosition.y = waterRenderer.bounds.center.y;
            }

            Shader.SetGlobalVector(WaterEntryOriginId, impactPosition);
            Shader.SetGlobalFloat(WaterEntryStartTimeId, Time.time);
            Shader.SetGlobalFloat(WaterEntryActiveId, 1f);

            PhasePassageScreenEffect.Enter(
                this,
                underwaterColor,
                underwaterShimmer,
                0.9f,
                underwaterEffectFadeIn,
                0.045f,
                1.85f,
                620f,
                underwaterDimming);

            if (splashVolume > 0.001f)
            {
                AudioClip clip = waterEntryClip != null
                    ? waterEntryClip
                    : GetOrCreateSplashClip();
                AudioSource.PlayClipAtPoint(clip, impactPosition, splashVolume);
            }
        }

        private IEnumerator CarryPlayerUnderwater(FirstPersonController player)
        {
            Rigidbody body = player.GetComponent<Rigidbody>();
            float elapsed = 0f;
            float entryY = body != null ? body.position.y : player.transform.position.y;
            float audioStartVolume = AudioListener.volume;
            float muffledEndVolume = audioStartVolume * 0.16f;
            Transform cameraTransform = player.playerCamera != null
                ? player.playerCamera.transform
                : null;
            float initialPitch = cameraTransform != null
                ? NormalizeAngle(cameraTransform.localEulerAngles.x)
                : 0f;

            while (elapsed < submergeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(submergeDuration, 0.001f));
                AudioListener.volume = Mathf.SmoothStep(
                    audioStartVolume,
                    muffledEndVolume,
                    progress);

                if (cameraTransform != null)
                {
                    float lookProgress = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01(elapsed / lookUpDuration));
                    float basePitch = Mathf.Lerp(initialPitch, -lookUpAngle, lookProgress);
                    float shakeEnvelope = Mathf.Sin(progress * Mathf.PI);
                    float shakeTime = elapsed * cameraShakeFrequency;
                    float pitchShake = Mathf.Sin(shakeTime * 1.37f) * cameraShakeAngle;
                    float yawShake = Mathf.Sin(shakeTime * 0.91f + 1.8f) * cameraShakeAngle * 0.62f;
                    float rollShake = Mathf.Sin(shakeTime * 1.71f + 0.6f) * cameraShakeAngle * 0.48f;
                    cameraTransform.localRotation = Quaternion.Euler(
                        basePitch + pitchShake * shakeEnvelope,
                        yawShake * shakeEnvelope,
                        rollShake * shakeEnvelope);
                }

                if (body != null)
                {
                    Vector3 velocity = body.linearVelocity;
                    float damping = 1f - Mathf.Pow(
                        1f - horizontalDamping,
                        Time.unscaledDeltaTime * 6f);
                    velocity.x = Mathf.Lerp(velocity.x, 0f, damping);
                    velocity.z = Mathf.Lerp(velocity.z, 0f, damping);
                    float depth = Mathf.Max(0f, entryY - body.position.y);
                    float depthProgress = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01(depth / sinkSlowdownDepth));
                    float targetSinkSpeed = Mathf.Lerp(
                        sinkSpeed,
                        deepSinkSpeed,
                        depthProgress);
                    float verticalBlend = 1f - Mathf.Exp(
                        -verticalWaterResistance * Time.unscaledDeltaTime);
                    velocity.y = Mathf.Lerp(
                        velocity.y,
                        -targetSinkSpeed,
                        verticalBlend);
                    body.linearVelocity = velocity;
                }

                yield return null;
            }
        }

        private void BeginUnderwaterPhysics(FirstPersonController player)
        {
            submergedPlayer = player;
            submergedBody = player.GetComponent<Rigidbody>();
            previousFallGravityMultiplier = player.fallGravityMultiplier;
            player.fallGravityMultiplier = 1f;

            if (submergedBody != null)
            {
                previousUseGravity = submergedBody.useGravity;
                submergedBody.useGravity = false;
            }
        }

        private void RestoreUnderwaterPhysics()
        {
            if (submergedPlayer != null)
            {
                submergedPlayer.fallGravityMultiplier = previousFallGravityMultiplier;
            }

            if (submergedBody != null)
            {
                submergedBody.useGravity = previousUseGravity;
            }

            submergedPlayer = null;
            submergedBody = null;
        }

        private void EndWaterEntry()
        {
            Shader.SetGlobalFloat(WaterEntryActiveId, 0f);
            PhasePassageScreenEffect.Exit(this);
        }

        private static Renderer FindWaterRenderer()
        {
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            foreach (Renderer candidate in renderers)
            {
                if (candidate != null &&
                    candidate.gameObject.name.ToLowerInvariant().Contains("water"))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static AudioClip GetOrCreateSplashClip()
        {
            if (generatedSplashClip != null)
            {
                return generatedSplashClip;
            }

            const int sampleRate = 22050;
            const float duration = 0.48f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            uint noiseState = 0x6D2B79F5u;
            float filteredNoise = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = ((noiseState >> 8) / 8388607.5f) - 1f;
                filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.075f);
                float envelope = Mathf.Exp(-time * 7.5f);
                float lowSplash = Mathf.Sin(time * Mathf.PI * 2f * (74f - time * 38f));
                float bubble = Mathf.Sin(time * Mathf.PI * 2f * 145f) * Mathf.Exp(-time * 12f);
                samples[i] = (filteredNoise * 0.72f + lowSplash * 0.34f + bubble * 0.12f)
                    * envelope
                    * 0.85f;
            }

            generatedSplashClip = AudioClip.Create(
                "Procedural Water Entry",
                sampleCount,
                1,
                sampleRate,
                false);
            generatedSplashClip.SetData(samples, 0);
            return generatedSplashClip;
        }

        private static Transform FindFallbackRespawnPoint()
        {
            GameObject fallback = GameObject.Find("GlacierRespawnStart");
            if (fallback == null)
            {
                fallback = GameObject.Find("GlacierRespawn");
            }

            return fallback != null ? fallback.transform : null;
        }

        private IEnumerator Fade(
            float from,
            float to,
            float duration,
            float audioFrom,
            float audioTo)
        {
            fadeCanvas.gameObject.SetActive(true);
            fadeGroup.alpha = from;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                fadeGroup.alpha = Mathf.SmoothStep(from, to, progress);
                AudioListener.volume = Mathf.SmoothStep(audioFrom, audioTo, progress);
                yield return null;
            }

            fadeGroup.alpha = to;
            AudioListener.volume = audioTo;
            if (to <= 0f)
            {
                fadeCanvas.gameObject.SetActive(false);
            }
        }

        private void CreateFadeOverlay()
        {
            GameObject canvasObject = new GameObject("Respawn Fade (Runtime)")
            {
                hideFlags = HideFlags.DontSave
            };

            fadeCanvas = canvasObject.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            fadeGroup = canvasObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.interactable = false;
            fadeGroup.blocksRaycasts = false;

            GameObject imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            imageObject.GetComponent<Image>().color = Color.black;

            canvasObject.SetActive(false);
        }

        private static void StopPlayerMotion(FirstPersonController player)
        {
            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void OnValidate()
        {
            fadeToBlackDuration = Mathf.Max(0.01f, fadeToBlackDuration);
            blackHoldDuration = Mathf.Max(0f, blackHoldDuration);
            fadeFromBlackDuration = Mathf.Max(0.01f, fadeFromBlackDuration);
            submergeDuration = Mathf.Max(0f, submergeDuration);
            sinkSpeed = Mathf.Max(0f, sinkSpeed);
            deepSinkSpeed = Mathf.Max(0f, deepSinkSpeed);
            sinkSlowdownDepth = Mathf.Max(0.01f, sinkSlowdownDepth);
            verticalWaterResistance = Mathf.Max(0.01f, verticalWaterResistance);
            horizontalDamping = Mathf.Clamp01(horizontalDamping);
            splashVolume = Mathf.Clamp01(splashVolume);
            lookUpAngle = Mathf.Clamp(lookUpAngle, 0f, 85f);
            lookUpDuration = Mathf.Max(0.01f, lookUpDuration);
            cameraShakeAngle = Mathf.Clamp(cameraShakeAngle, 0f, 8f);
            cameraShakeFrequency = Mathf.Max(0f, cameraShakeFrequency);
            underwaterDimming = Mathf.Clamp01(underwaterDimming);
            underwaterEffectFadeIn = Mathf.Max(0.01f, underwaterEffectFadeIn);

            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private void OnDisable()
        {
            if (listenerVolumeOverrideActive)
            {
                AudioListener.volume = listenerVolumeBeforeEntry;
                listenerVolumeOverrideActive = false;
            }

            if (isRespawning)
            {
                EndWaterEntry();
            }

            RestoreUnderwaterPhysics();
        }
    }
}
