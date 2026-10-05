using UnityEngine;
using UnityEngine.Splines;

namespace AnEchoHasNoShape.TerrainTools
{
    [DisallowMultipleComponent]
    public sealed class SplineTerrainRoad : MonoBehaviour
    {
#if UNITY_EDITOR
        public Terrain terrain;
        [Tooltip("All road splines affecting this terrain. Rebuilt together from one baseline.")]
        public SplineContainer[] paths;
        public TerrainLayer roadLayer;
        public bool carve = true;
        public bool paint = true;
        public bool lowerOnly = true;
        [Min(0.1f)] public float floorWidth = 4f;
        [Min(0.01f)] public float shoulderWidth = 3f;
        public float heightOffset = -0.15f;
        [Min(0.1f)] public float paintWidth = 3f;
        [Min(0.01f)] public float paintFeather = 1f;
        [Min(0.1f)] public float sampleSpacing = 0.5f;
        [HideInInspector] public TerrainData baseline;
        [HideInInspector] public TerrainData baselineTarget;
#endif
    }
}
