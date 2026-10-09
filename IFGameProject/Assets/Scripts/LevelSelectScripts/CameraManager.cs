using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    // Singleton instance
    public static CameraManager Instance { get; private set; }

    // Stored priority value
    public static int MainCameraPriority { get; private set; }

    [SerializeField] private CinemachineCamera mainCamera;
    public CinemachineBrain mainCameraBrain;
    public Node selectedNode;

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

    private void Start()
    {
        // Auto-find main camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        if (!mainCameraBrain)
        {
            mainCameraBrain = FindFirstObjectByType<CinemachineBrain>();
        }

        // Store the priority at startup
        if (mainCamera != null)
        {
            MainCameraPriority = mainCamera.Priority;
            Debug.Log($"Main camera priority stored: {MainCameraPriority}");
        }
        else
        {
            Debug.LogWarning("No CinemachineCamera found in scene.");
        }
    }
}
