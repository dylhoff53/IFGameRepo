using Unity.Cinemachine;
using UnityEngine;


public class AutoOrbitCamera : MonoBehaviour
{
    public GameObject freeLookCamera;
    public float orbitSpeed = 10f;
    public bool isOrbiting = false;

    public CinemachineOrbitalFollow orbital;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (freeLookCamera != null)
        {
            orbital = freeLookCamera.GetComponent<CinemachineOrbitalFollow>();
            if (orbital == null)
            {
                Debug.LogError("CinemachineOrbitalFollow component not found on the FreeLook camera.");
            }
        }
        else
        {
            Debug.LogError("FreeLook camera is not assigned.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isOrbiting && orbital != null)
        {
            orbital.HorizontalAxis.Value += orbitSpeed * Time.deltaTime;
        }
    }
}
