using UnityEngine;

namespace AnEchoHasNoShape
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GlacierEntranceCheckpoint : MonoBehaviour
    {
        private GameManager.PlayerCheckpoint checkpoint;
        public void Configure(GameManager.PlayerCheckpoint value)
        {
            checkpoint = value;
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<FirstPersonController>() != null)
                GameManager.Instance?.SetCheckpoint(checkpoint);
        }
    }
}
