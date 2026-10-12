using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        transform.forward = cam.transform.forward;
    }
}