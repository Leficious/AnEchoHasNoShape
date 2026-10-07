using System;
using System.Collections.Generic;
using System.IO;
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
            var road = (SplineTerrainRoad)target;
            if (!Application.isPlaying) MigrateLegacy(road);
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Road paint is applied to the terrain's CURRENT texture layers, only near the spline. Each paint bake saves an immutable pre-bake copy. Heights still rebuild from the saved baseline.", MessageType.Info);
            float largestTexel = 0f;
            if (road.terrains != null)
                foreach (var tile in road.terrains)
                    if (tile != null && tile.terrainData != null)
                    {
                        var data = tile.terrainData;
                        largestTexel = Mathf.Max(largestTexel,
                            data.size.x / data.alphamapWidth, data.size.z / data.alphamapHeight);
                    }
            if (largestTexel > 0f)
            {
                float featherTexels = road.paintFeather / largestTexel;
                EditorGUILayout.HelpBox($"Paint feather: {featherTexels:0.0} texels on the coarsest tile. Around 4 or more gives a visibly soft blend. The bake also samples coverage within each texel.",
                    featherTexels < 3f ? MessageType.Warning : MessageType.Info);
                if (!Application.isPlaying && featherTexels < 3f && GUILayout.Button("Set Paint Feather to 4 Texels"))
                {
                    Undo.RecordObject(road, "Set smoother road paint feather");
                    road.paintFeather = largestTexel * 4f;
                    EditorUtility.SetDirty(road);
                    EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                }
            }
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Add All Active Terrains"))
                {
                    Undo.RecordObject(road, "Assign road terrains");
                    road.terrains = Terrain.activeTerrains;
                    EditorUtility.SetDirty(road);
                    EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                }
                if (GUILayout.Button("Rebuild Passageways + Road Paint")) Run(road, false);
                using (new EditorGUI.DisabledScope(!HasBaseline(road)))
                {
                    if (GUILayout.Button("Restore Baseline Heights Only (Keep Paint)") && EditorUtility.DisplayDialog("Restore terrain heights?", "Restore baseline heights? Current terrain texture painting, including road paint, will be left untouched.", "Restore Heights", "Cancel")) Run(road, true);
                    if (GUILayout.Button("Use Current Terrain as New Baseline...") && EditorUtility.DisplayDialog("New baseline?", "The next bake will capture the CURRENT terrain. Restore the old baseline and make your manual edits first, unless you want existing roads permanently included. The previous backup asset will be retained.", "Use Current Terrain", "Cancel"))
                    {
                        MigrateLegacy(road);
                        Undo.RecordObject(road, "Reset road baseline reference");
                        foreach (var state in road.tileStates)
                        {
                            state.baseline = null;
                            state.baselineTarget = null;
                            state.lastBakedPaint = null;
                        }
                        road.baseline = null;
                        road.baselineTarget = null;
                        road.lastBakedPaint = null;
                        EditorUtility.SetDirty(road);
                        EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                    }
                }
                if (road.tileStates != null)
                    foreach (var state in road.tileStates)
                        if (state != null && state.terrain != null && !string.IsNullOrEmpty(state.lastPaintBackupPath))
                            if (GUILayout.Button("Restore Last Paint Backup: " + state.terrain.name) &&
                                EditorUtility.DisplayDialog("Restore terrain paint?", "Replace ALL texture paint on " + state.terrain.name + " with its last pre-road-bake backup? A new backup of the current paint will be saved first.", "Restore Paint", "Cancel"))
                                RestorePaintBackup(road, state);
            }
        }

        static bool HasBaseline(SplineTerrainRoad road)
        {
            if (road.baseline != null) return true;
            if (road.tileStates == null) return false;
            foreach (var state in road.tileStates) if (state != null && state.baseline != null) return true;
            return false;
        }

        static void MigrateLegacy(SplineTerrainRoad road)
        {
            bool changed = false;
            if (road.tileStates == null)
            {
                Undo.RecordObject(road, "Migrate road terrain settings");
                road.tileStates = new List<SplineTerrainRoad.TerrainState>();
                changed = true;
            }
            if ((road.terrains == null || road.terrains.Length == 0) && road.terrain != null)
            {
                if (!changed) Undo.RecordObject(road, "Migrate road terrain settings");
                road.terrains = new[] { road.terrain };
                changed = true;
            }
            if (road.terrain != null && road.baseline != null)
            {
                var oldState = road.tileStates.Find(s => s != null && s.terrain == road.terrain);
                if (oldState == null)
                {
                    if (!changed) Undo.RecordObject(road, "Migrate road terrain settings");
                    oldState = new SplineTerrainRoad.TerrainState { terrain = road.terrain };
                    road.tileStates.Add(oldState);
                    changed = true;
                }
                if (oldState.baseline == null)
                {
                    if (!changed) Undo.RecordObject(road, "Migrate road terrain settings");
                    oldState.baseline = road.baseline;
                    oldState.baselineTarget = road.baselineTarget;
                    oldState.lastBakedPaint = road.lastBakedPaint;
                    changed = true;
                }
            }
            // Clear legacy references only after their tile state is populated.
            if (road.terrain != null && road.baseline != null &&
                road.tileStates.Exists(s => s != null && s.terrain == road.terrain && s.baseline == road.baseline))
            {
                if (!changed) Undo.RecordObject(road, "Migrate road terrain settings");
                road.baseline = null;
                road.baselineTarget = null;
                road.lastBakedPaint = null;
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(road);
                EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            }
        }

        static void Validate(SplineTerrainRoad road, List<Segment> segments, bool restore)
        {
            if (road.terrains == null || road.terrains.Length == 0)
                throw new InvalidOperationException("Add all terrain tiles crossed by the road to Terrains.");
            var seen = new HashSet<TerrainData>();
            foreach (var terrain in road.terrains)
            {
                if (terrain == null || terrain.terrainData == null)
                    throw new InvalidOperationException("A Terrain entry is missing its Terrain or TerrainData.");
                if (terrain.transform.rotation != Quaternion.identity || terrain.transform.lossyScale != Vector3.one)
                    throw new InvalidOperationException(terrain.name + " must have identity rotation and unit scale.");
                TerrainData data = terrain.terrainData;
                if (!seen.Add(data))
                    throw new InvalidOperationException("A TerrainData asset is assigned more than once. Each road tile needs distinct TerrainData.");
                if (!restore && road.paint && Array.IndexOf(data.terrainLayers, road.roadLayer) < 0)
                    throw new InvalidOperationException(terrain.name + " does not have the chosen Road Layer. Add it to this tile before baking.");
                foreach (var other in UnityEngine.Object.FindObjectsByType<SplineTerrainRoad>(FindObjectsInactive.Include))
                {
                    if (other == road || other.terrains == null) continue;
                    foreach (var otherTerrain in other.terrains)
                        if (otherTerrain != null && otherTerrain.terrainData == data)
                            throw new InvalidOperationException(terrain.name + " is assigned to another Spline Terrain Road component.");
                }
                var state = road.tileStates.Find(s => s != null && s.terrain == terrain);
                if (state == null) continue;
                if (state.baseline != null && (state.baselineTarget != data || state.baseline == data ||
                    state.baseline.heightmapResolution != data.heightmapResolution || state.baseline.size != data.size ||
                    state.baseline.alphamapWidth != data.alphamapWidth || state.baseline.alphamapHeight != data.alphamapHeight))
                    throw new InvalidOperationException(terrain.name + " no longer matches its baseline size or resolution.");
                if (state.baseline != null)
                {
                    var a = state.baseline.terrainLayers;
                    var b = data.terrainLayers;
                    if (a.Length != b.Length) throw new InvalidOperationException(terrain.name + " layer count changed since baseline capture.");
                    for (int i = 0; i < a.Length; i++)
                        if (a[i] != b[i]) throw new InvalidOperationException(terrain.name + " layer order changed since baseline capture.");
                }
            }
            if (restore || segments == null) return;
            foreach (var segment in segments)
            {
                if (!InsideAnyTerrain(road.terrains, segment.a) ||
                    !InsideAnyTerrain(road.terrains, (segment.a + segment.b) * 0.5f) ||
                    !InsideAnyTerrain(road.terrains, segment.b))
                    throw new InvalidOperationException("A road spline leaves the assigned terrain tiles. Add the missing tile to Terrains.");
            }
        }

        static bool InsideAnyTerrain(Terrain[] terrains, Vector3 point)
        {
            foreach (var terrain in terrains)
            {
                Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
                if (point.x >= origin.x - 0.01f && point.z >= origin.z - 0.01f &&
                    point.x <= origin.x + size.x + 0.01f && point.z <= origin.z + size.z + 0.01f)
                    return true;
            }
            return false;
        }

        static bool TouchesTerrain(Terrain terrain, List<Segment> segments, float radius)
        {
            Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
            float right = origin.x + size.x, far = origin.z + size.z;
            foreach (var segment in segments)
                if (Mathf.Max(segment.a.x, segment.b.x) + radius >= origin.x &&
                    Mathf.Min(segment.a.x, segment.b.x) - radius <= right &&
                    Mathf.Max(segment.a.z, segment.b.z) + radius >= origin.z &&
                    Mathf.Min(segment.a.z, segment.b.z) - radius <= far)
                    return true;
            return false;
        }

        static void Run(SplineTerrainRoad road, bool restore)
        {
            try
            {
                Undo.RecordObject(road, "Update road terrain assignments");
                MigrateLegacy(road);
                if (!restore && !road.carve && !road.paint) return;
                var segments = restore ? null : Sample(road);
                if (!restore && segments.Count == 0)
                    throw new InvalidOperationException("Assign a spline with at least two knots.");
                Validate(road, segments, restore);
                int count = 0;
                foreach (var terrain in road.terrains)
                {
                    var state = road.tileStates.Find(s => s != null && s.terrain == terrain);
                    float radius = Mathf.Max(
                        road.carve ? road.floorWidth * 0.5f + road.shoulderWidth : 0f,
                        road.paint ? road.paintWidth * 0.5f + road.paintFeather : 0f);
                    bool touches = !restore && TouchesTerrain(terrain, segments, radius);
                    if (!touches && (state == null || state.baseline == null)) continue;
                    if (restore && (state == null || state.baseline == null)) continue;
                    if (state == null)
                    {
                        state = new SplineTerrainRoad.TerrainState { terrain = terrain };
                        road.tileStates.Add(state);
                        EditorUtility.SetDirty(road);
                        EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                    }
                    BakeTile(road, terrain, state, segments, restore);
                    count++;
                }
                AssetDatabase.SaveAssets();
                Debug.Log(restore ? "Restored baseline heights on " + count + " terrain tiles; paint was not changed." :
                    "Carved and painted roads on " + count + " terrain tiles. Save the scene and project.", road);
            }
            catch (Exception e) { Debug.LogError("Road bake: " + e.Message, road); }
        }

        static void BakeTile(SplineTerrainRoad road, Terrain terrain, SplineTerrainRoad.TerrainState state,
            List<Segment> segments, bool restore)
        {
            TerrainData data = terrain.terrainData;
            bool hadBaseline = state.baseline != null;
            if (!hadBaseline)
            {
                const string folder = "Assets/Game/Terrain/RoadBaselines";
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder("Assets/Game/Terrain", "RoadBaselines");
                var copy = Instantiate(data);
                copy.name = data.name + " Road Baseline";
                AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(folder + "/RoadBaseline.asset"));
                Undo.RecordObject(road, "Capture terrain road baseline");
                state.baseline = copy;
                state.baselineTarget = data;
                EditorUtility.SetDirty(road);
                EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            }

            int n = data.heightmapResolution;
            var heights = state.baseline.GetHeights(0, 0, n, n);
            // Never reconstruct paint from a baseline or a previous bake: doing so
            // can replace hand-painted areas across the entire terrain tile.
            bool paintThisTile = !restore && road.paint && TouchesTerrain(terrain, segments,
                road.paintWidth * 0.5f + road.paintFeather);
            float[,,] paint = paintThisTile
                ? data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight) : null;
            int minPaintX = data.alphamapWidth, minPaintZ = data.alphamapHeight;
            int maxPaintX = -1, maxPaintZ = -1;
            if (paint != null)
            {
                string backupPath = SavePaintBackup(data, terrain.name, paint);
                Undo.RecordObject(road, "Record road paint backup");
                state.lastPaintBackupPath = backupPath;
                EditorUtility.SetDirty(road);
                EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            }

            if (!restore)
            {
                if (road.carve)
                    Raster(terrain, segments, n, n, Mathf.Max(0.05f, road.floorWidth * 0.5f),
                        Mathf.Max(0.01f, road.shoulderWidth), false,
                        (x, z, y, weight) =>
                        {
                            float target = Mathf.Clamp01((y + road.heightOffset - terrain.transform.position.y) / data.size.y);
                            if (road.lowerOnly) target = Mathf.Min(heights[z, x], target);
                            heights[z, x] = Mathf.Lerp(heights[z, x], target, weight);
                        });
                if (paintThisTile)
                {
                    int layer = Array.IndexOf(data.terrainLayers, road.roadLayer);
                    Raster(terrain, segments, data.alphamapWidth, data.alphamapHeight,
                        Mathf.Max(0.05f, road.paintWidth * 0.5f), Mathf.Max(0.01f, road.paintFeather), true,
                        (x, z, y, weight) =>
                        {
                            // Set a minimum road weight instead of blending it
                            // repeatedly; successive bakes then leave the feather unchanged.
                            float existingRoad = paint[z, x, layer];
                            if (existingRoad >= weight) return;
                            float otherScale = (1f - weight) / Mathf.Max(0.000001f, 1f - existingRoad);
                            for (int l = 0; l < data.alphamapLayers; l++)
                                if (l != layer) paint[z, x, l] *= otherScale;
                            paint[z, x, layer] = weight;
                            minPaintX = Mathf.Min(minPaintX, x);
                            minPaintZ = Mathf.Min(minPaintZ, z);
                            maxPaintX = Mathf.Max(maxPaintX, x);
                            maxPaintZ = Mathf.Max(maxPaintZ, z);
                        });
                }
            }

            Undo.RegisterCompleteObjectUndo(data, restore ? "Restore spline roads" : "Bake spline roads");
            data.SetHeights(0, 0, heights);
            if (maxPaintX >= minPaintX && maxPaintZ >= minPaintZ)
            {
                int width = maxPaintX - minPaintX + 1, height = maxPaintZ - minPaintZ + 1;
                var changedPaint = new float[height, width, data.alphamapLayers];
                for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
                    for (int l = 0; l < data.alphamapLayers; l++)
                        changedPaint[z, x, l] = paint[minPaintZ + z, minPaintX + x, l];
                data.SetAlphamaps(minPaintX, minPaintZ, changedPaint);
            }
            terrain.Flush();
            EditorUtility.SetDirty(data);
        }

        const string PaintBackupFolder = "Assets/Game/Terrain/RoadBaselines";
        const string PaintBackupSignature = "AEHNS Road Paint 1";

        static string SavePaintBackup(TerrainData data, string terrainName, float[,,] paint)
        {
            if (!AssetDatabase.IsValidFolder(PaintBackupFolder))
                AssetDatabase.CreateFolder("Assets/Game/Terrain", "RoadBaselines");
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Save " + terrainName + " TerrainData as an asset before road painting so a recoverable backup can be made.");
            string safeName = string.Concat(terrainName.Split(Path.GetInvalidFileNameChars()));
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(PaintBackupFolder + "/PreRoadPaint_" + safeName + "_" +
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".bytes");
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            string temporaryPath = fullPath + ".tmp";
            try
            {
                using (var writer = new BinaryWriter(new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write)))
                {
                    writer.Write(PaintBackupSignature);
                    writer.Write(guid);
                    writer.Write(data.alphamapWidth);
                    writer.Write(data.alphamapHeight);
                    writer.Write(data.alphamapLayers);
                    for (int z = 0; z < data.alphamapHeight; z++)
                        for (int x = 0; x < data.alphamapWidth; x++)
                            for (int l = 0; l < data.alphamapLayers; l++)
                                writer.Write(paint[z, x, l]);
                }
                File.Move(temporaryPath, fullPath);
                AssetDatabase.ImportAsset(assetPath);
                return assetPath;
            }
            catch
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                throw;
            }
        }

        static void RestorePaintBackup(SplineTerrainRoad road, SplineTerrainRoad.TerrainState state)
        {
            try
            {
                TerrainData data = state.terrain.terrainData;
                string assetPath = state.lastPaintBackupPath;
                if (!assetPath.StartsWith(PaintBackupFolder + "/", StringComparison.Ordinal) ||
                    !assetPath.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The paint backup is not in the road backup folder.");
                string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    assetPath.Replace('/', Path.DirectorySeparatorChar));
                float[,,] paint;
                using (var reader = new BinaryReader(File.OpenRead(fullPath)))
                {
                    string signature = reader.ReadString();
                    string guid = reader.ReadString();
                    int width = reader.ReadInt32(), height = reader.ReadInt32(), layers = reader.ReadInt32();
                    if (signature != PaintBackupSignature ||
                        guid != AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data)) ||
                        width != data.alphamapWidth || height != data.alphamapHeight || layers != data.alphamapLayers)
                        throw new InvalidOperationException("Paint backup does not match this terrain data and layer resolution.");
                    paint = new float[height, width, layers];
                    for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
                        for (int l = 0; l < layers; l++)
                        {
                            float value = reader.ReadSingle();
                            if (float.IsNaN(value) || value < 0f || value > 1f)
                                throw new InvalidDataException("Paint backup contains invalid layer weights.");
                            paint[z, x, l] = value;
                        }
                }
                string safetyPath = SavePaintBackup(data, state.terrain.name, data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight));
                Undo.RecordObject(road, "Update road paint backup reference");
                Undo.RegisterCompleteObjectUndo(data, "Restore road paint backup");
                data.SetAlphamaps(0, 0, paint);
                state.lastPaintBackupPath = safetyPath;
                state.terrain.Flush();
                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(road);
                EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                AssetDatabase.SaveAssets();
                Debug.Log("Restored paint on " + state.terrain.name + ". The paint from before this restore was backed up to " + safetyPath, road);
            }
            catch (Exception error) { Debug.LogError("Road paint restore: " + error.Message, road); }
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
        static void Raster(Terrain terrain, List<Segment> segments, int width, int height,
            float halfWidth, float feather, bool texelCenters, Action<int, int, float, float> apply)
        {
            var distance = new float[height, width];
            var elevation = new float[height, width];
            float radius = halfWidth + feather;
            Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
            float dx = size.x / (texelCenters ? width : width - 1), dz = size.z / (texelCenters ? height : height - 1);
            float offset = texelCenters ? 0.5f : 0f;
            // A paint texel can straddle the edge even when its center is outside.
            float halfDiagonal = texelCenters ? 0.5f * Mathf.Sqrt(dx * dx + dz * dz) : 0f;
            float searchRadius = radius + halfDiagonal;
            var nearestSegment = texelCenters ? new int[height, width] : null;
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++) distance[z, x] = searchRadius * searchRadius;
            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                var seg = segments[segmentIndex];
                Vector3 a = seg.a - origin, b = seg.b - origin;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - searchRadius) / dx - offset));
                int x1 = Mathf.Min(width - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + searchRadius) / dx - offset));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, b.z) - searchRadius) / dz - offset));
                int z1 = Mathf.Min(height - 1, Mathf.CeilToInt((Mathf.Max(a.z, b.z) + searchRadius) / dz - offset));
                Vector2 direction = new Vector2(b.x - a.x, b.z - a.z);
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                {
                    Vector2 relative = new Vector2((x + offset) * dx - a.x, (z + offset) * dz - a.z);
                    float t = direction.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector2.Dot(relative, direction) / direction.sqrMagnitude) : 0f;
                    float d = (relative - direction * t).sqrMagnitude;
                    if (d >= distance[z, x]) continue;
                    distance[z, x] = d;
                    elevation[z, x] = Mathf.Lerp(seg.a.y, seg.b.y, t);
                    if (texelCenters) nearestSegment[z, x] = segmentIndex;
                }
            }
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                float d = Mathf.Sqrt(distance[z, x]);
                if (d >= searchRadius) continue;
                float weight = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((d - halfWidth) / feather));
                if (texelCenters && d > halfWidth - halfDiagonal)
                {
                    // Integrate 16 coverage samples at the edge. Sampling the
                    // neighboring spline segments keeps curves smooth as well.
                    weight = 0f;
                    int nearest = nearestSegment[z, x];
                    for (int sz = 0; sz < 4; sz++) for (int sx = 0; sx < 4; sx++)
                    {
                        Vector2 point = new Vector2(origin.x + (x + (sx + 0.5f) / 4f) * dx,
                            origin.z + (z + (sz + 0.5f) / 4f) * dz);
                        float sampleDistance = float.PositiveInfinity;
                        for (int index = Mathf.Max(0, nearest - 2); index <= Mathf.Min(segments.Count - 1, nearest + 2); index++)
                            sampleDistance = Mathf.Min(sampleDistance, DistanceToSegmentXZ(point, segments[index]));
                        weight += 1f - Mathf.SmoothStep(0f, 1f,
                            Mathf.Clamp01((sampleDistance - halfWidth) / feather));
                    }
                    weight *= 1f / 16f;
                }
                if (weight <= 0f) continue;
                apply(x, z, elevation[z, x], weight);
            }
        }

        static float DistanceToSegmentXZ(Vector2 point, Segment segment)
        {
            Vector2 a = new Vector2(segment.a.x, segment.a.z);
            Vector2 direction = new Vector2(segment.b.x - segment.a.x, segment.b.z - segment.a.z);
            float t = direction.sqrMagnitude > 0.000001f
                ? Mathf.Clamp01(Vector2.Dot(point - a, direction) / direction.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + direction * t);
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
                road.terrains = Terrain.activeTerrains;
                EditorUtility.SetDirty(road);
                EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
                Debug.Log("RoadTest is ready: select it and use Rebuild Passageways + Road Paint. Terrain is unchanged until you bake.", road);
            }
        }
    }
}
