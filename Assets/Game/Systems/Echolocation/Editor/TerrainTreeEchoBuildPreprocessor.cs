#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AnEchoHasNoShape.Echolocation.Editor
{
    /// <summary>
    /// TerrainTreeEchoProxy combines authored tree meshes at runtime. Mesh CPU
    /// access is unrestricted in the Editor but must be enabled in a player, so
    /// prepare only model assets referenced by Terrain tree prototypes.
    /// </summary>
    public sealed class TerrainTreeEchoBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            HashSet<string> modelPaths = CollectTerrainTreeModelPaths();
            int changedCount = 0;

            foreach (string modelPath in modelPaths)
            {
                if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
                {
                    continue;
                }

                bool needsReimport = false;

                // Unity 6 no longer supports the old External material-location
                // importer mode used by this asset pack. InPrefab preserves the
                // explicit externalObjects material remaps while avoiding the
                // obsolete-importer warning on future tree preparation.
#pragma warning disable 0618
                if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    needsReimport = true;
                }
#pragma warning restore 0618

                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    needsReimport = true;
                }

                if (!needsReimport)
                {
                    continue;
                }

                importer.SaveAndReimport();
                changedCount++;
            }

            if (changedCount > 0)
            {
                Debug.Log($"Enabled Read/Write on {changedCount} terrain-tree model asset(s) for echo proxies.");
            }
        }

        private static HashSet<string> CollectTerrainTreeModelPaths()
        {
            HashSet<string> modelPaths = new HashSet<string>();
            string[] terrainDataGuids = AssetDatabase.FindAssets("t:TerrainData");

            foreach (string terrainDataGuid in terrainDataGuids)
            {
                string terrainDataPath = AssetDatabase.GUIDToAssetPath(terrainDataGuid);
                TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
                if (terrainData == null)
                {
                    continue;
                }

                foreach (TreePrototype prototype in terrainData.treePrototypes)
                {
                    GameObject prefab = prototype.prefab;
                    if (prefab == null)
                    {
                        continue;
                    }

                    foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        Mesh mesh = filter.sharedMesh;
                        string meshPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : string.Empty;
                        if (!string.IsNullOrEmpty(meshPath) && AssetImporter.GetAtPath(meshPath) is ModelImporter)
                        {
                            modelPaths.Add(meshPath);
                        }
                    }
                }
            }

            return modelPaths;
        }
    }
}
#endif
