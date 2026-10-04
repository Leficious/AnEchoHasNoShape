using UnityEngine;

namespace AnEchoHasNoShape.Core
{
    [CreateAssetMenu(menuName = "An Echo Has No Shape/Runtime Audio Library")]
    public sealed class GameAudioLibrary : ScriptableObject
    {
        private const string ResourcePath = "RuntimeAudioLibrary";
        private static GameAudioLibrary cached;

        [Header("Player Echo")]
        [SerializeField] private AudioClip echoClip;
        [SerializeField, Range(0f, 1f)] private float echoVolume = .78f;

        [Header("Global Ambience")]
        [SerializeField] private AudioClip ambienceClip;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = .34f;

        [Header("Water Ambience")]
        [SerializeField] private AudioClip wavesClip;
        [SerializeField, Range(0f, 1f)] private float wavesVolume = .42f;
        [Tooltip("Distance from the water surface at which the waves reach full volume.")]
        [SerializeField, Min(0f)] private float wavesFullVolumeDistance = 2f;
        [Tooltip("Distance from the water surface at which the waves become silent.")]
        [SerializeField, Min(.1f)] private float wavesFadeOutDistance = 12f;
        [Tooltip("How far into the outer-to-inner glacier transition the player must be before waves begin.")]
        [SerializeField, Range(0f, .95f)] private float wavesGlacierBlendStart = .3f;
        [SerializeField, Min(.05f)] private float wavesFadeSeconds = 1.5f;

        public AudioClip EchoClip => echoClip;
        public float EchoVolume => echoVolume;
        public AudioClip AmbienceClip => ambienceClip;
        public float AmbienceVolume => ambienceVolume;
        public AudioClip WavesClip => wavesClip;
        public float WavesVolume => wavesVolume;
        public float WavesFullVolumeDistance => wavesFullVolumeDistance;
        public float WavesFadeOutDistance => wavesFadeOutDistance;
        public float WavesGlacierBlendStart => wavesGlacierBlendStart;
        public float WavesFadeSeconds => wavesFadeSeconds;

        public static GameAudioLibrary Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<GameAudioLibrary>(ResourcePath);
            }

            return cached;
        }

        private void OnValidate()
        {
            wavesFullVolumeDistance = Mathf.Max(0f, wavesFullVolumeDistance);
            wavesFadeOutDistance = Mathf.Max(wavesFullVolumeDistance + .1f, wavesFadeOutDistance);
            wavesGlacierBlendStart = Mathf.Clamp(wavesGlacierBlendStart, 0f, .95f);
            wavesFadeSeconds = Mathf.Max(.05f, wavesFadeSeconds);
        }
    }
}
