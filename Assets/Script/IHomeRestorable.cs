/// <summary>거점에 들어오면 자기 상태를 가득 찬 출발 상태로 되돌린다.</summary>
public interface IHomeRestorable
{
    /// <summary>
    /// 거점 회복. 호출 순서와 상관없이 같고, 이미 회복된 상태에서 다시 불러도 안전해야 한다.
    /// </summary>
    void RestoreForHome();
}
