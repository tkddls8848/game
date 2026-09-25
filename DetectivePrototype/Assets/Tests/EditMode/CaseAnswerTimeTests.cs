using Detective.Core;
using Detective.Data;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 정답 시각을 두 축으로 적을 수 있게 한 뒤의 규칙을 못박는다.
    ///
    /// 저택 사건(case_01)은 18:00~19:00을 10분 7칸으로 쓰므로 tick으로 적는다.
    /// 엿듣기 사건(case_02)은 한 회차가 10분인데 틱 한 칸도 10분이라 회차 전체가 칸 하나다 —
    /// tick으로는 "회차 시작 4분 1.5초"를 적을 수 없어서 timeMs를 따로 둔다.
    ///
    /// 여기서 조용히 깨질 수 있는 지점이 하나 있다. JsonUtility는 빠진 int를 0으로 채우는데
    /// 0은 유효한 시각이다. timeMs를 적지 않은 옛 사건이 0(회차 시작)으로 읽히면
    /// 정답 시각이 바뀌고, 채점은 멀쩡히 돌면서 답만 틀리게 된다.
    /// </summary>
    public class CaseAnswerTimeTests
    {
        [Test]
        public void TickOnly_StillReadsFromTick()
        {
            var answer = new CaseAnswer { tick = 3, timeMs = GameTime.NoTime };
            Assert.AreEqual(GameTime.TickToMs(3), answer.TimeMs);
            Assert.AreNotEqual(0, answer.TimeMs, "18:30이 18:00으로 읽히면 안 된다");
        }

        [Test]
        public void ExplicitTimeMs_WinsOverTick()
        {
            var answer = new CaseAnswer { tick = 0, timeMs = 241500 };
            Assert.AreEqual(241500, answer.TimeMs,
                "회차 안의 정확한 시각을 적었으면 그것이 이긴다");
        }

        [Test]
        public void NeitherWritten_IsNoTime_NotZero()
        {
            // 둘 다 없으면 "시각 없음"이어야 한다. 0으로 떨어지면 회차 시작이 정답이 돼 버린다.
            var answer = new CaseAnswer();
            Assert.AreEqual(GameTime.NoTime, answer.TimeMs);
        }

        [Test]
        public void ZeroIsARealTime_SoItMustBeWrittenDeliberately()
        {
            // 0은 버림값이 아니라 회차 시작이다. 적어 넣으면 그대로 쓰여야 한다.
            var answer = new CaseAnswer { tick = -1, timeMs = 0 };
            Assert.AreEqual(0, answer.TimeMs);
        }

        [Test]
        public void OutOfRangeTimeMs_FallsBackToTick()
        {
            var answer = new CaseAnswer { tick = 2, timeMs = GameTime.EndMs + 1 };
            Assert.AreEqual(GameTime.TickToMs(2), answer.TimeMs,
                "범위 밖 값은 무시하고 tick으로 돌아간다");
        }
    }
}
