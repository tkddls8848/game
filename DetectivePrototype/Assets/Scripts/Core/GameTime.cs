using System.Globalization;

namespace Detective.Core
{
    /// <summary>
    /// 사건 시각 표현. 내부는 정수 틱 0~6(18:00~19:00, 10분 단위)이고 문자열은 표시용이다.
    /// 시각 비교·채점은 전부 틱으로 한다 — 문자열 시각 비교 금지(CLAUDE.md 설계 원칙 5).
    /// </summary>
    public static class GameTime
    {
        public const int FirstTick = 0;
        public const int LastTick = ProjectInfo.TimelineTickCount - 1;
        public const int TickCount = ProjectInfo.TimelineTickCount;

        /// <summary>플레이어가 저택에 도착한 "현재". 사건 기록은 전부 이 시각 이전의 과거다.</summary>
        public const int PresentTick = LastTick;

        public const int StartHour = 18;
        public const int MinutesPerTick = 10;

        public static bool IsValidTick(int tick)
        {
            return tick >= FirstTick && tick <= LastTick;
        }

        public static int Clamp(int tick)
        {
            if (tick < FirstTick) return FirstTick;
            if (tick > LastTick) return LastTick;
            return tick;
        }

        /// <summary>틱 → "18:30". 범위 밖이면 "--:--".</summary>
        public static string ToLabel(int tick)
        {
            if (!IsValidTick(tick)) return "--:--";

            int minutes = StartHour * 60 + tick * MinutesPerTick;
            return (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + ":"
                + (minutes % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>"18:30" → 3. 10분 단위에 정확히 맞지 않거나 범위 밖이면 false.</summary>
        public static bool TryParse(string label, out int tick)
        {
            tick = -1;
            if (string.IsNullOrEmpty(label)) return false;

            string[] parts = label.Trim().Split(':');
            if (parts.Length != 2) return false;

            int hour, minute;
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hour)) return false;
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minute)) return false;
            if (minute < 0 || minute >= 60) return false;

            int offset = (hour - StartHour) * 60 + minute;
            if (offset < 0 || offset % MinutesPerTick != 0) return false;

            int candidate = offset / MinutesPerTick;
            if (!IsValidTick(candidate)) return false;

            tick = candidate;
            return true;
        }
    }
}
