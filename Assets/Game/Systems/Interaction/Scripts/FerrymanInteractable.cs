using UnityEngine;

namespace AnEchoHasNoShape.Interaction
{
    [AddComponentMenu("An Echo Has No Shape/Interaction/Ferryman Interactable")]
    [DisallowMultipleComponent]
    public sealed class FerrymanInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactionVerb = "Take Ferry";
        public string InteractionVerb => string.IsNullOrWhiteSpace(interactionVerb) ? "Take Ferry" : interactionVerb.Trim();

        public void Interact(PlayerInteractionController interactor)
        {
            ConfirmationPromptUI.ShowChoices("Where shall we take you?",
                "Ancient Monument", () => Teleport(interactor, GameManager.PlayerCheckpoint.GlacierMonument, "GlacierRespawnMonument"),
                "Unremembered City", () => Teleport(interactor, GameManager.PlayerCheckpoint.GlacierCity, "GlacierRespawnCity"));
        }

        private void Teleport(PlayerInteractionController interactor, GameManager.PlayerCheckpoint checkpoint, string locator)
        {
            GameObject target = GameObject.Find(locator);
            FirstPersonController player = interactor != null
                ? interactor.GetComponentInParent<FirstPersonController>()
                : FindAnyObjectByType<FirstPersonController>();
            GameManager manager = GameManager.Instance;
            if (target == null || player == null || manager == null)
            {
                Debug.LogWarning("Ferry could not resolve player, GameManager or destination: " + locator, this);
                return;
            }
            if (manager.TeleportPlayerWithFade(player, target.transform))
                manager.SetCheckpoint(checkpoint);
        }
    }
}
