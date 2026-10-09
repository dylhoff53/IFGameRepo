using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControler : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private LayerMask interactableLayers;
    [SerializeField] private float maxInteractionDistance = 10f;
    [SerializeField] private bool showDebugRay = true;

    private Camera mainCamera;

    public Node previousNode;

    public void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("No main camera found in the scene!");
        }
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            CheckForInteraction();
        }
    }

    private void CheckForInteraction()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxInteractionDistance, interactableLayers) && !CameraManager.Instance.mainCameraBrain.IsBlending)
        {
            HandleInteraction(hit.collider.gameObject);
        }

        // Draw debug ray if enabled
        if (showDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * maxInteractionDistance, Color.red, 1f);
        }
    }

    public void PostMoveReset()
    {
        if (previousNode != null)
        {
            previousNode.OnDeselected();
        }
    }

    private void HandleInteraction(GameObject clickedObject)
    {
        Debug.Log($"Clicked on: {clickedObject.name}");
        //playerIndicator.SetActive(true);
        Invoke(nameof(PostMoveReset), 2f);
        // Add your interaction logic here
    }
}
