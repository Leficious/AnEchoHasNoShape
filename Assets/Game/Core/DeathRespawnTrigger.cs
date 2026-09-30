using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AnEchoHasNoShape.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DeathRespawnTrigger : MonoBehaviour
    {
        [SerializeField] private Transform respawnPoint;

        [Header("Fade")]
        [SerializeField, Min(0.01f)] private float fadeToBlackDuration = 0.55f;
        [SerializeField, Min(0f)] private float blackHoldDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float fadeFromBlackDuration = 0.7f;

        private Canvas fadeCanvas;
        private CanvasGroup fadeGroup;
        private bool isRespawning;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;

            if (respawnPoint == null)
            {
                GameObject respawnObject = GameObject.Find("GlacierRespawn");
                respawnPoint = respawnObject != null ? respawnObject.transform : null;
            }

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
            if (respawnPoint == null)
            {
                Debug.LogError("DeathBox could not find GlacierRespawn.", this);
                yield break;
            }

            isRespawning = true;
            bool previousMovementState = player.playerCanMove;
            bool previousCameraState = player.cameraCanMove;
            player.playerCanMove = false;
            player.cameraCanMove = false;
            StopPlayerMotion(player);

            yield return Fade(0f, 1f, fadeToBlackDuration);

            player.TeleportTo(respawnPoint);
            Physics.SyncTransforms();

            if (blackHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(blackHoldDuration);
            }

            yield return Fade(1f, 0f, fadeFromBlackDuration);

            player.playerCanMove = previousMovementState;
            player.cameraCanMove = previousCameraState;
            isRespawning = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            fadeCanvas.gameObject.SetActive(true);
            fadeGroup.alpha = from;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                fadeGroup.alpha = Mathf.SmoothStep(from, to, progress);
                yield return null;
            }

            fadeGroup.alpha = to;
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

            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }
    }
}
