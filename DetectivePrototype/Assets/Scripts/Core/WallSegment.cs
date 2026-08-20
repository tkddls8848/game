namespace Detective.Core
{
    /// <summary>
    /// 벽 조각 하나. 축 정렬 사각형이고 중심 좌표 + 크기로 표현한다.
    /// UnityEngine에 의존하지 않는다 — 씬 빌더가 이 값을 그대로 Transform/BoxCollider2D에 옮긴다.
    /// </summary>
    public struct WallSegment
    {
        public readonly float CenterX;
        public readonly float CenterY;
        public readonly float Width;
        public readonly float Height;

        public WallSegment(float centerX, float centerY, float width, float height)
        {
            CenterX = centerX;
            CenterY = centerY;
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return string.Format("Wall(c=({0:0.##},{1:0.##}) size=({2:0.##},{3:0.##}))",
                CenterX, CenterY, Width, Height);
        }
    }
}
