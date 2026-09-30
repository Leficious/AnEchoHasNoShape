using UnityEngine;

namespace AnEchoHasNoShape.GroundBlend
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GroundBlendGlobals : MonoBehaviour
    {
        [SerializeField] private Texture2D terrainColorMap;
        [SerializeField] private Vector2 worldMinimum;
        [SerializeField] private Vector2 worldSize = Vector2.one;

        private static readonly int MapId = Shader.PropertyToID("_GlobalGroundBlendMap");
        private static readonly int BoundsId = Shader.PropertyToID("_GlobalGroundBlendBounds");
        private static readonly int AvailableId = Shader.PropertyToID("_GlobalGroundBlendMapAvailable");

        public Texture2D TerrainColorMap => terrainColorMap;
        public Vector2 WorldMinimum => worldMinimum;
        public Vector2 WorldSize => worldSize;

        private void OnEnable() => ApplyGlobals();
        private void OnValidate() => ApplyGlobals();

        private void OnDisable()
        {
            Shader.SetGlobalFloat(AvailableId, 0f);
        }

        public void Configure(Texture2D map, Vector2 minimum, Vector2 size)
        {
            terrainColorMap = map;
            worldMinimum = minimum;
            worldSize = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
            ApplyGlobals();
        }

        [ContextMenu("Apply Ground Blend Globals")]
        public void ApplyGlobals()
        {
            Shader.SetGlobalTexture(MapId, terrainColorMap != null ? terrainColorMap : Texture2D.grayTexture);
            Shader.SetGlobalVector(BoundsId, new Vector4(
                worldMinimum.x,
                worldMinimum.y,
                Mathf.Max(0.001f, worldSize.x),
                Mathf.Max(0.001f, worldSize.y)));
            Shader.SetGlobalFloat(AvailableId, terrainColorMap != null ? 1f : 0f);
        }
    }
}
