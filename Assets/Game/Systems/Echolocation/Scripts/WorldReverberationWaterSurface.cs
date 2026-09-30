using UnityEngine;
using UnityEngine.Rendering;

namespace AnEchoHasNoShape.Echolocation
{
    /// <summary>
    /// Adds a world-reverberation-only flowing ripple overlay to water meshes. The
    /// original transparent water material remains untouched.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WorldReverberationWaterSurface : MonoBehaviour
    {
        private const string ShaderName = "An Echo Has No Shape/World Reverberation Water";
        private const string SurfaceShaderName = "An Echo Has No Shape/Visible Water Surface";

        [SerializeField, Min(0f)] private float surfaceOffset = 0.018f;
        [SerializeField, Range(0.05f, 2f)] private float rippleScale = 0.28f;
        [SerializeField, Range(0.25f, 3f)] private float rippleThickness = 1.1f;
        [SerializeField, Range(0f, 2f)] private float revealStrength = 0.72f;

        [Header("Normal Surface Visibility")]
        [SerializeField, Range(0f, 1f)] private float surfaceOpacity = 0.38f;
        [SerializeField] private Color surfaceColor = new Color(0.045f, 0.19f, 0.25f, 1f);
        [SerializeField] private Color surfaceHighlightColor = new Color(0.24f, 0.68f, 0.76f, 1f);
        [SerializeField, Range(0.02f, 1f)] private float surfaceWaveScale = 0.16f;
        [SerializeField, Range(0f, 0.5f)] private float surfaceWaveHeight = 0.085f;
        [SerializeField, Min(0f)] private float surfaceFogFadeStart = 22f;
        [SerializeField, Min(0.1f)] private float surfaceFogFadeEnd = 85f;

        private GameObject overlayObject;
        private Material overlayMaterial;
        private Material surfaceMaterial;

        private void Awake()
        {
            if (IgnoreEcholocation.IsIgnored(transform))
            {
                enabled = false;
                return;
            }

            Material shaderReference = Resources.Load<Material>("Rendering/WorldReverbWaterRuntime");
            Shader shader = shaderReference != null && shaderReference.shader != null
                ? shaderReference.shader
                : Shader.Find(ShaderName);

            if (shader == null)
            {
                Debug.LogError($"Water reverberation shader '{ShaderName}' could not be found.", this);
                enabled = false;
                return;
            }

            Mesh sourceMesh = GetComponent<MeshFilter>().sharedMesh;
            if (sourceMesh == null)
            {
                enabled = false;
                return;
            }

            Material surfaceShaderReference = Resources.Load<Material>(
                "Rendering/WaterSurfaceVisibilityRuntime");
            Shader surfaceShader = surfaceShaderReference != null && surfaceShaderReference.shader != null
                ? surfaceShaderReference.shader
                : Shader.Find(SurfaceShaderName);

            if (surfaceShader != null)
            {
                surfaceMaterial = new Material(surfaceShader)
                {
                    name = $"{gameObject.name} Visible Water Surface (Runtime)",
                    hideFlags = HideFlags.DontSave
                };
                surfaceMaterial.SetFloat("_SurfaceOffset", surfaceOffset * 0.55f);
                surfaceMaterial.SetFloat("_SurfaceOpacity", surfaceOpacity);
                surfaceMaterial.SetColor("_SurfaceColor", surfaceColor);
                surfaceMaterial.SetColor("_HighlightColor", surfaceHighlightColor);
                surfaceMaterial.SetFloat("_WaveScale", surfaceWaveScale);
                surfaceMaterial.SetFloat("_WaveHeight", surfaceWaveHeight);
                surfaceMaterial.SetFloat("_FogFadeStart", surfaceFogFadeStart);
                surfaceMaterial.SetFloat("_FogFadeEnd", surfaceFogFadeEnd);
                CreateOverlayRenderer("Visible Water Surface", sourceMesh, surfaceMaterial);
            }
            else
            {
                Debug.LogWarning($"Water surface shader '{SurfaceShaderName}' could not be found.", this);
            }

            overlayMaterial = new Material(shader)
            {
                name = $"{gameObject.name} World Reverb Water (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            overlayMaterial.SetFloat("_SurfaceOffset", surfaceOffset);
            overlayMaterial.SetFloat("_RippleScale", rippleScale);
            overlayMaterial.SetFloat("_RippleThickness", rippleThickness);
            overlayMaterial.SetFloat("_RevealStrength", revealStrength);

            overlayObject = new GameObject("World Reverberation Water Ripples")
            {
                hideFlags = HideFlags.DontSave,
                layer = gameObject.layer
            };
            overlayObject.transform.SetParent(transform, false);

            MeshFilter overlayFilter = overlayObject.AddComponent<MeshFilter>();
            overlayFilter.sharedMesh = sourceMesh;

            MeshRenderer overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
            overlayRenderer.sharedMaterial = overlayMaterial;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void CreateOverlayRenderer(string objectName, Mesh mesh, Material material)
        {
            GameObject surfaceObject = new GameObject(objectName)
            {
                hideFlags = HideFlags.DontSave,
                layer = gameObject.layer
            };
            surfaceObject.transform.SetParent(transform, false);

            MeshFilter filter = surfaceObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = surfaceObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void OnDestroy()
        {
            if (overlayMaterial != null)
            {
                Destroy(overlayMaterial);
            }

            if (surfaceMaterial != null)
            {
                Destroy(surfaceMaterial);
            }
        }

        private void OnValidate()
        {
            surfaceOffset = Mathf.Max(0f, surfaceOffset);
            rippleScale = Mathf.Clamp(rippleScale, 0.05f, 2f);
            rippleThickness = Mathf.Clamp(rippleThickness, 0.25f, 3f);
            revealStrength = Mathf.Clamp(revealStrength, 0f, 2f);
            surfaceOpacity = Mathf.Clamp01(surfaceOpacity);
            surfaceWaveScale = Mathf.Clamp(surfaceWaveScale, 0.02f, 1f);
            surfaceWaveHeight = Mathf.Clamp(surfaceWaveHeight, 0f, 0.5f);
            surfaceFogFadeStart = Mathf.Max(0f, surfaceFogFadeStart);
            surfaceFogFadeEnd = Mathf.Max(surfaceFogFadeStart + 0.1f, surfaceFogFadeEnd);
        }
    }
}
