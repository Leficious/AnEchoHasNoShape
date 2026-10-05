using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace AnEchoHasNoShape.TerrainTools
{
    [CustomEditor(typeof(SplineTerrainRoad))]
    public sealed class SplineTerrainRoadEditor : UnityEditor.Editor
    {
        struct Segment { public Vector3 a, b; }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var road = (SplineTerrainRoad)target;
            EditorGUILayout.HelpBox("Editor bake only. Spline Y + Height Offset defines the floor. Include ALL roads for this terrain in Paths. Rebuild replaces heights and paint from the saved baseline; hand edits made after capture will be lost. Save the scene after setup.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Rebuild Passageways + Road Paint")) Run(road, false);
                using (new EditorGUI.DisabledScope(road.baseline == null))
                {
                    if (GUILayout.Button("Restore Baseline Heights + Paint") && EditorUtility.DisplayDialog("Restore terrain?", "Replace current heights and paint with the captured baseline?", "Restore", "Cancel")) Run(road, true);
                    if (GUILayout.Button("Use Current Terrain as New Baseline...") && EditorUtility.DisplayDialog("New baseline?", "The next bake will capture the CURRENT terrain. Restore the old baseline and make your manual edits first, unless you want existing roads permanently included. The previous backup asset will be retained.", "Use Current Terrain", "Cancel"))
                    {
                        Undo.RecordObject(road, "Reset road baseline reference");
                        road.baseline = null;
                        road.baselineTarget = null;
                        EditorUtility.SetDirty(road);
                        EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                    }
                }
            }
        }

        static void Validate(SplineTerrainRoad road)
        {
            if (road.terrain == null || road.terrain.terrainData == null) throw new InvalidOperationException("Assign the target Terrain.");
            if (road.terrain.transform.rotation != Quaternion.identity || road.terrain.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("Terrain must have identity rotation and unit scale.");
            var data = road.terrain.terrainData;
            foreach (var other in UnityEngine.Object.FindObjectsByType<SplineTerrainRoad>(FindObjectsInactive.Include))
                if (other != road && other.terrain != null && other.terrain.terrainData == data)
                    throw new InvalidOperationException("Use one Spline Terrain Road component per terrain data asset. Add additional splines to its Paths array.");
            if (road.baseline != null && (road.baselineTarget != data || road.baseline == data ||
                road.baseline.heightmapResolution != data.heightmapResolution || road.baseline.size != data.size ||
                road.baseline.alphamapWidth != data.alphamapWidth || road.baseline.alphamapHeight != data.alphamapHeight))
                throw new InvalidOperationException("Baseline does not match this terrain's data, size or resolution. Restore before changing terrain resolution.");
            if (road.baseline != null)
            {
                var a = road.baseline.terrainLayers; var b = data.terrainLayers;
                if (a.Length != b.Length) throw new InvalidOperationException("Terrain layers changed since baseline capture.");
                for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) throw new InvalidOperationException("Terrain layer order changed since baseline capture.");
            }
        }

        static void Run(SplineTerrainRoad road, bool restore)
        {
            try
            {
                Validate(road);
                var data = road.terrain.terrainData;
                int layer = Array.IndexOf(data.terrainLayers, road.roadLayer);
                if (!restore && road.paint && (road.roadLayer == null || layer < 0))
                    throw new InvalidOperationException("Choose an existing layer from this terrain for Road Layer.");
                var segments = restore ? null : Sample(road);
                if (!restore && segments.Count == 0) throw new InvalidOperationException("Assign a spline with at least two knots.");
                if (!restore)
                {
                    Vector3 origin = road.terrain.transform.position, size = data.size;
                    foreach (var segment in segments)
                    {
                        Vector3 a = segment.a - origin, b = segment.b - origin;
                        if (Mathf.Min(a.x, b.x) < 0 || Mathf.Min(a.z, b.z) < 0 || Mathf.Max(a.x, b.x) > size.x || Mathf.Max(a.z, b.z) > size.z)
                            throw new InvalidOperationException("A path leaves the selected terrain. This version supports one terrain per road tool; split paths at terrain boundaries.");
                    }
                }
                if (!restore && !road.carve && !road.paint) return;
                if (road.baseline == null)
                {
                    // A persistent asset survives domain reloads and editor restarts. Never overwrite it.
                    const string folder = "Assets/Game/Terrain/RoadBaselines";
                    if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Game/Terrain", "RoadBaselines");
                    var copy = Instantiate(data);
                    copy.name = data.name + " Road Baseline";
                    AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(folder + "/RoadBaseline.asset"));
                    Undo.RecordObject(road, "Assign road baseline");
                    road.baseline = copy;
                    road.baselineTarget = data;
                    EditorUtility.SetDirty(road);
                    EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                    AssetDatabase.SaveAssets();
                }
                var baseline = road.baseline;
                int n = data.heightmapResolution;
                var heights = baseline.GetHeights(0, 0, n, n);
                var paint = baseline.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
                if (!restore)
                {
                    if (road.carve)
                    {
                        Raster(road, segments, n, n, Mathf.Max(0.05f, road.floorWidth * 0.5f), Mathf.Max(0.01f, road.shoulderWidth), false,
                            (x, z, y, weight) => {
                                float targetHeight = Mathf.Clamp01((y + road.heightOffset - road.terrain.transform.position.y) / data.size.y);
                                if (road.lowerOnly) targetHeight = Mathf.Min(heights[z, x], targetHeight);
                                heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight, weight);
                            });
                    }
                    if (road.paint)
                        Raster(road, segments, data.alphamapWidth, data.alphamapHeight, Mathf.Max(0.05f, road.paintWidth * 0.5f), Mathf.Max(0.01f, road.paintFeather), true,
                            (x, z, y, weight) => {
                                for (int l = 0; l < data.alphamapLayers; l++) paint[z, x, l] *= 1f - weight;
                                paint[z, x, layer] += weight;
                            });
                }
                Undo.RegisterCompleteObjectUndo(data, restore ? "Restore road baseline" : "Bake spline roads");
                data.SetHeights(0, 0, heights);
                data.SetAlphamaps(0, 0, paint);
                road.terrain.Flush();
                EditorUtility.SetDirty(data);
                Debug.Log(restore ? "Road baseline restored." : "Spline roads carved and painted. Save the project to keep the terrain bake.", road);
            }
            catch (Exception e) { Debug.LogError("Road bake: " + e.Message, road); }
        }

        static List<Segment> Sample(SplineTerrainRoad road)
        {
            var result = new List<Segment>();
            if (road.paths == null) return result;
            foreach (var path in road.paths)
            {
                if (path == null) continue;
                for (int s = 0; s < path.Splines.Count; s++)
                {
                    if (path.Splines[s].Count < 2) continue;
                    float length = path.CalculateLength(s);
                    int steps = Mathf.CeilToInt(length / Mathf.Max(0.1f, road.sampleSpacing));
                    if (steps > 50000) throw new InvalidOperationException("Spline sampling exceeds 50,000 segments. Increase Sample Spacing.");
                    steps = Mathf.Max(8, steps);
                    Vector3 previous = path.EvaluatePosition(s, 0f);
                    for (int i = 1; i <= steps; i++)
                    {
                        Vector3 next = path.EvaluatePosition(s, i / (float)steps);
                        result.Add(new Segment { a = previous, b = next });
                        previous = next;
                    }
                }
            }
            return result;
        }

        // Rasterize only each segment's footprint, then apply the closest road once.
        // Overlaps do not accumulate paint or repeatedly deepen the terrain.
        static void Raster(SplineTerrainRoad road, List<Segment> segments, int width, int height,
            float halfWidth, float feather, bool texelCenters, Action<int, int, float, float> apply)
        {
            var distance = new float[height, width];
            var elevation = new float[height, width];
            float radius = halfWidth + feather;
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++) distance[z, x] = radius * radius;
            Vector3 origin = road.terrain.transform.position, size = road.terrain.terrainData.size;
            float dx = size.x / (texelCenters ? width : width - 1), dz = size.z / (texelCenters ? height : height - 1);
            float offset = texelCenters ? 0.5f : 0f;
            foreach (var seg in segments)
            {
                Vector3 a = seg.a - origin, b = seg.b - origin;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius) / dx - offset));
                int x1 = Mathf.Min(width - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + radius) / dx - offset));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, b.z) - radius) / dz - offset));
                int z1 = Mathf.Min(height - 1, Mathf.CeilToInt((Mathf.Max(a.z, b.z) + radius) / dz - offset));
                Vector2 direction = new Vector2(b.x - a.x, b.z - a.z);
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                {
                    Vector2 relative = new Vector2((x + offset) * dx - a.x, (z + offset) * dz - a.z);
                    float t = direction.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector2.Dot(relative, direction) / direction.sqrMagnitude) : 0f;
                    float d = (relative - direction * t).sqrMagnitude;
                    if (d >= distance[z, x]) continue;
                    distance[z, x] = d;
                    elevation[z, x] = Mathf.Lerp(seg.a.y, seg.b.y, t);
                }
            }
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                float d = Mathf.Sqrt(distance[z, x]);
                if (d >= radius) continue;
                float weight = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((d - halfWidth) / feather));
                apply(x, z, elevation[z, x], weight);
            }
        }

        [InitializeOnLoadMethod]
        static void ScheduleSetup() { EditorApplication.delayCall += Setup; }

        [MenuItem("Tools/An Echo Has No Shape/Set Up RoadTest")]
        static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Setup; return; }
            foreach (var path in UnityEngine.Object.FindObjectsByType<SplineContainer>(FindObjectsInactive.Exclude))
            {
                if (!path.name.Equals("roadtest", StringComparison.OrdinalIgnoreCase) || path.GetComponent<SplineTerrainRoad>() != null) continue;
                var road = Undo.AddComponent<SplineTerrainRoad>(path.gameObject);
                road.paths = new[] { path };
                road.roadLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Game/Terrain/Layers/Road.terrainlayer");
                Vector3 p = path.Splines.Count > 0 && path.Splines[0].Count > 0 ? (Vector3)path.EvaluatePosition(0, 0f) : path.transform.position;
                foreach (var terrain in Terrain.activeTerrains)
                {
                    Vector3 local = p - terrain.transform.position;
                    Vector3 size = terrain.terrainData.size;
                    if (local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z) { road.terrain = terrain; break; }
                }
                EditorUtility.SetDirty(road);
                EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
                Debug.Log("RoadTest is ready: select it and use Rebuild Passageways + Road Paint. Terrain is unchanged until you bake.", road);
            }
        }
    }
}
