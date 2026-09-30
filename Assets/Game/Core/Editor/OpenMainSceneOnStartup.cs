using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace AnEchoHasNoShape.Editor
{
    [InitializeOnLoad]
    internal static class OpenMainSceneOnStartup
    {
        private const string MainScenePath = "Assets/Game/Scenes/Main.unity";
        private const string StartupCheckSessionKey = "AnEchoHasNoShape.MainSceneStartupChecked";

        static OpenMainSceneOnStartup()
        {
            if (SessionState.GetBool(StartupCheckSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(StartupCheckSessionKey, true);
            EditorApplication.delayCall += OpenMainIfEditorStartedUntitled;
        }

        private static void OpenMainIfEditorStartedUntitled()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            bool isEmptyUntitledScene = string.IsNullOrEmpty(activeScene.path) && !activeScene.isDirty;
            if (!isEmptyUntitledScene)
            {
                return;
            }

            SceneAsset mainScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
            if (mainScene != null)
            {
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }
        }
    }
}
