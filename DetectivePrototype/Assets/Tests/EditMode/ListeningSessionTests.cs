using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// Phase U-2 — 청취점과 가청 판정(DEVELOPMENT_PLAN_UNHEARD.md).
    /// 완료 조건은 "같은 시각에 위치만 바꾸면 보이는 발화가 달라진다"이다.
    /// 여기서는 한 순간이 아니라 **회차를 끝까지 들었을 때의 결과**까지 위치로 갈린다는 것을 본다.
    ///
    /// 방은 TestCaseFactory의 a(0~10) · b(10~20) · study(20~30) 한 줄 배치를 쓴다.
    /// a와 study는 벽을 맞대지 않으므로 서로 아무것도 들리지 않고, 가운데 b에서는 양쪽이 웅얼거림으로 들린다.
    /// </summary>
    public class ListeningSessionTests
    {
        private const string RoomA = "a";
        private const string RoomB = "b";
        private const string RoomStudy = "study";

        private const string UttA = "uA";
        private const string UttStudy = "uS";
        private const string UttALate = "uA2";

        private RoomLayout _layout;
        private ScriptTimeline _timeline;
        private AudibilityModel _audibility;

        [SetUp]
        public void SetUp()
        {
            _layout = RoomLayout.FromTable(TestCaseFactory.Rooms());
            _timeline = new ScriptTimeline(BuildScript());
            _audibility = new AudibilityModel(_layout);
        }

        /// <summary>a와 study에서 한 번씩 겹쳐 말하고, 뒤에 a에서 한 번 더 말한다.</summary>
        private static ScriptDefinition BuildScript()
        {
            return new ScriptDefinition
            {
                caseId = "listening_session_test",
                durationMs = 10000,
                speakers = new[]
                {
                    new SpeakerDefinition { voiceId = "v1", npcId = "npc_a" },
                    new SpeakerDefinition { voiceId = "v2", npcId = "npc_b" }
                },
                utterances = new[]
                {
                    new Utterance { id = UttA,     startMs = 1000, durationMs = 2000, voiceId = "v1", room = RoomA,     text = "A에서 한 말", clip = "" },
                    new Utterance { id = UttStudy, startMs = 1500, durationMs = 2000, voiceId = "v2", room = RoomStudy, text = "서재에서 한 말", clip = "" },
                    new Utterance { id = UttALate, startMs = 6000, durationMs = 1000, voiceId = "v1", room = RoomA,     text = "A에서 나중에", clip = "" }
                }
            }.Normalized();
        }

        private ListeningSession NewSession(string startRoom)
        {
            var session = new ListeningSession(_timeline, _audibility);
            session.MoveTo(startRoom);
            session.Transport.Play();
            return session;
        }

        /// <summary>회차를 100ms씩 끝까지 흘린다. moveAtMs에 도달하면 그 방으로 옮긴다.</summary>
        private static void PlayThrough(ListeningSession session, int moveAtMs = -1, string moveTo = null)
        {
            bool moved = moveAtMs < 0;
            while (!session.Transport.AtEnd)
            {
                if (!moved && session.PositionMs >= moveAtMs)
                {
                    session.MoveTo(moveTo);
                    moved = true;
                }
                session.Advance(100);
            }
        }

        // ── U-2 완료 조건 ────────────────────────────────────────

        [Test]
        public void SameRun_DifferentPositions_YieldDifferentHeardSets()
        {
            ListeningSession stayed = NewSession(RoomA);
            PlayThrough(stayed);

            ListeningSession wandered = NewSession(RoomA);
            PlayThrough(wandered, 2000, RoomStudy);

            // 가만히 있었으면 서재의 말은 벽 두 개 너머라 존재조차 모른다.
            Assert.AreEqual(Audibility.None, stayed.HeardLevel(UttStudy), "a에 머물면 서재 발화는 닿지 않아야 한다");
            Assert.AreEqual(Audibility.Full, stayed.HeardLevel(UttA));

            // 옮겨 갔으면 서재의 말을 듣는다. 대신 뒤에 a에서 한 말을 놓친다.
            Assert.AreEqual(Audibility.Full, wandered.HeardLevel(UttStudy), "서재로 옮기면 그 말이 들려야 한다");
            Assert.AreEqual(Audibility.None, wandered.HeardLevel(UttALate), "옮겨 간 뒤 a의 말은 놓쳐야 한다");

            Assert.AreNotEqual(stayed.FullyHeardCount + ":" + stayed.HeardLevel(UttStudy),
                               wandered.FullyHeardCount + ":" + wandered.HeardLevel(UttStudy),
                               "위치만 바꿨는데 회차 결과가 같으면 U-2가 성립하지 않는다");
        }

        [Test]
        public void SameInstant_DifferentRooms_ShowDifferentBubbles()
        {
            ListeningSession inA = NewSession(RoomA);
            ListeningSession inStudy = NewSession(RoomStudy);
            inA.SeekTo(2000);
            inStudy.SeekTo(2000); // 두 발화가 함께 울리는 순간

            Assert.AreEqual(1, inA.Current.Count);
            Assert.AreEqual(UttA, inA.Current[0].UtteranceId);

            Assert.AreEqual(1, inStudy.Current.Count);
            Assert.AreEqual(UttStudy, inStudy.Current[0].UtteranceId);
        }

        [Test]
        public void BetweenTheTwo_BothAreMuffled_AndCarryNoContent()
        {
            ListeningSession inB = NewSession(RoomB);
            inB.SeekTo(2000);

            Assert.AreEqual(2, inB.Current.Count, "가운데 방에서는 양쪽이 다 들려야 한다");
            for (int i = 0; i < inB.Current.Count; i++)
            {
                PerceivedUtterance p = inB.Current[i];
                Assert.AreEqual(Audibility.Muffled, p.Level);
                Assert.AreEqual(string.Empty, p.Text, "웅얼거림이 내용을 흘리면 옮겨 갈 이유가 사라진다");
                Assert.AreEqual(string.Empty, p.VoiceId, "웅얼거림은 누구인지도 알려주지 않는다");
                Assert.IsFalse(string.IsNullOrEmpty(p.Room), "어느 쪽에서 나는 소리인지는 알아야 한다");
            }
            Assert.AreEqual(0, inB.FullyHeardCount);
            Assert.AreEqual(2, inB.MuffledOnly().Count);
        }

        // ── 기록의 규칙 ──────────────────────────────────────────

        [Test]
        public void SkippedStretch_IsNotCountedAsHeard()
        {
            ListeningSession session = NewSession(RoomA);
            session.SeekTo(5000); // uA(1000~3000)를 통째로 건너뛴다
            PlayThrough(session);

            Assert.AreEqual(Audibility.None, session.HeardLevel(UttA), "건너뛴 발화를 들었다고 쳐 주면 되돌려 들을 이유가 없어진다");
            Assert.AreEqual(Audibility.Full, session.HeardLevel(UttALate));
        }

        [Test]
        public void MuffledFirst_ThenMovingCloser_UpgradesOnlyTheOneYouWalkedTo()
        {
            // b방은 a와 study 사이라, 1600ms에는 양쪽 발화가 동시에 웅얼거림으로 잡힌다.
            ListeningSession session = NewSession(RoomB);
            session.SeekTo(1600);
            Assert.AreEqual(Audibility.Muffled, session.HeardLevel(UttA));
            Assert.AreEqual(Audibility.Muffled, session.HeardLevel(UttStudy));

            session.MoveTo(RoomStudy);

            // 걸어간 쪽만 내용이 열린다. 반대쪽은 벽 두 개 너머가 되어 웅얼거림으로 스친 채 남는다 —
            // 한 번에 둘 다 가질 수 없다는 것이 이 게임의 긴장이다.
            Assert.AreEqual(Audibility.Full, session.HeardLevel(UttStudy), "가까이 가면 기록이 올라가야 한다");
            Assert.AreEqual(Audibility.Muffled, session.HeardLevel(UttA), "멀어진 쪽은 들은 만큼만 남아야 한다");

            List<string> muffled = session.MuffledOnly();
            CollectionAssert.DoesNotContain(muffled, UttStudy, "내용을 들었으면 웅얼거림 목록에서 빠진다");
            CollectionAssert.Contains(muffled, UttA, "아직 내용을 못 들은 것은 목록에 남아 되돌아갈 이유가 된다");
        }

        [Test]
        public void OnTheThreshold_NothingIsAudible()
        {
            ListeningSession session = NewSession(RoomA);
            session.MoveTo(string.Empty); // 벽 두께 안 — 어느 방에도 속하지 않는다
            PlayThrough(session);

            Assert.AreEqual(0, session.Current.Count);
            Assert.AreEqual(0, session.FullyHeardCount);
            Assert.AreEqual(Audibility.None, session.HeardLevel(UttA));
        }

        [Test]
        public void Restart_RewindsButKeepsWhatWasAlreadyHeard()
        {
            ListeningSession session = NewSession(RoomA);
            PlayThrough(session);
            int heard = session.FullyHeardCount;
            Assert.Greater(heard, 0);

            session.Restart();
            Assert.AreEqual(0, session.PositionMs);
            Assert.AreEqual(heard, session.FullyHeardCount, "두 번째 청취의 의미는 이미 들은 것 위에 쌓는 데 있다");

            session.ForgetHeard();
            Assert.AreEqual(0, session.FullyHeardCount);
        }

        // ── 방 환경음 설정 검사 ──────────────────────────────────

        [Test]
        public void RoomAmbientVolume_OutOfRange_IsCaught()
        {
            CaseDatabase database = TestCaseFactory.Database();
            List<string> errors = ArtManifestValidator.Validate(
                ManifestWith(new RoomArt { roomId = RoomA, ambient = "Audio/Ambient/fireplace", ambientVolume = 1.4f }), database);

            Assert.AreEqual(1, errors.Count, string.Join(" / ", errors.ToArray()));
            StringAssert.Contains("ambientVolume", errors[0]);
        }

        [Test]
        public void RoomAmbient_WithZeroVolume_IsCaught()
        {
            CaseDatabase database = TestCaseFactory.Database();
            List<string> errors = ArtManifestValidator.Validate(
                ManifestWith(new RoomArt { roomId = RoomA, ambient = "Audio/Ambient/fireplace", ambientVolume = 0f }), database);

            Assert.AreEqual(1, errors.Count, string.Join(" / ", errors.ToArray()));
            StringAssert.Contains("들리지 않는다", errors[0]);
        }

        [Test]
        public void RoomWithoutAmbient_IsFine()
        {
            CaseDatabase database = TestCaseFactory.Database();
            List<string> errors = ArtManifestValidator.Validate(
                ManifestWith(new RoomArt { roomId = RoomA, tileSize = 4f }), database);

            Assert.AreEqual(0, errors.Count, string.Join(" / ", errors.ToArray()));
        }

        private static ArtManifest ManifestWith(RoomArt room)
        {
            if (room.tileSize <= 0f) room.tileSize = 4f;
            return new ArtManifest { rooms = new[] { room } }.Normalized();
        }
    }
}
