using Detective.Core;
using Detective.Data;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 시각 표현. 내부 시각은 18:00 기준 정수 밀리초이고, 틱(10분 칸)은 표시용 파생값이다.
    /// 채점·목격 판정이 전부 여기 기대므로 여기가 틀리면 전부 틀린다.
    /// </summary>
    public class TimeTests
    {
        private const int Minute = GameTime.MsPerMinute;

        // ----- 밀리초 축 -------------------------------------------------------

        [Test]
        public void Axis_IsIntegerMillisecondsFromSixPm()
        {
            Assert.AreEqual(0, GameTime.StartMs);
            Assert.AreEqual(600000, GameTime.MsPerTick);
            Assert.AreEqual(3600000, GameTime.EndMs, "19:00 = 60분");
            Assert.AreEqual(3600000, GameTime.DurationMs);
            Assert.AreEqual(GameTime.EndMs, GameTime.PresentMs);
        }

        [Test]
        public void ToLabel_TakesMilliseconds()
        {
            Assert.AreEqual("18:00", GameTime.ToLabel(0));
            Assert.AreEqual("18:00", GameTime.ToLabel(3), "3은 3ms지 18:30이 아니다");
            Assert.AreEqual("18:15", GameTime.ToLabel(15 * Minute));
            Assert.AreEqual("18:30", GameTime.ToLabel(31 * Minute - 1), "초 이하는 버린다");
            Assert.AreEqual("19:00", GameTime.ToLabel(GameTime.PresentMs));
        }

        [Test]
        public void ToLabelWithSeconds_ShowsSeconds()
        {
            Assert.AreEqual("18:20:34", GameTime.ToLabelWithSeconds(20 * Minute + 34 * GameTime.MsPerSecond + 567));
            Assert.AreEqual("19:00:00", GameTime.ToLabelWithSeconds(GameTime.EndMs));
            Assert.AreEqual("--:--:--", GameTime.ToLabelWithSeconds(GameTime.EndMs + 1));
        }

        [Test]
        public void ToLabel_OutOfRangeMs_ReturnsPlaceholder()
        {
            Assert.AreEqual("--:--", GameTime.ToLabel(-1));
            Assert.AreEqual("--:--", GameTime.ToLabel(GameTime.EndMs + 1));
        }

        [Test]
        public void TryParse_ReturnsMilliseconds()
        {
            int ms;
            Assert.IsTrue(GameTime.TryParse("18:15", out ms), "ms 축에서는 10분 칸 밖도 유효한 시각이다");
            Assert.AreEqual(15 * Minute, ms);
            Assert.IsTrue(GameTime.TryParse("18:30:12", out ms));
            Assert.AreEqual(30 * Minute + 12 * GameTime.MsPerSecond, ms);
            Assert.IsTrue(GameTime.TryParse("19:00", out ms));
            Assert.AreEqual(GameTime.EndMs, ms);
        }

        [Test]
        public void TryParse_RejectsMalformedAndOutOfRange()
        {
            int ms;
            Assert.IsFalse(GameTime.TryParse("17:59:59", out ms), "시작 전");
            Assert.IsFalse(GameTime.TryParse("19:00:01", out ms), "끝난 뒤");
            Assert.IsFalse(GameTime.TryParse("18:60", out ms));
            Assert.IsFalse(GameTime.TryParse("18:00:60", out ms));
            Assert.IsFalse(GameTime.TryParse("99999999:00", out ms), "int 넘침");
            Assert.IsFalse(GameTime.TryParse("18:00:00:00", out ms));
            Assert.IsFalse(GameTime.TryParse("abc", out ms));
            Assert.AreEqual(GameTime.NoTime, ms);
        }

        [Test]
        public void Clamp_KeepsMsInsideTimeline()
        {
            Assert.AreEqual(GameTime.StartMs, GameTime.Clamp(-5));
            Assert.AreEqual(GameTime.EndMs, GameTime.Clamp(int.MaxValue));
            Assert.AreEqual(123, GameTime.Clamp(123));
        }

        // ----- 틱 ↔ ms -------------------------------------------------------

        [Test]
        public void TickToMs_IsTheSlotStart_AndInvalidTicksHaveNoTime()
        {
            Assert.AreEqual(0, GameTime.TickToMs(0));
            Assert.AreEqual(30 * Minute, GameTime.TickToMs(3));
            Assert.AreEqual(GameTime.EndMs, GameTime.TickToMs(GameTime.LastTick));
            Assert.AreEqual(GameTime.NoTime, GameTime.TickToMs(-1), "데이터의 -1 = 시각 없음");
            Assert.AreEqual(GameTime.NoTime, GameTime.TickToMs(7));
        }

        [Test]
        public void TickOf_FloorsIntoTenMinuteSlots()
        {
            Assert.AreEqual(3, GameTime.TickOf(35 * Minute), "18:35 → 18:30 칸");
            Assert.AreEqual(3, GameTime.TickOf(40 * Minute - 1));
            Assert.AreEqual(4, GameTime.TickOf(40 * Minute));
            Assert.AreEqual(GameTime.LastTick, GameTime.TickOf(GameTime.EndMs));
            Assert.AreEqual(GameTime.NoTime, GameTime.TickOf(-1));
            Assert.AreEqual(GameTime.NoTime, GameTime.TickOf(GameTime.EndMs + 1));

            for (int tick = GameTime.FirstTick; tick <= GameTime.LastTick; tick++)
                Assert.AreEqual(tick, GameTime.TickOf(GameTime.TickToMs(tick)), "t=" + tick);
        }

        [Test]
        public void DataSchemas_ReadTickFieldsThroughTheBoundary()
        {
            Assert.AreEqual(30 * Minute, new EvidenceDefinition { revealTick = 3 }.RevealMs);
            Assert.AreEqual(GameTime.NoTime, new EvidenceDefinition().RelatedMs, "빠진 시각은 -1 → NoTime");
            Assert.AreEqual(10 * Minute, new DialogueLine { claimTick = 1 }.ClaimMs);
            Assert.AreEqual(GameTime.NoTime, new DialogueLine().SightingMs);
            Assert.AreEqual(30 * Minute, new CaseAnswer { tick = 3 }.TimeMs);
        }

        // ----- 표시용 틱 ------------------------------------------------------

        [Test]
        public void TickLabel_CoversWholeTimeline()
        {
            Assert.AreEqual("18:00", GameTime.TickLabel(0));
            Assert.AreEqual("18:30", GameTime.TickLabel(3));
            Assert.AreEqual("19:00", GameTime.TickLabel(6));
        }

        [Test]
        public void TickLabel_OutOfRange_ReturnsPlaceholder()
        {
            Assert.AreEqual("--:--", GameTime.TickLabel(-1));
            Assert.AreEqual("--:--", GameTime.TickLabel(7));
        }

        [Test]
        public void TryParseTick_RoundTripsEveryTick()
        {
            for (int tick = GameTime.FirstTick; tick <= GameTime.LastTick; tick++)
            {
                int parsed;
                Assert.IsTrue(GameTime.TryParseTick(GameTime.TickLabel(tick), out parsed), GameTime.TickLabel(tick));
                Assert.AreEqual(tick, parsed);
            }
        }

        [Test]
        public void TryParseTick_RejectsOffGridAndOutOfRange()
        {
            int tick;
            Assert.IsFalse(GameTime.TryParseTick("18:15", out tick), "10분 칸의 시작이 아니다");
            Assert.IsFalse(GameTime.TryParseTick("18:30:01", out tick), "10분 칸의 시작이 아니다");
            Assert.IsFalse(GameTime.TryParseTick("17:50", out tick), "시작 전");
            Assert.IsFalse(GameTime.TryParseTick("19:10", out tick), "끝난 뒤");
            Assert.IsFalse(GameTime.TryParseTick("abc", out tick));
            Assert.IsFalse(GameTime.TryParseTick("", out tick));
            Assert.AreEqual(-1, tick);
        }

        [Test]
        public void ClampTick_KeepsTickInsideTimeline()
        {
            Assert.AreEqual(0, GameTime.ClampTick(-5));
            Assert.AreEqual(6, GameTime.ClampTick(99));
            Assert.AreEqual(4, GameTime.ClampTick(4));
        }

        [Test]
        public void Present_IsNineteenHundred()
        {
            Assert.AreEqual("19:00", GameTime.ToLabel(GameTime.PresentMs));
            Assert.AreEqual("19:00", GameTime.TickLabel(GameTime.PresentTick));
            Assert.AreEqual(GameTime.PresentTick, GameTime.TickOf(GameTime.PresentMs));
        }
    }
}
