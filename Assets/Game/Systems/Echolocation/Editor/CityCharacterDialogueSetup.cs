using AnEchoHasNoShape.Echolocation;
using AnEchoHasNoShape.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnEchoHasNoShape.Editor
{
    // Migrate the open scene rather than replacing its file and losing unsaved level edits.
    [InitializeOnLoad]
    internal static class CityCharacterDialogueSetup
    {
        static CityCharacterDialogueSetup()
        {
            EditorApplication.delayCall += EnsureDialogue;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += EnsureDialogue;
            };
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += EnsureDialogue;
        }

        [MenuItem("Tools/An Echo Has No Shape/Set Up Editable City Dialogue")]
        private static void EnsureDialogue()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureDialogue;
                return;
            }

            string[] names = { "Muse", "Ruler", "Architect" };
            int changed = 0;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded || scene.path != "Assets/Game/Scenes/Main.unity") continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    int index = System.Array.FindIndex(names,
                        name => string.Equals(name, child.name, System.StringComparison.OrdinalIgnoreCase));
                    if (index < 0) continue;
                    // Also repair an already-open scene, which may predate the on-disk migration.
                    EchoRevealState reveal = child.GetComponent<EchoRevealState>();
                    if (reveal == null)
                    {
                        reveal = Undo.AddComponent<EchoRevealState>(child.gameObject);
                        reveal.ConfigureWindow(10f, 0.9f);
                        EditorUtility.SetDirty(reveal);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(reveal);
                        EditorSceneManager.MarkSceneDirty(scene);
                        changed++;
                    }
                    if (child.GetComponent<EchoInteractionGate>() == null)
                    {
                        Undo.AddComponent<EchoInteractionGate>(child.gameObject);
                        EditorSceneManager.MarkSceneDirty(scene);
                        changed++;
                    }
                    CityCharacterInteractable dialogue = child.GetComponent<CityCharacterInteractable>();
                    if (dialogue != null && dialogue.DialogueInitialized) continue;
                    if (dialogue == null)
                        dialogue = Undo.AddComponent<CityCharacterInteractable>(child.gameObject);
                    Undo.RecordObject(dialogue, "Initialize city character dialogue");
                    dialogue.InitializeDialogue(index);
                    EditorUtility.SetDirty(dialogue);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(dialogue);
                    EditorSceneManager.MarkSceneDirty(scene);
                    changed++;
                }
            }

            if (changed > 0)
                Debug.Log("Updated city character dialogue/reveal components. Edit Passage or Echo Reveal State on the characters, then save the scene.");
        }
    }
}
