using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>
    /// Relays the player's real physics contacts to fake iceberg buoyancy. Kept
    /// separate from the third-party first-person controller so that controller
    /// updates cannot silently remove the integration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IcebergLandingReporter : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnPlayer()
        {
            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            if (player != null && player.GetComponent<IcebergLandingReporter>() == null)
            {
                player.gameObject.AddComponent<IcebergLandingReporter>();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            ReportSupport(collision, Mathf.Abs(collision.relativeVelocity.y));
        }

        private void OnCollisionStay(Collision collision)
        {
            ReportSupport(collision, 0f);
        }

        private static void ReportSupport(Collision collision, float downwardSpeed)
        {
            FakeIcebergBuoyancy iceberg = collision.collider.GetComponentInParent<FakeIcebergBuoyancy>();
            if (iceberg == null)
            {
                return;
            }

            // Side impacts must not make a nearby iceberg look as if it was
            // stepped on. At least one contact must support the player upward.
            for (int index = 0; index < collision.contactCount; index++)
            {
                if (collision.GetContact(index).normal.y > .45f)
                {
                    iceberg.NotifyPlayerSupport(-downwardSpeed);
                    return;
                }
            }
        }
    }
}
