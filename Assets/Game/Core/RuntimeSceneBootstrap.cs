using System.Collections;
using AnEchoHasNoShape.Echolocation;
using AnEchoHasNoShape.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnEchoHasNoShape
{
    /// <summary>
    /// Rebuilds runtime-only systems after every scene load, including Restart.
    /// All installers are duplicate-safe, so this also covers the first load.
    /// </summary>
    public static class RuntimeSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EchoPulseController.ResetSharedPulseChannel();
            InstallRuntimeSystems();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartCoroutine(InstallAfterSceneStart());
            }
        }

        private static IEnumerator InstallAfterSceneStart()
        {
            yield return null;
            InstallRuntimeSystems();
            GameManager.Instance?.ApplyDevelopmentStartState();
        }

        private static void InstallRuntimeSystems()
        {
            InteractionInstaller.Install();
            EchoPrototypeInstaller.Install();
            WorldReverberationInstaller.Install();
        }
    }
}
