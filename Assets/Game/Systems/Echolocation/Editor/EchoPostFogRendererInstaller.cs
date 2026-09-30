using System.Linq;
using AnEchoHasNoShape.Echolocation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AnEchoHasNoShape.Editor
{
    [InitializeOnLoad]
    internal static class EchoPostFogRendererInstaller
    {
        private const string RendererPath = "Assets/Game/Config/Rendering/PC_Renderer.asset";

        static EchoPostFogRendererInstaller()
        {
            EditorApplication.delayCall += EnsureInstalled;
        }

        private static void EnsureInstalled()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureInstalled;
                return;
            }

            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);

            if (rendererData == null ||
                rendererData.rendererFeatures.OfType<EchoPostFogRendererFeature>().Any())
            {
                return;
            }

            EchoPostFogRendererFeature feature =
                ScriptableObject.CreateInstance<EchoPostFogRendererFeature>();
            feature.name = "Echo Post Fog";
            feature.hideFlags |= HideFlags.HideInHierarchy;

            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();

            rendererData.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
        }
    }
}
