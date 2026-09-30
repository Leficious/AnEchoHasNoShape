using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AnEchoHasNoShape.FogLighting
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class FogLightVolume : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int VolumeCenterId = Shader.PropertyToID("_VolumeCenter");
        private static readonly int VolumeRadiusId = Shader.PropertyToID("_VolumeRadius");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
        private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
        private static readonly int DepthOffsetId = Shader.PropertyToID("_DepthOffset");

        [Header("Fog Volume")]
        [Min(0.01f)]
        [SerializeField] private float radiusMultiplier = 1f;

        [Range(0f, 1.5f)]
        [SerializeField] private float density = 0.22f;

        [Range(0f, 4f)]
        [SerializeField] private float brightness = 0.8f;

        [Range(0.25f, 6f)]
        [SerializeField] private float radialFalloff = 1.8f;

        [Header("Movement")]
        [Range(0f, 1f)]
        [SerializeField] private float noiseAmount = 0.22f;

        [Range(0.05f, 3f)]
        [SerializeField] private float noiseScale = 0.35f;

        [Range(0f, 2f)]
        [SerializeField] private float noiseSpeed = 0.08f;

        [Header("Intersection")]
        [Tooltip("Keeps the fog volume just in front of opaque surfaces to avoid flickering.")]
        [Range(0f, 0.25f)]
        [SerializeField] private float depthOffset = 0.025f;

        private Light sourceLight;
        private GameObject volumeObject;
        private MeshRenderer volumeRenderer;
        private Material volumeMaterial;
        private MaterialPropertyBlock properties;
        private bool missingShaderReported;

        private void OnEnable()
        {
            sourceLight = GetComponent<Light>();
            EnsureVolume();
            EnsureCameraDepthTextures();
            UpdateVolume();
        }

        private void LateUpdate()
        {
            EnsureVolume();
            UpdateVolume();
        }

        private void OnValidate()
        {
            radiusMultiplier = Mathf.Max(0.01f, radiusMultiplier);
            sourceLight = GetComponent<Light>();

            if (isActiveAndEnabled)
            {
                EnsureVolume();
                UpdateVolume();
            }
        }

        private void OnDisable()
        {
            ReleaseVolume();
        }

        private void OnDestroy()
        {
            ReleaseVolume();
        }

        private void EnsureVolume()
        {
            if (volumeObject != null && volumeRenderer != null && volumeMaterial != null)
            {
                return;
            }

            Material shaderReference = Resources.Load<Material>("Rendering/FogLightVolumeRuntime");
            Shader shader = shaderReference != null && shaderReference.shader != null
                ? shaderReference.shader
                : Shader.Find("Hidden/An Echo Has No Shape/Fog Light Volume");
            if (shader == null)
            {
                if (!missingShaderReported)
                {
                    Debug.LogError("Fog Light Volume shader could not be found.", this);
                    missingShaderReported = true;
                }

                return;
            }

            missingShaderReported = false;
            volumeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            volumeObject.name = "Fog Light Volume (Generated)";
            volumeObject.hideFlags = HideFlags.HideAndDontSave;
            volumeObject.transform.SetParent(transform, false);
            volumeObject.transform.localPosition = Vector3.zero;
            volumeObject.transform.localRotation = Quaternion.identity;

            Collider generatedCollider = volumeObject.GetComponent<Collider>();
            DestroyGeneratedObject(generatedCollider);

            volumeRenderer = volumeObject.GetComponent<MeshRenderer>();
            volumeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            volumeRenderer.receiveShadows = false;
            volumeRenderer.lightProbeUsage = LightProbeUsage.Off;
            volumeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            volumeRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            volumeRenderer.allowOcclusionWhenDynamic = false;

            volumeMaterial = new Material(shader)
            {
                name = "Fog Light Volume (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            volumeRenderer.sharedMaterial = volumeMaterial;
            properties = new MaterialPropertyBlock();
        }

        private void UpdateVolume()
        {
            if (sourceLight == null || volumeObject == null || volumeRenderer == null)
            {
                return;
            }

            bool supported = sourceLight.type == LightType.Point;
            volumeRenderer.enabled = supported && sourceLight.enabled && sourceLight.gameObject.activeInHierarchy;

            if (!supported)
            {
                return;
            }

            float radius = Mathf.Max(0.01f, sourceLight.range * radiusMultiplier);
            volumeObject.transform.localScale = Vector3.one * radius * 2f;

            if (properties == null)
            {
                properties = new MaterialPropertyBlock();
            }

            volumeRenderer.GetPropertyBlock(properties);
            properties.SetColor(ColorId, sourceLight.color);
            properties.SetVector(VolumeCenterId, transform.position);
            properties.SetFloat(VolumeRadiusId, radius);
            properties.SetFloat(DensityId, density);
            properties.SetFloat(BrightnessId, brightness * Mathf.Min(Mathf.Sqrt(Mathf.Max(sourceLight.intensity, 0f)), 8f));
            properties.SetFloat(FalloffId, radialFalloff);
            properties.SetFloat(NoiseAmountId, noiseAmount);
            properties.SetFloat(NoiseScaleId, noiseScale);
            properties.SetFloat(NoiseSpeedId, noiseSpeed);
            properties.SetFloat(DepthOffsetId, depthOffset);
            volumeRenderer.SetPropertyBlock(properties);
        }

        private static void EnsureCameraDepthTextures()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
            foreach (Camera camera in cameras)
            {
                if (camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                {
                    cameraData.requiresDepthTexture = true;
                }
            }
        }

        private void ReleaseVolume()
        {
            DestroyGeneratedObject(volumeObject);
            DestroyGeneratedObject(volumeMaterial);
            volumeObject = null;
            volumeRenderer = null;
            volumeMaterial = null;
            properties = null;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
