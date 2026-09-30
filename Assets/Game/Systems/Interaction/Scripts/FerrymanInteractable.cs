using UnityEngine;

namespace AnEchoHasNoShape.Interaction
{
    [AddComponentMenu("An Echo Has No Shape/Interaction/Ferryman Interactable")]
    [DisallowMultipleComponent]
    public sealed class FerrymanInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactionVerb = "Take Ferry";
        [SerializeField] private string question = "Take the ferry back?";
        [Tooltip("If empty, an object named GlacierRespawnStart is used.")]
        [SerializeField] private Transform destination;

        public string InteractionVerb => string.IsNullOrWhiteSpace(interactionVerb) ? "Take Ferry" : interactionVerb.Trim();

        public void Interact(PlayerInteractionController interactor)
        {
            ConfirmationPromptUI.Show(question, () => Teleport(interactor));
        }

        private void Teleport(PlayerInteractionController interactor)
        {
            Transform target = destination;
            if (target == null)
            {
                GameObject fallback = GameObject.Find("GlacierRespawnStart");
                if (fallback == null)
                {
                    fallback = GameObject.Find("GlacierRespawn");
                }
                target = fallback != null ? fallback.transform : null;
            }

            FirstPersonController controller = interactor != null
                ? interactor.GetComponent<FirstPersonController>()
                : FindAnyObjectByType<FirstPersonController>();

            if (target == null || controller == null)
            {
                Debug.LogWarning("Ferryman could not find the player or GlacierRespawnStart destination.", this);
                return;
            }

            GameManager gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            if (gameManager == null || !gameManager.TeleportPlayerWithFade(controller, target))
            {
                Debug.LogWarning("Ferryman could not start its travel transition.", this);
                return;
            }

            gameManager.SetCheckpoint(GameManager.PlayerCheckpoint.GlacierStart);
        }
    }
}
