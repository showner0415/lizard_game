/// <summary>탄창이 아직 들고 있는 비늘 수.</summary>
public static class ScaleMagazineExtensions
{
    /// <summary>장전된 칸과 선딜 중인 칸의 합. 탄창이 없으면 0.</summary>
    public static int HeldCount(this IScaleMagazine magazine)
    {
        if (magazine == null)
            return 0;

        return magazine.LoadedCount + magazine.ReservedCount;
    }
}
