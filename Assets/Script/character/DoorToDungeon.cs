using UnityEngine;
using UnityEngine.InputSystem;

public class DoorToDungeon : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string targetSceneName = "Dungeon";

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryClickDoor();
        }
    }

    private void TryClickDoor()
    {
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            if (hit.transform == transform)
            {
                EnterDoor();
            }
        }
    }

    private void EnterDoor()
    {
        if (PlayerRoot.Instance == null)
        {
            Debug.LogError("PlayerRoot Instance ¾øÀ½");
            return;
        }
        SaveManager.Instance?.SaveOnEnterDungeon();
        PlayerRoot.Instance.MoveToScene(targetSceneName);
    }
}
