using AnEchoHasNoShape.Echolocation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace AnEchoHasNoShape.Editor
{
    [InitializeOnLoad]
    internal static class CitySplineWalkersSetup
    {
        static CitySplineWalkersSetup()
        {
            EditorApplication.delayCall += SetUp;
            EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += SetUp;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += SetUp;
            };
        }
        private static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += SetUp;
                return;
            }
            SplineContainer[] splines = Object.FindObjectsByType<SplineContainer>(FindObjectsInactive.Include);
            CitySplineWalkers template = null;
            foreach (SplineContainer spline in splines)
                if (spline.gameObject.scene.path == "Assets/Game/Scenes/Main.unity" &&
                    GlobalObjectId.GetGlobalObjectIdSlow(spline).targetObjectId == 202796064)
                    template = spline.GetComponent<CitySplineWalkers>();
            // One-time migration of the newly authored paths in an already-open scene.
            // Copy the live template so unsaved Inspector tuning takes precedence.
            ulong[] addedPaths = { 150710693, 474338285, 555892903, 1034032282,
                1058374297, 1518659211, 1605358029, 1687062556, 1791049076 };
            foreach (SplineContainer spline in splines)
            {
                if (spline.gameObject.scene.path != "Assets/Game/Scenes/Main.unity") continue;
                ulong id = GlobalObjectId.GetGlobalObjectIdSlow(spline).targetObjectId;
                bool copyTemplate = System.Array.IndexOf(addedPaths, id) >= 0;
                if (!copyTemplate && id != 202796064 && id != 903752001) continue;
                if (copyTemplate && template == null) continue;
                if (spline.TryGetComponent(out CitySplineWalkers existing))
                {
                    AssignCharacters(existing);
                    continue;
                }
                CitySplineWalkers walkers = Undo.AddComponent<CitySplineWalkers>(spline.gameObject);
                if (copyTemplate)
                {
                    Undo.RecordObject(walkers, "Copy city walker settings");
                    EditorUtility.CopySerialized(template, walkers);
                    EditorUtility.SetDirty(walkers);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(walkers);
                }
                AssignCharacters(walkers);
                EditorSceneManager.MarkSceneDirty(spline.gameObject.scene);
            }
        }

        private static void AssignCharacters(CitySplineWalkers walkers)
        {
            SerializedObject data = new SerializedObject(walkers);
            if (data.FindProperty("characterPrefabsConfigured").boolValue) return;
            const string folder = "Assets/_PEASANTS/Lowpoly_Characters/Prefabs/Modular_NPC/Peasants_Citizens/Sets/";
            string[] names = { "PT_Female_Peasant_01_a", "PT_Boy_Peasant_01", "PT_Male_Peasant_01" };
            var prefabs = new GameObject[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(folder + names[i] + ".prefab");
                if (prefabs[i] == null) return;
            }
            Undo.RecordObject(walkers, "Assign city ghost character prefabs");
            SerializedProperty list = data.FindProperty("characterPrefabs");
            // Preserve any manually assigned model choices.
            if (list.arraySize == 0)
            {
                list.arraySize = prefabs.Length;
                for (int i = 0; i < prefabs.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            }
            data.FindProperty("characterPrefabsConfigured").boolValue = true;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(walkers);
            EditorSceneManager.MarkSceneDirty(walkers.gameObject.scene);
        }
    }
}
