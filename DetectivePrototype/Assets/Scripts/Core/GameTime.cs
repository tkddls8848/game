using System.Globalization;

namespace Detective.Core
{
    /// <summary>
    /// 사건 시각 표현. 내부 시각은 사건 시작(18:00) 기준 <b>정수 밀리초</b>이고, 문자열은 표시용이다.
    /// 시각 비교·채점은 전부 정수 밀리초로 한다 — 부동소수·문자열 시각 비교 금지(CLAUDE.md 설계 원칙 5).
    ///
    /// 틱(10분 칸, 0~6)은 표시용 파생값이다. 수사 노트 표와 타임라인 눈금이 이 칸을 쓴다.
    /// 틱으로 적힌 기존 데이터(schedule[7], revealTick 등)는 <see cref="TickToMs"/> 경계 함수로만 ms가 된다.
    /// </summary>
    public static class GameTime
    {
        /// <summary>"시각 없음". 데이터의 -1 관례와 같은 값이다.</summary>
        public const int NoTime = -1;

        public const int MsPerSecond = 1000;
        public const int MsPerMinute = 60 * MsPerSecond;
        public const int MsPerHour = 60 * MsPerMinute;

        public const int StartHour = 18;

        // ----- 표시용 틱(10분 칸) --------------------------------------------------

        public const int MinutesPerTick = 10;
        public const int MsPerTick = MinutesPerTick * MsPerMinute;

        public const int FirstTick = 0;
        public const int LastTick = ProjectInfo.TimelineTickCount - 1;
        public const int TickCount = ProjectInfo.TimelineTickCount;

        // ----- 밀리초 축 -------------------------------------------------------

        /// <summary>18:00. 사건 기록의 첫 시각.</summary>
        public const int StartMs = 0;

        /// <summary>19:00. 마지막 틱이 시작하는 시각이자 사건 기록의 끝(포함).</summary>
        public const int EndMs = StartMs + LastTick * MsPerTick;

        public const int DurationMs = EndMs - StartMs;

        /// <summary>플레이어가 저택에 도착한 "현재"(19:00). 사건 기록은 전부 이 시각 이전의 과거다.</summary>
        public const int PresentMs = EndMs;

        /// <summary><see cref="PresentMs"/>가 속한 틱. 표시용.</summary>
        public const int PresentTick = LastTick;

        public static bool IsValid(int ms)
        {
            return ms >= StartMs && ms <= EndMs;
        }

        public static int Clamp(int ms)
        {
            if (ms < StartMs) return StartMs;
            if (ms > EndMs) return EndMs;
            return ms;
        }

        /// <summary>ms → "18:30"(초 이하는 버린다). 범위 밖이면 "--:--".</summary>
        public static string ToLabel(int ms)
        {
            if (!IsValid(ms)) return "--:--";

            int minutes = StartHour * 60 + ms / MsPerMinute;
            return Two(minutes / 60) + ":" + Two(minutes % 60);
        }

        /// <summary>ms → "18:30:12"(밀리초는 버린다). 범위 밖이면 "--:--:--".</summary>
        public static string ToLabelWithSeconds(int ms)
        {
            if (!IsValid(ms)) return "--:--:--";

            int seconds = StartHour * 3600 + ms / MsPerSecond;
            return Two(seconds / 3600) + ":" + Two(seconds / 60 % 60) + ":" + Two(seconds % 60);
        }

        /// <summary>"18:30" 또는 "18:30:12" → ms. 형식이 틀리거나 범위 밖이면 false(ms = <see cref="NoTime"/>).</summary>
        public static bool TryParse(string label, out int ms)
        {
            ms = NoTime;
            if (string.IsNullOrEmpty(label)) return false;

            string[] parts = label.Trim().Split(':');
            if (parts.Length != 2 && parts.Length != 3) return false;

            int hour, minute, second = 0;
            if (!ParsePart(parts[0], out hour)) return false;
            if (!ParsePart(parts[1], out minute) || minute >= 60) return false;
            if (parts.Length == 3 && (!ParsePart(parts[2], out second) || second >= 60)) return false;

            // 시(hour)가 터무니없이 크면 int 곱셈이 넘친다. 범위 판정 전에 먼저 거른다.
            if (hour < StartHour || hour > StartHour + DurationMs / MsPerHour + 1) return false;

            int candidate = (hour - StartHour) * MsPerHour + minute * MsPerMinute + second * MsPerSecond;
            if (!IsValid(candidate)) return false;

            ms = candidate;
            return true;
        }

        // ----- 틱 ↔ ms -------------------------------------------------------

        public static bool IsValidTick(int tick)
        {
            return tick >= FirstTick && tick <= LastTick;
        }

        public static int ClampTick(int tick)
        {
            if (tick < FirstTick) return FirstTick;
            if (tick > LastTick) return LastTick;
            return tick;
        }

        /// <summary>
        /// 경계 함수: 틱으로 적힌 데이터 → 그 칸이 시작하는 ms. 범위 밖(-1 포함)이면 <see cref="NoTime"/>.
        /// 틱 데이터를 읽는 곳은 반드시 이 함수를 거친다.
        /// </summary>
        public static int TickToMs(int tick)
        {
            return IsValidTick(tick) ? StartMs + tick * MsPerTick : NoTime;
        }

        /// <summary>ms가 속한 10분 칸. 18:35 → 3. 범위 밖이면 <see cref="NoTime"/>.</summary>
        public static int TickOf(int ms)
        {
            return IsValid(ms) ? (ms - StartMs) / MsPerTick : NoTime;
        }

        /// <summary>틱 → "18:30". 범위 밖이면 "--:--". 표 머리·눈금용.</summary>
        public static string TickLabel(int tick)
        {
            return ToLabel(TickToMs(tick));
        }

        /// <summary>"18:30" → 3. 10분 칸의 시작에 정확히 맞지 않거나 범위 밖이면 false(tick = -1).</summary>
        public static bool TryParseTick(string label, out int tick)
        {
            tick = NoTime;
            int ms;
            if (!TryParse(label, out ms) || (ms - StartMs) % MsPerTick != 0) return false;

            tick = TickOf(ms);
            return true;
        }

        private static bool ParsePart(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static string Two(int value)
        {
            return value.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
