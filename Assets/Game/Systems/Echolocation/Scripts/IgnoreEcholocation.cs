using UnityEngine;
using System.Collections.Generic;

namespace AnEchoHasNoShape.Echolocation
{
    [AddComponentMenu("An Echo Has No Shape/Echolocation/Ignore Echolocation")]
    [DisallowMultipleComponent]
    public sealed class IgnoreEcholocation : MonoBehaviour
    {
        private const int MaxFogIgnoreRegions = 16;
        private static readonly int IgnoreCountId = Shader.PropertyToID("_EchoIgnoredBoundsCount");
        private static readonly int IgnoreMinId = Shader.PropertyToID("_EchoIgnoredBoundsMin");
        private static readonly int IgnoreMaxId = Shader.PropertyToID("_EchoIgnoredBoundsMax");
        private static readonly List<IgnoreEcholocation> ActiveMarkers = new List<IgnoreEcholocation>();
        private static readonly Vector4[] BoundsMin = new Vector4[MaxFogIgnoreRegions];
        private static readonly Vector4[] BoundsMax = new Vector4[MaxFogIgnoreRegions];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveMarkers.Clear();
            Shader.SetGlobalInt(IgnoreCountId, 0);
        }

        public static bool IsIgnored(Transform target)
        {
            IgnoreEcholocation[] markers = target.GetComponentsInParent<IgnoreEcholocation>(true);
            foreach (IgnoreEcholocation marker in markers)
            {
                if (marker.isActiveAndEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnEnable()
        {
            if (!ActiveMarkers.Contains(this))
            {
                ActiveMarkers.Add(this);
            }

            RefreshChildren();
            PublishFogIgnoreRegions();
        }

        private void OnDisable()
        {
            ActiveMarkers.Remove(this);
            RefreshChildren();
            PublishFogIgnoreRegions();
        }

        private void LateUpdate()
        {
            // One marker publishes the combined list each frame so moving or
            // animated ignored objects keep their fog mask aligned.
            if (ActiveMarkers.Count > 0 && ActiveMarkers[0] == this)
            {
                PublishFogIgnoreRegions();
            }
        }

        private static void PublishFogIgnoreRegions()
        {
            int regionCount = 0;

            foreach (IgnoreEcholocation marker in ActiveMarkers)
            {
                if (marker == null || !marker.isActiveAndEnabled || regionCount >= MaxFogIgnoreRegions ||
                    !marker.TryGetWorldBounds(out Bounds bounds))
                {
                    continue;
                }

                BoundsMin[regionCount] = bounds.min;
                BoundsMax[regionCount] = bounds.max;
                regionCount++;
            }

            Shader.SetGlobalInt(IgnoreCountId, regionCount);
            Shader.SetGlobalVectorArray(IgnoreMinId, BoundsMin);
            Shader.SetGlobalVectorArray(IgnoreMaxId, BoundsMax);
        }

        private bool TryGetWorldBounds(out Bounds combinedBounds)
        {
            combinedBounds = default;
            bool hasBounds = false;

            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            Terrain[] terrains = GetComponentsInChildren<Terrain>(true);
            foreach (Terrain terrain in terrains)
            {
                if (terrain.terrainData == null)
                {
                    continue;
                }

                Bounds localBounds = terrain.terrainData.bounds;
                Vector3 scale = terrain.transform.lossyScale;
                Vector3 worldSize = Vector3.Scale(localBounds.size,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                Bounds terrainBounds = new Bounds(terrain.transform.TransformPoint(localBounds.center), worldSize);

                if (!hasBounds)
                {
                    combinedBounds = terrainBounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(terrainBounds);
                }
            }

            return hasBounds;
        }

        private void RefreshChildren()
        {
            EchoReactiveSurface[] surfaces = GetComponentsInChildren<EchoReactiveSurface>(true);
            foreach (EchoReactiveSurface surface in surfaces)
            {
                surface.RefreshEchoIgnoreState();
            }

            EchoReactiveTerrain[] terrains = GetComponentsInChildren<EchoReactiveTerrain>(true);
            foreach (EchoReactiveTerrain terrain in terrains)
            {
                terrain.RefreshEchoIgnoreState();
            }

            if (!Application.isPlaying || isActiveAndEnabled)
            {
                return;
            }

            // If this marker was active when the scene loaded, the automatic
            // installer deliberately skipped these objects. Add echo support
            // when the marker is disabled at runtime.
            MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in childRenderers)
            {
                if (renderer.GetComponent<MeshFilter>() != null &&
                    (renderer.gameObject.hideFlags & HideFlags.DontSave) == 0 &&
                    !renderer.transform.root.CompareTag("Player") &&
                    !IsIgnored(renderer.transform) &&
                    renderer.GetComponent<EchoReactiveSurface>() == null)
                {
                    renderer.gameObject.AddComponent<EchoReactiveSurface>();
                }
            }

            Terrain[] childTerrains = GetComponentsInChildren<Terrain>(true);
            foreach (Terrain terrain in childTerrains)
            {
                if (!IsIgnored(terrain.transform) && terrain.GetComponent<EchoReactiveTerrain>() == null)
                {
                    terrain.gameObject.AddComponent<EchoReactiveTerrain>();
                }
            }
        }
    }
}
