using System.Collections.Generic;
using AnEchoHasNoShape.Echolocation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnEchoHasNoShape.Editor
{
    [InitializeOnLoad]
    [CustomEditor(typeof(CityVisibility))]
    internal sealed class CityVisibilityEditor : UnityEditor.Editor
    {
        static CityVisibilityEditor()
        {
            EditorApplication.delayCall += SetUpOpenScenes;
            EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += SetUpOpenScenes;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += SetUpOpenScenes;
            };
        }

        private static void SetUpOpenScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += SetUpOpenScenes;
                return;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.path != "Assets/Game/Scenes/Main.unity") continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != "UnrememberedCity") continue;
                    CityVisibility visibility = child.GetComponent<CityVisibility>();
                    if (visibility == null)
                    {
                        visibility = Undo.AddComponent<CityVisibility>(child.gameObject);
                        Capture(visibility);
                        Debug.Log("City visibility captured. Hidden meshes are now visible for set dressing; save Main to preserve the runtime hide list.", visibility);
                    }
                    else ShowForEditing(visibility);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Listed meshes are visible in Edit Mode and hidden in Play Mode. Echoes still reveal them. To add meshes, disable their Mesh Renderers and click Capture Disabled Meshes. Inactive GameObjects are not activated.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Capture Disabled Meshes")) Capture((CityVisibility)target);
                if (GUILayout.Button("Show Captured Meshes for Editing")) ShowForEditing((CityVisibility)target);
            }
        }

        private static void Capture(CityVisibility visibility)
        {
            var sources = new List<MeshRenderer>(visibility.HiddenAtRuntime);
            foreach (MeshRenderer source in visibility.GetComponentsInChildren<MeshRenderer>(true))
                if (!IsCityBoundary(source) && !source.enabled && !sources.Contains(source) && (source.hideFlags & HideFlags.DontSave) == 0)
                    sources.Add(source);
            Undo.RecordObject(visibility, "Capture city runtime visibility");
            SerializedObject data = new SerializedObject(visibility);
            SerializedProperty list = data.FindProperty("hiddenAtRuntime");
            list.arraySize = sources.Count;
            for (int i = 0; i < sources.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sources[i];
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(visibility);
            EditorSceneManager.MarkSceneDirty(visibility.gameObject.scene);
            ShowForEditing(visibility);
        }

        private static void ShowForEditing(CityVisibility visibility)
        {
            bool removeBoundaries = false;
            foreach (MeshRenderer source in visibility.HiddenAtRuntime)
            {
                if (source == null || !source.transform.IsChildOf(visibility.transform)) continue;
                bool boundary = IsCityBoundary(source);
                removeBoundaries |= boundary;
                // Undo the old preview's accidental enabling of captured trigger meshes.
                // Only their renderer changes; colliders and zone logic stay active.
                bool show = !boundary;
                if (source.enabled == show) continue;
                Undo.RecordObject(source, "Show city mesh for editing");
                source.enabled = show;
                PrefabUtility.RecordPrefabInstancePropertyModifications(source);
                EditorUtility.SetDirty(source);
                EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
            }
            if (removeBoundaries)
            {
                Undo.RecordObject(visibility, "Exclude city boundary volumes from preview");
                SerializedObject data = new SerializedObject(visibility);
                SerializedProperty list = data.FindProperty("hiddenAtRuntime");
                var retained = new List<MeshRenderer>();
                foreach (MeshRenderer source in visibility.HiddenAtRuntime)
                    if (source != null && !IsCityBoundary(source)) retained.Add(source);
                list.arraySize = retained.Count;
                for (int i = 0; i < retained.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = retained[i];
                data.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(visibility);
                EditorSceneManager.MarkSceneDirty(visibility.gameObject.scene);
            }
        }

        private static bool IsCityBoundary(MeshRenderer source)
        {
            for (Transform node = source.transform; node != null; node = node.parent)
            {
                string name = node.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                if (name == "cityinner" || name == "cityinnerbounds" ||
                    name == "cityouter" || name == "cityouterbounds") return true;
            }
            return false;
        }
    }
}
