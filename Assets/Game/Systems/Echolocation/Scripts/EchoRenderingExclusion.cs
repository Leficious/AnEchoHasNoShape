using UnityEngine;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>
    /// Prevents helper or trigger geometry from receiving any echo overlay.
    /// Unlike IgnoreEcholocation, this does not publish a world-space fog mask.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoRenderingExclusion : MonoBehaviour
    {
    }
}
