using AnEchoHasNoShape.Core;
using AnEchoHasNoShape.FogLighting;
using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>
    /// Plays a non-spatial wave bed whose volume follows the listener's true
    /// distance from this water surface. Using renderer bounds avoids treating
    /// the center of a very large water plane as its only sound source.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class WaterProximityAudio : MonoBehaviour
    {
        private Renderer waterRenderer;
        private Transform listenerTransform;
        private AudioSource source;
        private float targetVolume;
        private float maxVolume;
        private float fadeSeconds = 1.5f;

        private void Awake()
        {
            waterRenderer = GetComponent<Renderer>();

            GameAudioLibrary library = GameAudioLibrary.Load();
            if (library == null || library.WavesClip == null)
            {
                enabled = false;
                Debug.LogWarning("Water proximity audio could not load RuntimeAudioLibrary/Waves.", this);
                return;
            }

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.clip = library.WavesClip;
            source.Play();

            maxVolume = library.WavesVolume;
            fadeSeconds = library.WavesFadeSeconds;
            ResolveListener();
        }

        private void Update()
        {
            if (source == null || waterRenderer == null)
            {
                return;
            }

            if (listenerTransform == null)
            {
                ResolveListener();
                targetVolume = 0f;
            }
            else
            {
                GameAudioLibrary library = GameAudioLibrary.Load();
                float distance = Mathf.Sqrt(waterRenderer.bounds.SqrDistance(listenerTransform.position));
                float proximity = 1f - Mathf.InverseLerp(
                    library.WavesFullVolumeDistance,
                    library.WavesFadeOutDistance,
                    distance);
                proximity = Mathf.SmoothStep(0f, 1f, proximity);

                // The water mesh is a very large rectangle that continues under
                // distant land. Gate its bounds-based proximity with the same
                // authored glacier transition used by fog, aurora, and snow.
                float areaGate = Mathf.InverseLerp(
                    library.WavesGlacierBlendStart,
                    1f,
                    GlacierFogBlendZone.CurrentAreaBlend);
                areaGate = Mathf.SmoothStep(0f, 1f, areaGate);
                targetVolume = maxVolume * proximity * areaGate;
            }

            float maxDelta = maxVolume * Time.unscaledDeltaTime / Mathf.Max(.05f, fadeSeconds);
            source.volume = Mathf.MoveTowards(source.volume, targetVolume, maxDelta);
        }

        private void ResolveListener()
        {
            AudioListener listener = FindAnyObjectByType<AudioListener>();
            listenerTransform = listener != null ? listener.transform : null;
        }
    }
}
