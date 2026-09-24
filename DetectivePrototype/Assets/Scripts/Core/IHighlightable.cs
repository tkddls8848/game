namespace Detective.Core
{
    /// <summary>플레이어가 다가가면 강조 표시되는 대상(조사 대상 하이라이트).</summary>
    public interface IHighlightable
    {
        void SetHighlighted(bool highlighted);
    }
}
