namespace Detective.Core
{
    /// <summary>
    /// Phase 0 마커. 런타임 어셈블리(DetectivePrototype)가 생성되고
    /// 테스트 어셈블리에서 참조 가능한지 검증하는 용도로만 존재한다.
    /// </summary>
    public static class ProjectInfo
    {
        public const string PrototypeName = "DetectivePrototype";

        /// <summary>사건 타임라인의 총 틱 수 (18:00 ~ 19:00, 10분 단위).</summary>
        public const int TimelineTickCount = 7;
    }
}
