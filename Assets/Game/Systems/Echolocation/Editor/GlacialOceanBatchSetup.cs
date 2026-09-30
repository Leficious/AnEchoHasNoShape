using System;
using System.Collections.Generic;
using AnEchoHasNoShape.Echolocation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnEchoHasNoShape.Editor
{
    [InitializeOnLoad]
    internal static class GlacialOceanBatchSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/Main.unity";
        private const string PhysicalGroupName = "01 Physical + Echo + Collision";
        private const string EchoOnlyGroupName = "02 Invisible + Echo + Collision";
        private const string FalseGroupName = "03 Visible + No Echo + No Collision";
        private const int ShuffleSeed = 73241;

        static GlacialOceanBatchSetup()
        {
            EditorApplication.delayCall += TryRunOnce;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path == ScenePath)
            {
                EditorApplication.delayCall += TryRunOnce;
            }
        }

        [MenuItem("Tools/An Echo Has No Shape/Run Glacial Ocean Batch Setup")]
        private static void RunFromMenu()
        {
            RunSetup(true);
        }

        public static void RunBatchFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RunSetup(true);
        }

        private static void TryRunOnce()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryRunOnce;
                return;
            }

            RunSetup(false);
        }

        private static void RunSetup(bool reportMissingObjects)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                return;
            }

            Transform glacialOcean = FindInScene(scene, "GlacialOcean");
            Transform icebergs = glacialOcean != null ? glacialOcean.Find("Icebergs") : null;
            Transform sourceA = glacialOcean != null ? glacialOcean.Find("SirenSourceA") : null;
            Transform sourceB = glacialOcean != null ? glacialOcean.Find("SirenSourceB") : null;
            Transform trigger = glacialOcean != null ? glacialOcean.Find("SirenTrigger") : null;

            if (glacialOcean == null || icebergs == null || sourceA == null || sourceB == null || trigger == null)
            {
                if (reportMissingObjects)
                {
                    Debug.LogError("Glacial Ocean setup needs GlacialOcean/Icebergs, SirenSourceA, SirenSourceB, and SirenTrigger.");
                }
                return;
            }

            // The siren component is also the persistent completion marker.
            SirenEchoZone existingZone = trigger.GetComponent<SirenEchoZone>();
            if (existingZone != null)
            {
                return;
            }

            List<Transform> icebergPieces = new List<Transform>();
            for (int index = 0; index < icebergs.childCount; index++)
            {
                Transform child = icebergs.GetChild(index);
                if (child.name != PhysicalGroupName &&
                    child.name != EchoOnlyGroupName &&
                    child.name != FalseGroupName)
                {
                    icebergPieces.Add(child);
                }
            }

            if (icebergPieces.Count == 0)
            {
                Debug.LogError("No direct iceberg children were found beneath GlacialOcean/Icebergs.");
                return;
            }

            Shuffle(icebergPieces, ShuffleSeed);

            IcebergStateGroup physicalGroup = CreateStateGroup(
                icebergs,
                PhysicalGroupName,
                IcebergEchoState.PhysicalEchoable,
                false);
            IcebergStateGroup echoOnlyGroup = CreateStateGroup(
                icebergs,
                EchoOnlyGroupName,
                IcebergEchoState.InvisibleEchoable,
                false);
            IcebergStateGroup falseGroup = CreateStateGroup(
                icebergs,
                FalseGroupName,
                IcebergEchoState.VisibleFalse,
                true);

            IcebergStateGroup[] groups = { physicalGroup, echoOnlyGroup, falseGroup };
            int[] counts = new int[groups.Length];

            for (int index = 0; index < icebergPieces.Count; index++)
            {
                int stateIndex = index % groups.Length;
                Transform piece = icebergPieces[index];
                Undo.SetTransformParent(piece, groups[stateIndex].transform, "Assign Iceberg Echo State");
                counts[stateIndex]++;
            }

            foreach (IcebergStateGroup group in groups)
            {
                group.ApplyState();
                RecordChildOverrides(group.transform);
                EditorUtility.SetDirty(group);
            }

            SirenEchoZone zone = Undo.AddComponent<SirenEchoZone>(trigger.gameObject);
            zone.Configure(sourceA, sourceB, icebergs);
            EditorUtility.SetDirty(zone);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"Glacial Ocean setup complete: {counts[0]} physical/echoable, " +
                $"{counts[1]} invisible/echoable, {counts[2]} visible/false icebergs. " +
                "SirenSourceA and SirenSourceB are wired to SirenTrigger.");
        }

        private static IcebergStateGroup CreateStateGroup(
            Transform parent,
            string groupName,
            IcebergEchoState state,
            bool ignoreEcho)
        {
            Transform existing = parent.Find(groupName);
            GameObject groupObject;

            if (existing != null)
            {
                groupObject = existing.gameObject;
            }
            else
            {
                groupObject = new GameObject(groupName);
                Undo.RegisterCreatedObjectUndo(groupObject, "Create Iceberg State Group");
                groupObject.transform.SetParent(parent, false);
            }

            IcebergStateGroup group = groupObject.GetComponent<IcebergStateGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<IcebergStateGroup>(groupObject);
            }

            group.Configure(state);

            if (ignoreEcho && groupObject.GetComponent<IgnoreEcholocation>() == null)
            {
                Undo.AddComponent<IgnoreEcholocation>(groupObject);
            }

            return group;
        }

        private static void RecordChildOverrides(Transform group)
        {
            foreach (MeshRenderer meshRenderer in group.GetComponentsInChildren<MeshRenderer>(true))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(meshRenderer);
                EditorUtility.SetDirty(meshRenderer);
            }

            foreach (Collider childCollider in group.GetComponentsInChildren<Collider>(true))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(childCollider);
                EditorUtility.SetDirty(childCollider);
            }
        }

        private static Transform FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindRecursive(root.transform, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindRecursive(Transform current, string objectName)
        {
            if (current.name == objectName)
            {
                return current;
            }

            for (int index = 0; index < current.childCount; index++)
            {
                Transform found = FindRecursive(current.GetChild(index), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void Shuffle<T>(IList<T> values, int seed)
        {
            System.Random random = new System.Random(seed);
            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }
    }
}
