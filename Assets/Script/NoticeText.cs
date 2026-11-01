/// <summary>플레이어에게 보여 주는 문구. 호출부에서 문장을 만들지 않는다.</summary>
public static class NoticeText
{
    /// <summary>마나가 부족하다.</summary>
    public const string NotEnoughMana = "마나가 부족합니다";

    /// <summary>장전된 비늘이 부족하다.</summary>
    public const string NotEnoughScales = "장전된 비늘이 부족합니다 (R: 장전)";

    /// <summary>채널 중에 비늘이 떨어졌다.</summary>
    public const string ScalesRanOut = "비늘이 다 떨어졌습니다";

    /// <summary>장전할 자란 칸이 없다.</summary>
    public const string NoGrownSlot = "장전할 수 있는 비늘 칸이 없습니다";

    /// <summary>고른 파밍 비늘이 인벤토리에 없다.</summary>
    public const string SelectedScaleMissing = "선택한 비늘이 인벤토리에 없습니다 (G: 비늘 선택)";

    /// <summary>다른 행동이 진행 중이다.</summary>
    public const string Busy = "지금은 다른 행동 중입니다";

    /// <summary>비늘 선택을 바꿀 수 없다.</summary>
    public const string CannotChangeScale = "지금은 비늘을 바꿀 수 없습니다";

    /// <summary>퀵슬롯 장식이 인벤토리에 없다.</summary>
    public const string OrnamentNotOwned = "인벤토리에 없는 장식입니다";

    /// <summary>지금 그 행동을 할 수 없다.</summary>
    public const string CannotUseNow = "지금은 사용할 수 없습니다";
}
