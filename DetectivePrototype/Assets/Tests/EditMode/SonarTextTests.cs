using System.Collections.Generic;
using Detective.Core;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 청취(소나) 화면의 순수 계산. 화면(SonarView)은 이것을 그릴 뿐이다.
    /// 웅얼거림 가림은 원문 글자를 한 자도 흘리지 않아야 하고, 파문은 재생 위치에만 묶여야 한다.
    /// </summary>
    public class SonarTextTests
    {
        [Test]
        public void Garble_HidesEveryLetter_KeepsOnlySpaces()
        {
            string garbled = SonarText.Garble("초록 넥타이를 맨 남자예요.");
            Assert.AreEqual("초록 넥타이를 맨 남자예요.".Length, garbled.Length, "길이(리듬)만 남는다");
            Assert.AreEqual(3, garbled.Split(' ').Length - 1, "띄어쓰기 수는 그대로");
            foreach (char c in garbled) Assert.IsTrue(c == ' ' || c == SonarText.Block, "원문 글자가 새면 안 된다: " + c);
            Assert.AreEqual(string.Empty, SonarText.Garble(null));
            Assert.AreEqual(string.Empty, SonarText.Garble(string.Empty));
        }

        [Test]
        public void RipplePhase_RingsStartStaggered_AndFollowPlaybackMs()
        {
            float p;
            Assert.IsTrue(SonarText.RipplePhase(0, 2000, 0, 4, out p));
            Assert.AreEqual(0f, p);
            Assert.IsFalse(SonarText.RipplePhase(0, 2000, 1, 4, out p), "두 번째 링은 주기의 1/4 뒤에 출발");
            Assert.IsTrue(SonarText.RipplePhase(500, 2000, 1, 4, out p));
            Assert.AreEqual(0f, p);
            Assert.IsTrue(SonarText.RipplePhase(1500, 2000, 0, 4, out p));
            Assert.AreEqual(0.75f, p, 1e-6f);
            Assert.IsTrue(SonarText.RipplePhase(2100, 2000, 0, 4, out p));
            Assert.AreEqual(0.05f, p, 1e-6f, "한 바퀴 돌면 다시 중심에서");
            Assert.IsFalse(SonarText.RipplePhase(-1, 2000, 0, 4, out p), "시작 전");
            Assert.IsFalse(SonarText.RipplePhase(100, 0, 0, 4, out p), "주기 0은 없다");
            Assert.IsFalse(SonarText.RipplePhase(100, 2000, 4, 4, out p), "링 번호는 개수 안에서");
        }

        [Test]
        public void Clock_Speed_VoiceName_Formatting()
        {
            Assert.AreEqual("0:34.0", SonarText.Clock(34000));
            Assert.AreEqual("1:30.0", SonarText.Clock(90000));
            Assert.AreEqual("0:05.9", SonarText.Clock(5999));
            Assert.AreEqual("0:00.0", SonarText.Clock(-5));

            Assert.AreEqual("×1", SonarText.Speed(100));
            Assert.AreEqual("×0.5", SonarText.Speed(50));
            Assert.AreEqual("×2", SonarText.Speed(200));
            Assert.AreEqual("×0.25", SonarText.Speed(25));

            Assert.AreEqual("목소리 2", SonarText.VoiceName("v2"));
            Assert.AreEqual("목소리 12", SonarText.VoiceName("V12"));
            Assert.AreEqual("ghost", SonarText.VoiceName("ghost"));
            Assert.AreEqual("목소리", SonarText.VoiceName(null));
        }

        // ── 회차 기록을 읽는 쪽 ────────────────────────────────────

        private const string RoomA = "a";
        private const string RoomB = "b";
        private const string RoomStudy = "study";

        /// <summary>a·study에서 겹쳐 말하고, 뒤에 a에서 한 번 더. 목소리는 v2가 먼저 등장한다.</summary>
        private static ScriptTimeline Timeline()
        {
            return new ScriptTimeline(new ScriptDefinition
            {
                caseId = "sonar_test",
                durationMs = 10000,
                utterances = new[]
                {
                    new Utterance { id = "uS", startMs = 1000, durationMs = 2000, voiceId = "v2", room = RoomStudy, text = "서재에서 한 말" },
                    new Utterance { id = "uA", startMs = 1500, durationMs = 2000, voiceId = "v1", room = RoomA, text = "A에서 한 말" },
                    new Utterance { id = "uA2", startMs = 6000, durationMs = 1000, voiceId = "v1", room = RoomA, text = "A에서 나중에" }
                }
            }.Normalized());
        }

        private static ListeningSession Session(string room)
        {
            var session = new ListeningSession(Timeline(), new AudibilityModel(RoomLayout.FromTable(TestCaseFactory.Rooms())));
            session.MoveTo(room);
            return session;
        }

        [Test]
        public void Voices_InOrderOfFirstUtterance()
        {
            CollectionAssert.AreEqual(new[] { "v2", "v1" }, SonarText.Voices(Timeline()));
            Assert.IsEmpty(SonarText.Voices(null));
        }

        [Test]
        public void FullyHeard_And_MissedSoFar_FollowTheSessionRecord()
        {
            // 가운데 b방: 양쪽이 웅얼거림으로만 스친다.
            ListeningSession inB = Session(RoomB);
            inB.SeekTo(2000);
            Assert.IsEmpty(SonarText.FullyHeard(inB), "웅얼거림은 들은 것이 아니다");
            Assert.AreEqual(0, SonarText.MissedSoFar(inB), "아직 끝난 발화가 없다");
            inB.SeekTo(10000);
            Assert.AreEqual(3, SonarText.MissedSoFar(inB), "끝까지 갔는데 내용을 들은 것이 없다");

            // a방: 같은 방 발화는 온전히, 서재 발화는 못 듣는다.
            ListeningSession inA = Session(RoomA);
            inA.SeekTo(2000);
            List<Utterance> heard = SonarText.FullyHeard(inA);
            Assert.AreEqual(1, heard.Count);
            Assert.AreEqual("uA", heard[0].id);
            inA.SeekTo(4000);
            Assert.AreEqual(1, SonarText.MissedSoFar(inA), "서재 발화(uS)는 끝났고 못 들었다");
            Assert.AreEqual(0, SonarText.MissedSoFar(null));
            Assert.IsEmpty(SonarText.FullyHeard(null));
        }
    }
}
