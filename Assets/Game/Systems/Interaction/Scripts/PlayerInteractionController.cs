using UnityEngine;

namespace AnEchoHasNoShape.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionController : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [SerializeField, Min(0.1f)] private float interactionRange = 4f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private Camera interactionCamera;

        [Header("Hover Highlight")]
        [SerializeField] private Color highlightColor = new Color(0.78f, 0.92f, 1f, 0.42f);
        [SerializeField, Range(0f, 2f)] private float highlightStrength = 1f;
        [SerializeField, Min(0.1f)] private float highlightShimmerSpeed = 1.8f;
        [SerializeField, Min(0.01f)] private float highlightFadeDuration = 0.18f;

        private Shader highlightShader;
        private InteractionHighlight currentHighlight;
        private MonoBehaviour hoveredBehaviour;
        private IInteractable hoveredInteractable;
        private EchoInteractionGate hoveredGate;

        private void Awake()
        {
            FindInteractionCamera();
            Material shaderReference = Resources.Load<Material>("Rendering/InteractionHoverRuntime");
            highlightShader = shaderReference != null && shaderReference.shader != null
                ? shaderReference.shader
                : Shader.Find("An Echo Has No Shape/Interaction Hover Overlay");
            if (highlightShader == null)
            {
                Debug.LogError(
                    "Interaction highlight shader could not be found. Ensure it is included in Graphics Settings.",
                    this);
            }

            ScreenPromptUI.EnsureExists();
        }

        private void Update()
        {
            if (InteractionTextPanel.Instance != null && InteractionTextPanel.Instance.IsOpen)
            {
                ClearHover();

                if (Input.GetKeyDown(interactKey))
                {
                    InteractionTextPanel.Instance.AdvanceOrClose();
                }

                return;
            }

            if (Time.timeScale <= 0f)
            {
                ClearHover();
                return;
            }

            RefreshHover();

            if (hoveredInteractable != null && Input.GetKeyDown(interactKey))
            {
                hoveredInteractable.Interact(this);
                hoveredGate?.NotifyInteractionPerformed();
                ClearHover();
            }
        }

        private void RefreshHover()
        {
            FindInteractionCamera();

            if (interactionCamera == null)
            {
                ClearHover();
                return;
            }

            Ray interactionRay = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);

            if (!Physics.Raycast(
                    interactionRay,
                    out RaycastHit hit,
                    interactionRange,
                    interactionLayers,
                    QueryTriggerInteraction.Collide))
            {
                ClearHover();
                return;
            }

            MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IInteractable interactable)
                {
                    EchoInteractionGate gate = behaviour.GetComponentInParent<EchoInteractionGate>(true);
                    if (gate != null && gate.isActiveAndEnabled && !gate.IsInteractionAvailable)
                    {
                        ClearHover();
                        return;
                    }

                    SetHover(behaviour, interactable, gate);
                    return;
                }
            }

            ClearHover();
        }

        private void SetHover(MonoBehaviour behaviour, IInteractable interactable, EchoInteractionGate gate)
        {
            if (hoveredBehaviour != behaviour)
            {
                currentHighlight?.Hide();
                hoveredBehaviour = behaviour;
                hoveredInteractable = interactable;
                hoveredGate = gate;
                currentHighlight = behaviour.GetComponent<InteractionHighlight>();
                if (currentHighlight == null)
                {
                    currentHighlight = behaviour.gameObject.AddComponent<InteractionHighlight>();
                }
            }

            currentHighlight?.Show(
                highlightShader,
                highlightColor,
                highlightStrength,
                highlightShimmerSpeed,
                highlightFadeDuration);
            ScreenPromptUI.SetInteraction($"Press F to {interactable.InteractionVerb}.");
        }

        private void ClearHover()
        {
            if (hoveredBehaviour == null && currentHighlight == null)
            {
                ScreenPromptUI.ClearInteraction();
                return;
            }

            currentHighlight?.Hide();
            currentHighlight = null;
            hoveredBehaviour = null;
            hoveredInteractable = null;
            hoveredGate = null;
            ScreenPromptUI.ClearInteraction();
        }

        private void FindInteractionCamera()
        {
            if (interactionCamera != null)
            {
                return;
            }

            FirstPersonController firstPersonController = GetComponent<FirstPersonController>();
            interactionCamera = firstPersonController != null && firstPersonController.playerCamera != null
                ? firstPersonController.playerCamera
                : Camera.main;
        }

        private void OnValidate()
        {
            interactionRange = Mathf.Max(0.1f, interactionRange);
            highlightStrength = Mathf.Clamp(highlightStrength, 0f, 2f);
            highlightShimmerSpeed = Mathf.Max(0.1f, highlightShimmerSpeed);
            highlightFadeDuration = Mathf.Max(0.01f, highlightFadeDuration);
        }

        private void OnDisable()
        {
            ClearHover();
        }

    }
}
