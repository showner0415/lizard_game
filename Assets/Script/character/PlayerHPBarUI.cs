using UnityEngine;
using UnityEngine.UI;

public class PlayerHPBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private int maxHP;

    public void Init(int maxHp)
    {
        maxHP = maxHp;
        SetHP(maxHp);
    }

    public void SetHP(int currentHp)
    {
        if (maxHP <= 0)
            return;

        float ratio = (float)currentHp / maxHP;
        fillImage.fillAmount = ratio;
    }
}