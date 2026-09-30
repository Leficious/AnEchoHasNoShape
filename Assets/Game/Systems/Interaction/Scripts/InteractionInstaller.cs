using UnityEngine;

namespace AnEchoHasNoShape.Interaction
{
    public static class InteractionInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (Object.FindAnyObjectByType<InteractionTextPanel>() == null)
            {
                GameObject panelObject = new GameObject("Interaction Text UI");
                panelObject.AddComponent<InteractionTextPanel>();
            }

            GameObject player = GameObject.FindWithTag("Player");

            if (player != null && player.GetComponent<PlayerInteractionController>() == null)
            {
                player.AddComponent<PlayerInteractionController>();
            }
        }
    }
}
