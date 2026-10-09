using Unity.Cinemachine;
using UnityEngine;

public class Node : MonoBehaviour
{
    [Header("Node Components")]
    [SerializeField] private Transform indicatorTransform;
    [SerializeField] private bool isSelectable = true;

    [Header("Visual Settings")]
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color selectedColor;

    [Header("Camera Components")]
    public CinemachineCamera nodeCamera;
    public AutoOrbitCamera autoOrbitCamera;

    [Header("Connected Nodes")]
    public Node NorthNode;
    public Node SouthNode;
    public Node EastNode;
    public Node WestNode;


    [Header("Node UI Info")]
    public string nodeName;
    public string nodeNickname;
    public string nodeNumber;


    [SerializeField] private GameObject[] directionalArrows;
    [SerializeField] private Node[] connectedNodes = new Node[4];



    private void Start()
    {
        nodeCamera = GetComponentInChildren<CinemachineCamera>();
        if (nodeCamera == null)
        {
            Debug.LogError("CinemachineCamera component not found in children of Node.");
        }
        autoOrbitCamera = GetComponentInChildren<AutoOrbitCamera>();
        if (autoOrbitCamera == null)
        {
            Debug.LogError("AutoOrbitCamera component not found in children of Node.");
        }
    }
    public void OnSelected()
    {
        if (isSelectable)
        {
            Debug.Log("Node selected");
            LevelSelector.Instance.playerIndicator.SetActive(true);
            LevelSelector.Instance.playerIndicator.transform.position = indicatorTransform.position;
            nodeCamera.Priority = CameraManager.MainCameraPriority + 1;
            CameraManager.Instance.selectedNode = this;
            LevelSelector.Instance.UpdateNodeUI(nodeName, nodeNumber);
            ActivateArrows();
        }
        else
        {
            Debug.Log("Node not selectable");
        }
    }

    public void OnDeselected()
    {
        Debug.Log("Node deselected");
        nodeCamera.Priority = 0;
        autoOrbitCamera.orbital.HorizontalAxis.Value = 0f;
        autoOrbitCamera.isOrbiting = false;
    }

    public void ActivateArrows()
    {
        int index = 0;
        foreach (Node node in connectedNodes)
        {
            if (node != null)
            {
                directionalArrows[index].SetActive(true);
                directionalArrows[index].transform.LookAt(node.transform);
            }
            index++;
        }
    }

    public void DeactivateArrows()
    {
        foreach (GameObject arrow in directionalArrows)
        {
            arrow.SetActive(false);
        }
    }

    public void ResetColor()
    {
        gameObject.GetComponent<Renderer>().material.color = startColor;
    }

    public void SetSelectedColor()
    {
        gameObject.GetComponent<Renderer>().material.color = selectedColor;
    }

    public void SetColor(Color color)
    {
        gameObject.GetComponent<Renderer>().material.color = color;
    }
}
