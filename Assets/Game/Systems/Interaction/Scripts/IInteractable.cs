namespace AnEchoHasNoShape.Interaction
{
    public interface IInteractable
    {
        string InteractionVerb { get; }

        void Interact(PlayerInteractionController interactor);
    }
}
