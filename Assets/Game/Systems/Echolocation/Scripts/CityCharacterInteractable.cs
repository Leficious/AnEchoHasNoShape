using UnityEngine;
using AnEchoHasNoShape.Interaction;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    public sealed class CityCharacterInteractable : MonoBehaviour, IInteractable
    {
        private CitySequenceController sequence;
        private int character;
        private string characterName;
        [SerializeField, TextArea(6, 24)] private string passage;
        [SerializeField, Min(1f)] private float charactersPerSecond = 36f;
        [SerializeField, HideInInspector] private bool dialogueInitialized;
        public bool DialogueInitialized => dialogueInitialized;
        public string InteractionVerb => $"speak to the {characterName}";
        public void Configure(CitySequenceController controller, int index, string displayName)
        {
            sequence = controller;
            character = index;
            characterName = displayName;
            InitializeDialogue(index);
            EchoRevealState reveal = GetComponent<EchoRevealState>();
            if (reveal == null)
            {
                reveal = gameObject.AddComponent<EchoRevealState>();
                reveal.ConfigureWindow(10f, 0.9f);
            }
            EchoInteractionGate gate = GetComponent<EchoInteractionGate>();
            if (gate == null) gate = gameObject.AddComponent<EchoInteractionGate>();
            gate.SetCityRevealSource(controller);
        }

        // Seed once, so Inspector edits (including an intentionally empty passage) survive play.
        public void InitializeDialogue(int index)
        {
            if (dialogueInitialized) return;
            dialogueInitialized = true;
            if (!string.IsNullOrWhiteSpace(passage)) return;
            passage = index switch
            {
                0 => "They count what stands.\nI cherish what will not stay—\n\na brief unfolding,\na passing wing,\na note after its singer has gone.\n\n[important]What fades is not less real\nfor having failed to remain.[/important]",
                1 => "They name this city by its towers.\nI know it by those who pass beneath them.\n\nAbove us, a promise catches the wind.\nBelow, a thousand lives give it meaning.\n\n[important]No city stands by stone alone.\nIt stands because we agree to belong.[/important]",
                2 => "I never knew the hands that raised this arch.\nStill, it shelters me.\n\nI never knew who laid these steps.\nStill, they bear my weight.\n\n[important]We build for those we will not meet.\nThey will know us by what remains.[/important]",
                _ => string.Empty
            };
        }

        public void Interact(PlayerInteractionController interactor)
        {
            EchoInteractionGate gate = GetComponent<EchoInteractionGate>();
            if (gate != null && gate.isActiveAndEnabled && !gate.TryAllowInteraction()) return;
            InteractionTextPanel.Show(passage, charactersPerSecond, OnPassageClosed);
        }

        private void OnPassageClosed()
        {
            if (this == null) return;
            GetComponent<EchoRevealState>()?.Reveal();
            if (this != null && sequence != null)
                sequence.Visit(character, transform.position);
        }
    }
}
