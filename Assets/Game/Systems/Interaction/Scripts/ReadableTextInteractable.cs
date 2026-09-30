using AnEchoHasNoShape.Echolocation;
using UnityEngine;

namespace AnEchoHasNoShape.Interaction
{
    [DisallowMultipleComponent]
    public sealed class ReadableTextInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction Prompt")]
        [Tooltip("Completes the prompt 'Press F to ...'. Examples: Read, Listen, Examine.")]
        [SerializeField] private string interactionVerb = "Read";

        [Header("Readable Text")]
        [Tooltip("Wrap mechanically important phrases in [important]...[/important] to render them larger with a warm accent color.")]
        [SerializeField, TextArea(6, 24)]
        private string text = "Write the text shown to the player here.";

        [Header("Text Presentation")]
        [Tooltip("How quickly this passage appears on screen.")]
        [SerializeField, Min(1f)] private float charactersPerSecond = 36f;

        [Header("Progression")]
        [Tooltip("Unlocks echolocation after the player closes this passage for the first time.")]
        [SerializeField] private bool unlockEchoAfterFirstRead;

        [Tooltip("Triggers the map-wide pulse from ReverberationCenter after the first completed reading.")]
        [SerializeField] private bool triggerWorldReverberationAfterFirstRead;

        [Tooltip("Makes HiddenDoorStone ignore echoes and become passable after the first completed reading.")]
        [SerializeField] private bool openHiddenDoorAfterFirstRead;

        private bool hasBeenRead;

        public string InteractionVerb => string.IsNullOrWhiteSpace(interactionVerb)
            ? "Interact"
            : interactionVerb.Trim();

        public void Interact(PlayerInteractionController interactor)
        {
            InteractionTextPanel.Show(text, charactersPerSecond, OnReadingCompleted);
        }

        public void EnableWorldReverberationAfterFirstRead()
        {
            triggerWorldReverberationAfterFirstRead = true;
        }

        public void EnableHiddenDoorAfterFirstRead()
        {
            openHiddenDoorAfterFirstRead = true;
        }

        private void OnReadingCompleted()
        {
            ApplyCompletedState(true, true);
        }

        public void ApplyCompletedState(bool playReverberation, bool recordProgress = true)
        {
            if (hasBeenRead) return;
            hasBeenRead = true;

            if (unlockEchoAfterFirstRead)
            {
                GameManager.Instance?.UnlockEcho();
                if (playReverberation)
                {
                    ScreenPromptUI.ShowEchoTutorial();
                }
            }

            if (triggerWorldReverberationAfterFirstRead && playReverberation)
            {
                WorldReverberationController.Instance?.TriggerReverberation();
            }

            if (triggerWorldReverberationAfterFirstRead && recordProgress)
            {
                GameManager.Instance?.RegisterWorldReverberationCompletion();
            }

            if (openHiddenDoorAfterFirstRead)
            {
                OpenHiddenDoorStone();
            }
        }

        public static void OpenHiddenDoorStone()
        {
            GameObject hiddenDoor = GameObject.Find("HiddenDoorStone");
            if (hiddenDoor == null)
            {
                Debug.LogWarning("Could not find HiddenDoorStone for the TabletMain event.");
                return;
            }

            IgnoreEcholocation ignoreMarker = hiddenDoor.GetComponent<IgnoreEcholocation>();
            if (ignoreMarker != null)
            {
                ignoreMarker.enabled = true;
            }

            Collider[] colliders = hiddenDoor.GetComponentsInChildren<Collider>(true);
            foreach (Collider targetCollider in colliders)
            {
                targetCollider.isTrigger = true;
            }

            PhaseThroughVolume phaseVolume = hiddenDoor.GetComponent<PhaseThroughVolume>();
            phaseVolume?.SetArmed(true);
        }

        private void OnValidate()
        {
            charactersPerSecond = Mathf.Max(1f, charactersPerSecond);

            if (string.IsNullOrWhiteSpace(interactionVerb))
            {
                interactionVerb = "Interact";
            }
        }
    }
}
