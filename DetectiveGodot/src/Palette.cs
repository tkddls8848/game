using Godot;

namespace DetectiveGodot
{
    /// <summary>
    /// 색과 글자 크기의 단일 출처.
    ///
    /// 처음 구현이 가벼워 보인 원인은 셋이었다. 여기서 한꺼번에 뒤집는다.
    ///
    ///   1. <b>채도가 높았다.</b> 호박색 #E8B06A를 강조에 썼더니 아케이드처럼 읽혔다.
    ///      진중함은 색을 빼는 데서 나온다 — 바랜 뼈색·재색·마른 핏빛으로 내린다.
    ///   2. <b>대비가 극단적이었다.</b> 순검정 배경 + 밝은 강조는 도트 게임의 문법이다.
    ///      배경을 완전한 검정에서 들어 올리고(짙은 잉크빛), 강조를 끌어내려 사이를 좁힌다.
    ///   3. <b>글자에 굵은 검은 외곽선이 있었다.</b> 4px 외곽선은 그것만으로 게임 UI가 된다.
    ///      인쇄된 글자는 외곽선이 없다. 지우고 크기를 키우고 불투명도를 낮춘다.
    ///
    /// 연출 방향("사건 파일 — 바랜 종이, 검은·붉은 잉크, 명조체")은 처음부터 문서에 있었다.
    /// 이 파일은 그것을 실제 수치로 옮긴 것이다.
    /// </summary>
    public static class Palette
    {
        // ── 바탕 ──────────────────────────────────────────────

        /// <summary>가장 어두운 바탕. 순검정이 아니다 — 순검정은 화면을 납작하게 만든다.</summary>
        public static readonly Color Void = new Color(0.055f, 0.052f, 0.062f);

        /// <summary>방 안쪽 그늘. 잉크가 고인 느낌.</summary>
        public static readonly Color Ink = new Color(0.092f, 0.088f, 0.101f);

        /// <summary>벽 옆면.</summary>
        public static readonly Color Wall = new Color(0.148f, 0.136f, 0.126f);

        /// <summary>벽 윗면. 위에서 내려다보므로 이것이 윤곽을 만든다.</summary>
        public static readonly Color WallCap = new Color(0.225f, 0.208f, 0.190f);

        /// <summary>문턱. 마른 나무.</summary>
        public static readonly Color Door = new Color(0.300f, 0.225f, 0.150f);

        // ── 글자·종이 ─────────────────────────────────────────

        /// <summary>바랜 종이에 쓴 글자. 흰색이 아니다.</summary>
        public static readonly Color Paper = new Color(0.815f, 0.790f, 0.725f);

        /// <summary>부차적인 글자. 조작 안내 등.</summary>
        public static readonly Color Faint = new Color(0.455f, 0.440f, 0.420f);

        /// <summary>벽 너머 웅얼거림. 읽히지 않아야 한다.</summary>
        public static readonly Color Muffled = new Color(0.390f, 0.385f, 0.380f);

        // ── 강조 ──────────────────────────────────────────────

        /// <summary>
        /// 유일한 따뜻한 색. 등불빛이되 호박색보다 한참 죽였다.
        /// 청취점·재생 머리처럼 "지금 여기"를 가리키는 데만 쓴다.
        /// </summary>
        public static readonly Color Lamp = new Color(0.760f, 0.660f, 0.495f);

        /// <summary>마른 핏빛. 사건 파일의 붉은 잉크. 아주 드물게 쓴다.</summary>
        public static readonly Color Oxblood = new Color(0.475f, 0.185f, 0.165f);

        /// <summary>차가운 강조. 말(증언) 눈금에 쓴다 — 소리(정황)와 구별되어야 한다.</summary>
        public static readonly Color Slate = new Color(0.475f, 0.520f, 0.565f);

        // ── 사람 ──────────────────────────────────────────────

        /// <summary>
        /// 실루엣. <b>밝은 회색이 아니다</b> — 사람은 그림자로 보여야 한다.
        /// 밝게 두면 보드게임 말이 되고, 그 인상이 전체를 가볍게 만든다.
        /// </summary>
        public static readonly Color Figure = new Color(0.128f, 0.122f, 0.120f);

        /// <summary>이동 중. 더 흐리게 — 지나가는 사람이다.</summary>
        public static readonly Color FigureTransit = new Color(0.098f, 0.094f, 0.094f);

        // ── 글자 크기 ─────────────────────────────────────────
        //
        // 작은 글자를 외곽선으로 읽히게 만드는 대신, 크게 쓰고 외곽선을 없앤다.

        public const int SizeBody = 23;
        public const int SizeStatus = 21;
        public const int SizeRoomName = 19;
        public const int SizeHint = 15;

        /// <summary>
        /// 글자에 줄 그림자. 외곽선(Outline) 대신 쓴다 — 외곽선은 글자를 스티커로 만들고,
        /// 그림자는 종이에 인쇄된 것처럼 남는다.
        /// </summary>
        public static LabelSettings Label(Font font, int size, Color color)
        {
            var settings = new LabelSettings
            {
                FontSize = size,
                FontColor = color,
                OutlineSize = 0,
                ShadowSize = 2,
                ShadowColor = new Color(0f, 0f, 0f, 0.55f),
                ShadowOffset = new Vector2(0f, 1.5f)
            };
            if (font != null) settings.Font = font;
            return settings;
        }
    }
}
