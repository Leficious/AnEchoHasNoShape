using UnityEngine;

namespace AnEchoHasNoShape.Core
{
    /// <summary>
    /// Owns the game's always-present ambience. It survives scene changes and is
    /// duplicate-safe when runtime systems are rebuilt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalAmbiencePlayer : MonoBehaviour
    {
        private static GlobalAmbiencePlayer instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (instance != null)
            {
                return;
            }

            GameObject ambienceObject = new GameObject("Global Ambience");
            DontDestroyOnLoad(ambienceObject);
            instance = ambienceObject.AddComponent<GlobalAmbiencePlayer>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            GameAudioLibrary library = GameAudioLibrary.Load();
            if (library == null || library.AmbienceClip == null)
            {
                Debug.LogWarning("Global ambience could not load RuntimeAudioLibrary/Ambience.", this);
                enabled = false;
                return;
            }

            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = library.AmbienceVolume;
            source.clip = library.AmbienceClip;
            source.Play();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
