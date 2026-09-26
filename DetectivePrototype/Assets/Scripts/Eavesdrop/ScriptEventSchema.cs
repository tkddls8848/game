using System;
using System.Collections.Generic;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 말이 아닌 소리 하나. 문이 닫히고, 잔이 깨지고, 발소리가 지나간다.
    ///
    /// 지금까지 들리는 것은 <see cref="Utterance"/>(말)뿐이었다. 그런데 이 게임에서 사람들은
    /// 계속 움직이고, <b>움직이는 과정 자체가 단서다</b> — 누가 언제 어느 문을 열었는지,
    /// 복도를 지나간 발소리가 한 사람이었는지 둘이었는지. 말만 들려서는 그것을 알 수 없다.
    ///
    /// 말과 갈라 둔 이유가 있다. 이벤트에는 목소리가 없으므로 "목소리 몇 번"에 이름을 붙이는
    /// 퍼즐에 들어가지 않고, 들었는지 세는 수(<see cref="ListeningSession.FullyHeardCount"/>)에도
    /// 끼지 않는다. 이벤트는 <b>정황</b>이고 말은 <b>증언</b>이다.
    ///
    /// JsonUtility 제약을 그대로 따른다 — public 필드만, 배열, 문자열 id.
    /// 시각은 회차 기준 정수 밀리초다(대본·이동 트랙과 같은 축).
    /// </summary>
    [Serializable]
    public class ScriptEvent
    {
        public string id;

        public int startMs;

        /// <summary>소리가 이어지는 길이. 0이면 순간음(문 닫힘)으로 보고 최소 길이를 쓴다.</summary>
        public int durationMs;

        /// <summary>어느 방에서 났는가. rooms.json의 방 id.</summary>
        public string room;

        /// <summary>누가 냈는가. 없으면 빈 문자열(저택이 내는 소리 — 괘종시계 등).</summary>
        public string npcId = string.Empty;

        /// <summary>
        /// 소리의 종류. <see cref="EventKind"/>의 값을 쓴다. 화면 표기와 자동 생성 규칙이 여기 붙는다.
        /// </summary>
        public string kind = EventKind.Other;

        /// <summary>같은 방에서 들었을 때 보이는 묘사. "문이 조용히 닫힌다".</summary>
        public string text = string.Empty;

        /// <summary>벽 너머로 들었을 때 보이는 묘사. 비어 있으면 종류에서 만든다.</summary>
        public string muffledText = string.Empty;

        /// <summary>소리 파일(Resources 경로, 확장자 없음). 없으면 침묵하고 글자만 남는다.</summary>
        public string sound = string.Empty;

        /// <summary>
        /// 벽 너머로도 <b>또렷하게</b> 들리는가. 큰 소리(유리 깨짐·비명·총성)는 벽을 넘어도
        /// 무슨 일인지 알 수 있다. 기본은 false — 벽 너머는 뭉개진다.
        /// </summary>
        public bool loud;

        /// <summary>순간음에 줄 최소 길이. 이보다 짧으면 화면에 스치지도 못한다.</summary>
        public const int MinDurationMs = 700;

        public int EffectiveDurationMs
        {
            get { return durationMs > 0 ? durationMs : MinDurationMs; }
        }

        public int EndMs { get { return startMs + EffectiveDurationMs; } }

        public ScriptEvent Normalized()
        {
            if (npcId == null) npcId = string.Empty;
            if (kind == null || kind.Length == 0) kind = EventKind.Other;
            if (text == null) text = string.Empty;
            if (muffledText == null) muffledText = string.Empty;
            if (sound == null) sound = string.Empty;
            if (room == null) room = string.Empty;
            if (startMs < 0) startMs = 0;
            return this;
        }
    }

    /// <summary>
    /// 이벤트 종류. 문자열 상수로 두는 것은 JsonUtility가 enum을 숫자로 다뤄서
    /// 데이터 파일이 읽히지 않게 되기 때문이다.
    /// </summary>
    public static class EventKind
    {
        /// <summary>문이 열리거나 닫힌다. 이동에서 자동으로 생긴다.</summary>
        public const string Door = "door";

        /// <summary>발소리가 지나간다. 이동 중에 자동으로 생긴다.</summary>
        public const string Footsteps = "footsteps";

        /// <summary>물건을 놓거나 떨어뜨린다.</summary>
        public const string Object = "object";

        /// <summary>유리·도자기가 깨진다. 기본이 큰 소리다.</summary>
        public const string Break = "break";

        /// <summary>몸싸움.</summary>
        public const string Struggle = "struggle";

        /// <summary>저택이 내는 소리 — 괘종시계, 삐걱임.</summary>
        public const string House = "house";

        public const string Other = "other";

        /// <summary>벽 너머로 들었을 때의 기본 묘사. 무슨 소리인지는 알고 내용은 모른다.</summary>
        public static string MuffledDescription(string kind)
        {
            switch (kind)
            {
                case Door: return "어디선가 문이 여닫힌다";
                case Footsteps: return "발소리가 지나간다";
                case Object: return "무언가 놓이는 소리";
                case Break: return "무언가 깨지는 소리";
                case Struggle: return "무언가 부딪히는 소리";
                case House: return "저택이 내는 소리";
                default: return "무슨 소리가 난다";
            }
        }

        /// <summary>이 종류는 벽을 넘어도 또렷한가. 데이터에서 <c>loud</c>로 덮어쓸 수 있다.</summary>
        public static bool IsLoudByDefault(string kind)
        {
            return kind == Break || kind == Struggle;
        }
    }

    /// <summary>
    /// 이벤트 하나가 청취점에 닿은 모습. <see cref="PerceivedUtterance"/>와 같은 자리의 값이다.
    /// </summary>
    public sealed class PerceivedEvent
    {
        public Audibility Level;
        public string EventId;
        public string Room;
        public string NpcId;
        public string Kind;

        /// <summary>지금 보여 줄 글자. 등급에 따라 이미 골라져 있다.</summary>
        public string Text;

        /// <summary>같은 방에서 듣고 있는가(소리를 원음으로 낼지 뭉갤지).</summary>
        public bool InSameRoom { get { return Level == Audibility.Full; } }
    }
}
