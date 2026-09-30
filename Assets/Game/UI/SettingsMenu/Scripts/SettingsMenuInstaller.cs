using UnityEngine;

namespace AnEchoHasNoShape.UI
{
    public static class SettingsMenuInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (Object.FindAnyObjectByType<SettingsMenuController>() != null)
            {
                return;
            }

            GameObject menuObject = new GameObject("Settings Menu Controller");
            menuObject.AddComponent<SettingsMenuController>();
        }
    }
}
