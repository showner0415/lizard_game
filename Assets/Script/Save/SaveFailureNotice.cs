using UnityEngine;

/// <summary>
/// 세이브를 불러오지 못한 세션에만 붙는 임시 경고.
/// </summary>
public sealed class SaveFailureNotice : MonoBehaviour
{
    void OnGUI()
    {
        GUI.color = Color.red;
        GUI.Label(new Rect(10, 10, 700, 40), "세이브를 불러오지 못해 이번 실행은 저장되지 않습니다.");
    }
}
