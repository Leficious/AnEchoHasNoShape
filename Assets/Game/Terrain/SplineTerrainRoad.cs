using UnityEngine;
using UnityEngine.Splines;
using System;
using System.Collections.Generic;

namespace AnEchoHasNoShape.TerrainTools
{
    [DisallowMultipleComponent]
    public sealed class SplineTerrainRoad : MonoBehaviour
    {
#if UNITY_EDITOR
        [Tooltip("Every terrain tile this road may cross. Each tile keeps its own baseline and paint history.")]
        public Terrain[] terrains;
        [Tooltip("All road splines affecting these terrains. Rebuilt together.")]
        public SplineContainer[] paths;
        public TerrainLayer roadLayer;
        public bool carve = true;
        public bool paint = true;
        public bool lowerOnly = true;
        [Min(0.1f)] public float floorWidth = 4f;
        [Min(0.01f)] public float shoulderWidth = 3f;
        public float heightOffset = -0.15f;
        [Min(0.1f)] public float paintWidth = 3f;
        [Min(0.01f)] public float paintFeather = 3f;
        [Min(0.1f)] public float sampleSpacing = 0.5f;
        [Serializable]
        public sealed class TerrainState
        {
            public Terrain terrain;
            public TerrainData baseline;
            public TerrainData baselineTarget;
            public TerrainData lastBakedPaint;
            public string lastPaintBackupPath;
        }

        [SerializeField, HideInInspector] public List<TerrainState> tileStates = new List<TerrainState>();

        // Serialized fields from the original one-terrain tool. The editor migrates
        // them into a TerrainState so existing captures are not discarded.
        [SerializeField, HideInInspector] public Terrain terrain;
        [SerializeField, HideInInspector] public TerrainData baseline;
        [SerializeField, HideInInspector] public TerrainData baselineTarget;
        [SerializeField, HideInInspector] public TerrainData lastBakedPaint;
#endif
    }
}
