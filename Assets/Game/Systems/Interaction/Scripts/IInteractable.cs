namespace AnEchoHasNoShape.Interaction
{
    public interface IInteractionPrompt
    {
        string PromptText { get; }
        bool CanInteract { get; }
    }

    public interface IInteractable
    {
        string InteractionVerb { get; }

        void Interact(PlayerInteractionController interactor);
    }
}
