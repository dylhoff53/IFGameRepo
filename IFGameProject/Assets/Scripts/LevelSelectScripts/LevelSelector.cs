using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelSelector : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private LayerMask interactableLayers;
    [SerializeField] private float maxInteractionDistance = 10f;
    [SerializeField] private bool showDebugRay = true;

    private Camera mainCamera;


    public static LevelSelector Instance { get; private set; }
    public GameObject playerIndicator;
    public Node previousNode;
    public GameObject NodeUI;
    public TMPro.TextMeshProUGUI activeNodeNameText;
    public TMPro.TextMeshProUGUI activeNodeNumberText;
    public GameObject startingNode;
    public bool isLocked;
    public GameObject selectionconfirmationUI;

    public GameObject colorSheet;

    public bool isTransitioning;
    public bool loadingIntoLevel;
    public float counter;
    public float multi;


    private void Awake()
    {
        // Singleton pattern setup
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Start()
    {
        // Ensure playerIndicator is set
        if (playerIndicator == null)
        {
            Debug.LogError("Player indicator is not assigned in MapManager.");
        }
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("No main camera found in the scene!");
        }

        startingNode.GetComponent<Node>().OnSelected();
        isTransitioning = true;
    }

    public void Update()
    {

        if (isTransitioning)
        {
            counter -= multi * Time.deltaTime;
            colorSheet.GetComponent<CanvasGroup>().alpha = counter;
            if (counter <= 0f || counter >= 1f)
            {
                if (loadingIntoLevel)
                {
                    Invoke("Change", 1.5f);
                }
                else
                {
                    isTransitioning = false;
                    colorSheet.SetActive(false);
                    counter = 0f;
                    colorSheet.GetComponent<CanvasGroup>().alpha = 0f;
                }

            }
        }
        
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            CheckForInteraction();
        }
    }


    public void OnEscape(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            CheckForSelection();
        }
    }

    public void OnEnter(InputAction.CallbackContext context)
    {
        if (!isLocked)
        {
            ConfirmSelection();
        }
        else
        {
            YesButtonPressed();
        }
    }

    public void StartLevelPressed()
    {
        if (!isLocked)
        {
            ConfirmSelection();
        }
    }

    public void ConfirmSelection()
    {
        selectionconfirmationUI.SetActive(true);
        isLocked = true;
    }

    public void YesButtonPressed()
    {
        multi = -multi;
        isTransitioning = true;
        loadingIntoLevel = true;
        colorSheet.SetActive(true);
    }

    public void NoButtonPressed()
    {
        if (!loadingIntoLevel)
        {
            isLocked = false;
            selectionconfirmationUI.SetActive(false);
        }
    }

    public void Change()
    {
        if (loadingIntoLevel)
        {
            SceneManager.LoadScene(int.Parse(CameraManager.Instance.selectedNode.nodeNumber));
        }

    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (playerIndicator.activeInHierarchy && !CameraManager.Instance.mainCameraBrain.IsBlending && !isLocked)
        {
            if (context.performed)
            {
                bool confirm = false;
                Vector2 input = context.ReadValue<Vector2>();
                Debug.Log(input + " Input received for movement.");
                if (input != Vector2.zero)
                {
                    previousNode = CameraManager.Instance.selectedNode;
                    if (input.x < 0 && CameraManager.Instance.selectedNode.WestNode != null)
                    {
                        CameraManager.Instance.selectedNode = CameraManager.Instance.selectedNode.WestNode;
                        confirm = true;

                        //CameraManager.Instance.mainCameraBrain.DefaultBlend.BlendTime = 1f; // Set blend time for camera transition
                    }
                    else if (input.x > 0 && CameraManager.Instance.selectedNode.EastNode != null)
                    {
                        CameraManager.Instance.selectedNode = CameraManager.Instance.selectedNode.EastNode;
                        confirm = true;
                    }
                    else if (input.y < 0 && CameraManager.Instance.selectedNode.SouthNode != null)
                    {
                        CameraManager.Instance.selectedNode = CameraManager.Instance.selectedNode.SouthNode;
                        confirm = true;
                    }
                    else if (input.y > 0 && CameraManager.Instance.selectedNode.NorthNode != null)
                    {
                        CameraManager.Instance.selectedNode = CameraManager.Instance.selectedNode.NorthNode;
                        confirm = true;
                    }
                    if (confirm == true)
                    {
                        CameraManager.Instance.selectedNode.OnSelected();
                        previousNode.DeactivateArrows();
                        Invoke(nameof(PostMoveReset), 2f); // Reset after a delay
                    }
                }
            }
        }
    }

    public void OnOrbit(InputAction.CallbackContext context)
    {
        if (context.performed && playerIndicator.activeInHierarchy && !CameraManager.Instance.mainCameraBrain.IsBlending)
        {
            CameraManager.Instance.selectedNode.autoOrbitCamera.isOrbiting = !CameraManager.Instance.selectedNode.autoOrbitCamera.isOrbiting;
        }
    }


    public void PostMoveReset()
    {
        previousNode.OnDeselected();
    }

    public void UpdateNodeUI(string nodeName, string nodeNumber)
    {
        if (!NodeUI.activeInHierarchy)
        {
            NodeUI.SetActive(true);
        }

        activeNodeNameText.text = "Name: " + nodeName;
        activeNodeNumberText.text = "Number: " + nodeNumber;
    }



    private void CheckForSelection()
    {
        if (CameraManager.Instance.selectedNode != null && !CameraManager.Instance.mainCameraBrain.IsBlending)
        {
            CameraManager.Instance.selectedNode.nodeCamera.Priority = 0;
            CameraManager.Instance.selectedNode.DeactivateArrows();
            playerIndicator.SetActive(false);
            NodeUI.SetActive(false);
            Invoke(nameof(DeselectReset), 2f); // Delay to ensure deselection logic is processed
        }
        else
        {
            Debug.Log("No node selected to clear.");
        }
    }
    private void DeselectReset()
    {
        CameraManager.Instance.selectedNode.OnDeselected();
        CameraManager.Instance.selectedNode = null;
    }


    private void CheckForInteraction()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxInteractionDistance, interactableLayers) && CameraManager.Instance.selectedNode == null)
        {
            HandleInteraction(hit.collider.gameObject);
        }

        // Draw debug ray if enabled
        if (showDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * maxInteractionDistance, Color.red, 1f);
        }
    }

    private void HandleInteraction(GameObject clickedObject)
    {
        Debug.Log($"Clicked on: {clickedObject.name}");
        //playerIndicator.SetActive(true);
        clickedObject.GetComponent<Node>().OnSelected();
        // Add your interaction logic here
    }
}
