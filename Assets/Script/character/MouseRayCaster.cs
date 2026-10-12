using UnityEngine;
using UnityEngine.InputSystem;

public class MouseRayCaster : MonoBehaviour
{
    public Camera cam;
    public LayerMask groundMask;

    public bool TryGetMouseWorldPosition(out Vector3 point)
    {
        Ray ray = cam.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundMask))
        {
            point = hit.point;
            return true;
        }

        point = Vector3.zero;
        return false;
    }
}