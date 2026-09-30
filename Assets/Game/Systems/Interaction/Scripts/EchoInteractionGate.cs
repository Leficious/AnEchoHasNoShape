using AnEchoHasNoShape.Echolocation;
using UnityEngine;
using UnityEngine.Events;

namespace AnEchoHasNoShape.Interaction
{
    [AddComponentMenu("An Echo Has No Shape/Interaction/Echo Interaction Gate")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EchoRevealState))]
    public sealed class EchoInteractionGate : MonoBehaviour
    {
        [Tooltip("Hide the reveal immediately after a successful interaction.")]
        [SerializeField] private bool consumeRevealOnInteraction;

        [Tooltip("Optional feedback invoked when the player tries to interact while this object is hidden.")]
        [SerializeField] private UnityEvent onBlockedInteraction;

        private EchoRevealState revealState;

        public bool IsInteractionAvailable => RevealState != null && RevealState.IsRevealed;

        private EchoRevealState RevealState
        {
            get
            {
                if (revealState == null)
                {
                    revealState = GetComponent<EchoRevealState>();
                }

                return revealState;
            }
        }

        public bool TryAllowInteraction()
        {
            if (IsInteractionAvailable)
            {
                return true;
            }

            onBlockedInteraction?.Invoke();
            return false;
        }

        public void NotifyInteractionPerformed()
        {
            if (consumeRevealOnInteraction)
            {
                RevealState?.ConsumeReveal();
            }
        }
    }
}
