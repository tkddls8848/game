using Detective.Core;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>틱 ↔ 시각 문자열 변환. 채점·목격 판정이 전부 틱 기준이라 여기가 틀리면 전부 틀린다.</summary>
    public class TimeTests
    {
        [Test]
        public void ToLabel_CoversWholeTimeline()
        {
            Assert.AreEqual("18:00", GameTime.ToLabel(0));
            Assert.AreEqual("18:30", GameTime.ToLabel(3));
            Assert.AreEqual("19:00", GameTime.ToLabel(6));
        }

        [Test]
        public void ToLabel_OutOfRange_ReturnsPlaceholder()
        {
            Assert.AreEqual("--:--", GameTime.ToLabel(-1));
            Assert.AreEqual("--:--", GameTime.ToLabel(7));
        }

        [Test]
        public void TryParse_RoundTripsEveryTick()
        {
            for (int tick = GameTime.FirstTick; tick <= GameTime.LastTick; tick++)
            {
                int parsed;
                Assert.IsTrue(GameTime.TryParse(GameTime.ToLabel(tick), out parsed), GameTime.ToLabel(tick));
                Assert.AreEqual(tick, parsed);
            }
        }

        [Test]
        public void TryParse_RejectsOffGridAndOutOfRange()
        {
            int tick;
            Assert.IsFalse(GameTime.TryParse("18:15", out tick), "10분 단위가 아니다");
            Assert.IsFalse(GameTime.TryParse("17:50", out tick), "시작 전");
            Assert.IsFalse(GameTime.TryParse("19:10", out tick), "끝난 뒤");
            Assert.IsFalse(GameTime.TryParse("abc", out tick));
            Assert.IsFalse(GameTime.TryParse("", out tick));
            Assert.AreEqual(-1, tick);
        }

        [Test]
        public void Clamp_KeepsTickInsideTimeline()
        {
            Assert.AreEqual(0, GameTime.Clamp(-5));
            Assert.AreEqual(6, GameTime.Clamp(99));
            Assert.AreEqual(4, GameTime.Clamp(4));
        }

        [Test]
        public void PresentTick_IsNineteenHundred()
        {
            Assert.AreEqual("19:00", GameTime.ToLabel(GameTime.PresentTick));
        }
    }
}
