using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 씬을 넘어 유지되는 플레이어 루트. 거점에 다시 들어올 때 씬에 배치된 복제본은
/// 자식이 깨어나기 전에 꺼서 파괴한다(Awake 순서를 가장 앞으로 둔다).
/// </summary>
[DefaultExecutionOrder(-1000)]
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
        if (Instance != null && Instance != this)
        {
            // 아직 깨어나지 않은 자식(UIManager, 창, PlayerInput, SaveManager…)의 Awake/OnEnable을 막는다.
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!shouldMoveOnSceneLoad)
            return;

        shouldMoveOnSceneLoad = false;

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }

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

        transform.position = spawn.transform.position;

        Transform player = transform.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("PlayerRoot 아래에 Player 오브젝트를 찾지 못함");
            yield break;
        }

        player.localPosition = Vector3.zero;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = transform.position;
        }
    }
}
