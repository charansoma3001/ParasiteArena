using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start() => mainCameraTransform = Camera.main.transform;

    void LateUpdate()
    {
        // Makes the UI always face the camera
        transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
                         mainCameraTransform.rotation * Vector3.up);
    }
}