using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoHomePortal : MonoBehaviour
{
    [SerializeField] private string homeSceneName = "Home";
    [SerializeField] private float stayTime = 3f;

    private Coroutine stayCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // 이미 카운트 중이면 무시
        if (stayCoroutine == null)
        {
            stayCoroutine = StartCoroutine(StayAndGoHome(other.transform));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // 나가면 취소
        if (stayCoroutine != null)
        {
            StopCoroutine(stayCoroutine);
            stayCoroutine = null;
        }
    }

    private IEnumerator StayAndGoHome(Transform player)
    {
        float timer = 0f;

        while (timer < stayTime)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        SaveManager.Instance?.SaveOnExtract();
        PlayerRoot.Instance.MoveToScene(homeSceneName);
    }
}