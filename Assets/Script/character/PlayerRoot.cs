using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class PlayerRoot : MonoBehaviour
{
    public static PlayerRoot Instance;

    private bool shouldMoveOnSceneLoad = false;

    public void MoveToScene(string sceneName)
    {
        // 씬이 바뀌면 커서 UI가 사라지므로, 그 전에 아이템을 모델이나 바닥에 둔다.
        if (UIManager.Instance != null)
            UIManager.Instance.CloseAll();

        shouldMoveOnSceneLoad = true;
        SceneManager.LoadScene(sceneName);
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 여기서 바로 등록
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!shouldMoveOnSceneLoad)
        { 
            Debug.Log("문제1");
            return;
        }
            

        shouldMoveOnSceneLoad = false;

        Canvas canvas = GetComponentInChildren<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        StartCoroutine(MoveNextFrame());
    }

    IEnumerator MoveNextFrame()
    {

        // 씬 내 모든 Awake / Start가 끝난 뒤 실행
        yield return null;

        GameObject spawn = GameObject.FindWithTag("PlayerSpawn");
        if (spawn == null)
        {
            Debug.LogWarning("PlayerSpawn 태그 오브젝트를 찾지 못함");
            yield break;
        }

        // 1. PlayerRoot를 스폰 위치로 이동
        transform.position = spawn.transform.position; 

        // 2. Player 찾기
        Transform player = transform.Find("Player");   
        if (player == null)
        {
            Debug.LogWarning("PlayerRoot 아래에 Player 오브젝트를 찾지 못함");
            yield break;
        }
        // 3. Player localPosition 초기화 (핵심)
        player.localPosition = Vector3.zero; 

        // 4. Rigidbody 보정 (있을 경우)
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Rigidbody가 있는 경우 position도 명시적으로 맞춰줌
            rb.position = transform.position;
        }
    }
}