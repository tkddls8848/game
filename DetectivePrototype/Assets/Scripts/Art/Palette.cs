using UnityEngine;

namespace Detective.Art
{
    /// <summary>
    /// 색과 글자 크기의 단일 출처.
    ///
    /// 구현이 "가벼워 보인다"는 지적을 받았고 원인이 넷으로 드러났다. 전부 아트 컨셉
    /// 01번(사건 파일)을 코드로 옮기면서 어긋난 것이었다:
    ///
    ///   1. <b>채도가 높았다.</b> 강조색 #E8B06A를 대사·눈금·표시에 두루 써서 아케이드처럼 읽혔다.
    ///      진중함은 색을 빼는 데서 나온다 — 바랜 뼈색·재색·마른 핏빛으로 내린다.
    ///   2. <b>대비가 극단적이었다.</b> 순검정 배경 + 밝은 강조는 도트 게임의 문법이다.
    ///      배경을 완전한 검정에서 들어 올리고 강조를 끌어내려 사이를 좁힌다.
    ///   3. <b>글자에 굵은 외곽선이 있었다.</b> 그것만으로 글자가 스티커가 된다.
    ///      인쇄된 글자에는 외곽선이 없다.
    ///   4. <b>바닥이 밝았다.</b> 밝은 바닥은 그림자를 지우고 화면을 납작하게 만든다.
    ///
    /// 연출 방향("사건 파일 — 바랜 종이, 검은·붉은 잉크, 명조체")은 처음부터 문서에 있었다.
    /// 이 파일은 그것을 <b>눈대중이 아니라 값으로</b> 옮긴 것이다. 화면에 쓰이는 색을
    /// 새로 만들지 말고 여기서 가져간다.
    ///
    /// 후처리(<see cref="CameraGrade"/>)가 이 위에 채도·대비·비네팅을 한 번 더 건다.
    /// 그래서 여기 값은 "후처리 전" 기준이다 — 화면에서 본 색보다 조금 밝고 조금 진하다.
    /// </summary>
    public static class Palette
    {
        // ── 바탕 ──────────────────────────────────────────────

        /// <summary>카메라 배경. 순검정이 아니다 — 순검정은 화면을 납작하게 만든다.</summary>
        public static readonly Color Void = new Color(0.055f, 0.052f, 0.062f);

        /// <summary>방 안쪽 그늘. 잉크가 고인 느낌.</summary>
        public static readonly Color Ink = new Color(0.092f, 0.088f, 0.101f);

        /// <summary>바닥 기본색. 질감이 없을 때 쓴다.</summary>
        public static readonly Color Floor = new Color(0.260f, 0.235f, 0.205f);

        /// <summary>벽.</summary>
        public static readonly Color Wall = new Color(0.090f, 0.075f, 0.060f);

        /// <summary>문턱. 마른 나무.</summary>
        public static readonly Color Door = new Color(0.280f, 0.210f, 0.130f);

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
        public static readonly Color Lamp = new Color(0.620f, 0.535f, 0.400f);

        /// <summary>마른 핏빛. 사건 파일의 붉은 잉크. 아주 드물게 쓴다.</summary>
        public static readonly Color Oxblood = new Color(0.475f, 0.185f, 0.165f);

        /// <summary>차가운 강조. 증언(말) 표시에 쓴다 — 정황(소리)과 구별되어야 한다.</summary>
        public static readonly Color Slate = new Color(0.475f, 0.520f, 0.565f);

        // ── 사람 ──────────────────────────────────────────────

        /// <summary>
        /// 인물 토큰. 밝은 회색이 아니다 — 사람은 그림자로 보여야 한다.
        /// 밝게 두면 보드게임 말이 되고, 그 인상이 전체를 가볍게 만든다.
        /// </summary>
        public static readonly Color Figure = new Color(0.385f, 0.372f, 0.355f);

        /// <summary>이동 중. 더 흐리게 — 지나가는 사람이다.</summary>
        public static readonly Color FigureTransit = new Color(0.275f, 0.268f, 0.262f);

        // ── 글자 크기 ─────────────────────────────────────────
        //
        // 작은 글자를 외곽선으로 읽히게 만드는 대신, 크게 쓰고 외곽선을 없앤다.
        // uGUI 기준 값이다(캔버스 스케일러가 1080p 기준으로 맞춘다).

        public const int SizeTitle = 64;
        public const int SizeBody = 32;
        public const int SizeStatus = 28;
        public const int SizeCaption = 26;
        public const int SizeHint = 22;
    }
}
