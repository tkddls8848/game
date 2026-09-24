namespace Detective.Core
{
    /// <summary>월드 좌표 한 점. UnityEngine.Vector2 대신 쓰는 순수 C# 값(§18-1).</summary>
    public struct Point2
    {
        public readonly float X;
        public readonly float Y;

        public Point2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return string.Format("({0:0.##},{1:0.##})", X, Y);
        }
    }
}
