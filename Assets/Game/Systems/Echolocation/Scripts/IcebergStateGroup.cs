using UnityEngine;
using AnEchoHasNoShape.Core;

namespace AnEchoHasNoShape.Echolocation
{
    public enum IcebergEchoState
    {
        PhysicalEchoable = 0,
        InvisibleEchoable = 1,
        VisibleFalse = 2
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class IcebergStateGroup : MonoBehaviour
    {
        [SerializeField] private IcebergEchoState state;

        public IcebergEchoState State => state;

        public void Configure(IcebergEchoState newState)
        {
            state = newState;
            ApplyState();
        }

        public void ApplyState()
        {
            bool renderPhysicalMesh = state != IcebergEchoState.InvisibleEchoable;
            bool enableCollision = state != IcebergEchoState.VisibleFalse;

            foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                // Runtime echo overlays are children too, but their visibility is
                // controlled by the echo shader rather than the physical state.
                if ((meshRenderer.gameObject.hideFlags & HideFlags.DontSave) == 0)
                {
                    meshRenderer.enabled = renderPhysicalMesh;
                }
            }

            foreach (Collider childCollider in GetComponentsInChildren<Collider>(true))
            {
                childCollider.enabled = enableCollision;
                if (Application.isPlaying && enableCollision)
                {
                    childCollider.sharedMaterial = GameplayPhysicsMaterials.Ice;
                }
            }
        }

        private void Awake()
        {
            ApplyState();
        }

        private void OnEnable()
        {
            ApplyState();
        }

        private void OnValidate()
        {
            ApplyState();
        }

        private void OnTransformChildrenChanged()
        {
            ApplyState();
        }
    }
}
