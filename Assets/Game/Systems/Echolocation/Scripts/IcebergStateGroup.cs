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

        [Header("Fake Buoyancy")]
        [SerializeField] private bool enableFakeBuoyancy = true;
        [SerializeField, Min(0f)] private float bobHeight = .045f;
        [SerializeField, Min(.25f)] private float bobCycleSeconds = 5.5f;
        [SerializeField, Min(0f)] private float driftDistance = .1f;
        [SerializeField, Min(.5f)] private float driftCycleSeconds = 14f;
        [SerializeField, Min(0f)] private float tiltDegrees = .3f;

        [Header("Landing Response")]
        [SerializeField, Min(0f)] private float landingDip = .11f;
        [SerializeField, Min(0f)] private float standingSink = .018f;
        [SerializeField, Min(0f)] private float landingSpring = 24f;
        [SerializeField, Min(0f)] private float landingDamping = 7.5f;

        public IcebergEchoState State => state;
        private bool setupPending;

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
            setupPending = true;
        }

        private void OnEnable()
        {
            setupPending = true;
        }

        private void OnValidate()
        {
            // Validation may run while Unity is loading/checking objects.
            // Do not mutate components or physics state from that callback.
            setupPending = true;
        }

        private void OnTransformChildrenChanged()
        {
            setupPending = true;
        }

        private void Update()
        {
            if (!setupPending) return;
            setupPending = false;
            ApplyState();
            if (Application.IsPlaying(gameObject))
            {
                InstallFakeBuoyancy();
            }
        }

        private void InstallFakeBuoyancy()
        {
            if (!Application.IsPlaying(gameObject) || !enableFakeBuoyancy)
            {
                return;
            }

            bool collisionEnabled = state != IcebergEchoState.VisibleFalse;
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform iceberg = transform.GetChild(index);
                FakeIcebergBuoyancy buoyancy = iceberg.GetComponent<FakeIcebergBuoyancy>();
                if (buoyancy == null)
                {
                    buoyancy = iceberg.gameObject.AddComponent<FakeIcebergBuoyancy>();
                }

                buoyancy.Configure(
                    collisionEnabled,
                    StableSeed(iceberg.name, index),
                    bobHeight,
                    bobCycleSeconds,
                    driftDistance,
                    driftCycleSeconds,
                    tiltDegrees,
                    landingDip,
                    standingSink,
                    landingSpring,
                    landingDamping);
            }
        }

        private static int StableSeed(string objectName, int siblingIndex)
        {
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < objectName.Length; index++)
                {
                    hash = hash * 31 + objectName[index];
                }

                return hash * 31 + siblingIndex;
            }
        }
    }
}
