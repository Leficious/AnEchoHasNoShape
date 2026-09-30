#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using AnEchoHasNoShape.GroundBlend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnEchoHasNoShape.EditorTools
{
    public static class GroundBlendTerrainBaker
    {
        private const int BakeResolution = 1024;
        private const string DefaultDirectory = "Assets/Game/Systems/GroundBlend/Generated";
        private const string LegacyRepairSessionKey = "AnEchoHasNoShape.GroundBlend.LegacyRepairV2";
        private const string RemovalSessionKey = "AnEchoHasNoShape.GroundBlend.SceneRemovalV3";

        [InitializeOnLoadMethod]
        private static void ScheduleLegacyMaterialRepair()
        {
            if (SessionState.GetBool(LegacyRepairSessionKey, false)) return;
            SessionState.SetBool(LegacyRepairSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    RestoreConvertedMaterialsInScene();
            };
        }

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedRemoval()
        {
            if (SessionState.GetBool(RemovalSessionKey, false)) return;
            SessionState.SetBool(RemovalSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    RemoveGroundBlendFromOpenScene();
            };
        }

        [MenuItem("Tools/An Echo Has No Shape/Ground Blend/Bake Terrain Color Map (1024)")]
        private static void BakeTerrainColorMap()
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>();
            if (terrains.Length == 0)
            {
                EditorUtility.DisplayDialog("Ground Blend", "No active Terrain objects were found in this scene.", "OK");
                return;
            }

            Directory.CreateDirectory(DefaultDirectory);
            string outputPath = AssetDatabase.GenerateUniqueAssetPath(DefaultDirectory + "/TerrainGroundMap.png");

            CalculateWorldBounds(terrains, out Vector2 worldMinimum, out Vector2 worldSize);
            Color32[] pixels = new Color32[BakeResolution * BakeResolution];
            Dictionary<Texture2D, Texture2D> readableTextures = new Dictionary<Texture2D, Texture2D>();
            BakedTerrainSource[] terrainSources = new BakedTerrainSource[terrains.Length];
            for (int i = 0; i < terrains.Length; i++)
            {
                terrainSources[i] = new BakedTerrainSource(terrains[i]);
            }

            try
            {
                for (int y = 0; y < BakeResolution; y++)
                {
                    float worldZ = worldMinimum.y + ((y + 0.5f) / BakeResolution) * worldSize.y;
                    for (int x = 0; x < BakeResolution; x++)
                    {
                        float worldX = worldMinimum.x + ((x + 0.5f) / BakeResolution) * worldSize.x;
                        pixels[y * BakeResolution + x] = BakeWorldPixel(
                            terrainSources,
                            worldX,
                            worldZ,
                            readableTextures);
                    }

                    if ((y & 31) == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Baking Ground Blend Map",
                            "Combining painted terrain layers...",
                            y / (float)BakeResolution);
                    }
                }

                Texture2D result = new Texture2D(BakeResolution, BakeResolution, TextureFormat.RGBA32, false, false);
                result.SetPixels32(pixels);
                result.Apply(false, false);
                File.WriteAllBytes(outputPath, result.EncodeToPNG());
                Object.DestroyImmediate(result);

                AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(outputPath) is TextureImporter importer)
                {
                    importer.sRGBTexture = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = true;
                    importer.maxTextureSize = BakeResolution;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }

                Texture2D bakedMap = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                GroundBlendGlobals globals = EnsureGlobalsComponent();
                Undo.RecordObject(globals, "Assign ground blend terrain map");
                globals.Configure(bakedMap, worldMinimum, worldSize);
                EditorUtility.SetDirty(globals);
                EditorSceneManager.MarkSceneDirty(globals.gameObject.scene);

                Selection.activeObject = bakedMap;
                EditorGUIUtility.PingObject(bakedMap);
                Debug.Log($"Ground blend map baked from {terrains.Length} terrain(s): {outputPath}", globals);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                foreach (Texture2D texture in readableTextures.Values)
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        [MenuItem("Tools/An Echo Has No Shape/Ground Blend/Enable Overlay On Selected")]
        private static void EnableOverlayOnSelected()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Ground Blend", "Select one or more scene props first.", "OK");
                return;
            }

            int objectCount = 0;

            foreach (GameObject root in selected)
            {
                GroundBlendObject blendObject = root.GetComponent<GroundBlendObject>();
                if (blendObject == null)
                {
                    blendObject = Undo.AddComponent<GroundBlendObject>(root);
                }
                blendObject.Apply();
                EditorUtility.SetDirty(blendObject);
                objectCount++;
            }

            EditorSceneManager.MarkSceneDirty(selected[0].scene);
            Debug.Log($"Ground blend overlay enabled on {objectCount} selected object(s). Existing material assignments were preserved.");
        }

        [MenuItem("Tools/An Echo Has No Shape/Ground Blend/Restore Converted Materials In Scene")]
        private static void RestoreConvertedMaterialsInScene()
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            Dictionary<string, Material> originals = new Dictionary<string, Material>();
            int restoredSlots = 0;

            foreach (Renderer renderer in renderers)
            {
                if ((renderer.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;
                Material[] assigned = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < assigned.Length; i++)
                {
                    Material generated = assigned[i];
                    if (generated == null) continue;
                    string generatedPath = AssetDatabase.GetAssetPath(generated);
                    if (!generatedPath.StartsWith(DefaultDirectory + "/Materials/")) continue;

                    string generatedName = Path.GetFileNameWithoutExtension(generatedPath);
                    int suffixIndex = generatedName.IndexOf("_GroundBlend", System.StringComparison.Ordinal);
                    if (suffixIndex <= 0) continue;
                    string originalName = generatedName.Substring(0, suffixIndex);

                    if (!originals.TryGetValue(originalName, out Material original))
                    {
                        original = FindOriginalMaterial(originalName);
                        originals[originalName] = original;
                    }
                    if (original == null) continue;

                    Undo.RecordObject(renderer, "Restore original ground blend material");
                    assigned[i] = original;
                    changed = true;
                    restoredSlots++;
                }

                if (changed)
                {
                    renderer.sharedMaterials = assigned;
                    EditorUtility.SetDirty(renderer);
                }
            }

            if (restoredSlots > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                Debug.Log($"Restored {restoredSlots} material slot(s). Ground blending now uses overlays and leaves the original shaders intact.");
            }
            else
            {
                Debug.Log("No generated ground-blend material assignments were found in the open scene.");
            }
        }

        private static Material FindOriginalMaterial(string exactName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:Material {exactName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(DefaultDirectory + "/Materials/")) continue;
                if (Path.GetFileNameWithoutExtension(path) == exactName)
                    return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return null;
        }

        [MenuItem("Tools/An Echo Has No Shape/Ground Blend/Remove Ground Blend From Open Scene")]
        private static void RemoveGroundBlendFromOpenScene()
        {
            RestoreConvertedMaterialsInScene();

            GroundBlendObject[] blendObjects = Object.FindObjectsByType<GroundBlendObject>(FindObjectsInactive.Include);
            GroundBlendGlobals[] globals = Object.FindObjectsByType<GroundBlendGlobals>(FindObjectsInactive.Include);
            int removedComponents = 0;

            foreach (GroundBlendObject blendObject in blendObjects)
            {
                if (blendObject == null || !blendObject.gameObject.scene.IsValid()) continue;
                Undo.DestroyObjectImmediate(blendObject);
                removedComponents++;
            }

            foreach (GroundBlendGlobals globalSettings in globals)
            {
                if (globalSettings == null || !globalSettings.gameObject.scene.IsValid()) continue;
                Undo.DestroyObjectImmediate(globalSettings);
                removedComponents++;
            }

            if (removedComponents > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
            }

            Debug.Log($"Ground blend removed from the open scene. Restored original renderer materials and removed {removedComponents} ground-blend component(s).");
        }

        private static GroundBlendGlobals EnsureGlobalsComponent()
        {
            GroundBlendGlobals globals = Object.FindAnyObjectByType<GroundBlendGlobals>();
            if (globals != null) return globals;

            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            GameObject host = manager != null ? manager.gameObject : new GameObject("Ground Blend Globals");
            return Undo.AddComponent<GroundBlendGlobals>(host);
        }

        private static void CalculateWorldBounds(Terrain[] terrains, out Vector2 minimum, out Vector2 size)
        {
            float minX = float.PositiveInfinity;
            float minZ = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxZ = float.NegativeInfinity;

            foreach (Terrain terrain in terrains)
            {
                Vector3 position = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                minX = Mathf.Min(minX, position.x);
                minZ = Mathf.Min(minZ, position.z);
                maxX = Mathf.Max(maxX, position.x + terrainSize.x);
                maxZ = Mathf.Max(maxZ, position.z + terrainSize.z);
            }

            minimum = new Vector2(minX, minZ);
            size = new Vector2(maxX - minX, maxZ - minZ);
        }

        private static Color32 BakeWorldPixel(
            BakedTerrainSource[] terrains,
            float worldX,
            float worldZ,
            Dictionary<Texture2D, Texture2D> readableTextures)
        {
            foreach (BakedTerrainSource terrain in terrains)
            {
                Vector3 position = terrain.Position;
                TerrainData data = terrain.Data;
                Vector3 size = data.size;
                if (worldX < position.x || worldX > position.x + size.x ||
                    worldZ < position.z || worldZ > position.z + size.z)
                {
                    continue;
                }

                float normalizedX = Mathf.InverseLerp(position.x, position.x + size.x, worldX);
                float normalizedZ = Mathf.InverseLerp(position.z, position.z + size.z, worldZ);
                int alphaX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
                int alphaY = Mathf.Clamp(Mathf.RoundToInt(normalizedZ * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);

                TerrainLayer[] layers = terrain.Layers;
                Color color = Color.black;
                float totalWeight = 0f;
                int layerCount = Mathf.Min(layers.Length, terrain.Weights.GetLength(2));

                for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
                {
                    float weight = terrain.Weights[alphaY, alphaX, layerIndex];
                    TerrainLayer layer = layers[layerIndex];
                    if (weight <= 0.0001f || layer == null || layer.diffuseTexture == null)
                    {
                        continue;
                    }

                    Texture2D readable = GetReadableTexture(layer.diffuseTexture, readableTextures);
                    Vector2 tileSize = layer.tileSize;
                    Vector2 tileOffset = layer.tileOffset;
                    float u = (worldX - position.x + tileOffset.x) / Mathf.Max(0.001f, tileSize.x);
                    float v = (worldZ - position.z + tileOffset.y) / Mathf.Max(0.001f, tileSize.y);
                    Color layerColor = readable.GetPixelBilinear(Mathf.Repeat(u, 1f), Mathf.Repeat(v, 1f));
                    layerColor.r *= layer.diffuseRemapMax.x;
                    layerColor.g *= layer.diffuseRemapMax.y;
                    layerColor.b *= layer.diffuseRemapMax.z;
                    color += layerColor * weight;
                    totalWeight += weight;
                }

                if (totalWeight > 0.0001f)
                {
                    color /= totalWeight;
                }
                color.a = 1f;
                return color;
            }

            return new Color32(128, 128, 128, 255);
        }

        private sealed class BakedTerrainSource
        {
            public readonly TerrainData Data;
            public readonly Vector3 Position;
            public readonly TerrainLayer[] Layers;
            public readonly float[,,] Weights;

            public BakedTerrainSource(Terrain terrain)
            {
                Data = terrain.terrainData;
                Position = terrain.transform.position;
                Layers = Data.terrainLayers;
                Weights = Data.GetAlphamaps(0, 0, Data.alphamapWidth, Data.alphamapHeight);
            }
        }

        private static Texture2D GetReadableTexture(
            Texture2D source,
            Dictionary<Texture2D, Texture2D> readableTextures)
        {
            if (readableTextures.TryGetValue(source, out Texture2D readable))
            {
                return readable;
            }

            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, temporary);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temporary;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            readableTextures.Add(source, readable);
            return readable;
        }
    }
}
#endif
