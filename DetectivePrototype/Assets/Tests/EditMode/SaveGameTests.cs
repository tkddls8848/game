using System.Collections.Generic;
using Detective.Core;
using Detective.Eavesdrop;
using Detective.Investigation;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 저장·불러오기(DEVELOPMENT_PLAN_RELEASE.md §2.2). 파일은 Unity 쪽이 쓰고,
    /// 여기서 지키는 것은 <b>무엇을 적고 무엇을 되돌리는가</b>다.
    ///
    /// 방은 TestCaseFactory의 a(0~10) · b(10~20) · study(20~30) 한 줄 배치를 쓴다.
    /// a와 study는 벽을 맞대지 않으므로 서로 무음이고, 가운데 b에서는 양쪽이 웅얼거림으로 들린다.
    /// </summary>
    public class SaveGameTests
    {
        private const string RoomA = "a";
        private const string RoomB = "b";
        private const string RoomStudy = "study";

        private const string UttA = "uA";
        private const string UttStudy = "uS";
        private const string UttALate = "uA2";

        private const string CaseId = "save_test";
        private const long SavedAt = 1700000000000L;

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

        private static ScriptDefinition BuildScript()
        {
            return new ScriptDefinition
            {
                caseId = CaseId,
                durationMs = 10000,
                speakers = new[]
                {
                    new SpeakerDefinition { voiceId = "v1", npcId = "culprit" },
                    new SpeakerDefinition { voiceId = "v2", npcId = "witness" }
                },
                utterances = new[]
                {
                    new Utterance { id = UttA,     startMs = 1000, durationMs = 2000, voiceId = "v1", room = RoomA,     text = "A에서 한 말", clip = "" },
                    new Utterance { id = UttStudy, startMs = 1500, durationMs = 2000, voiceId = "v2", room = RoomStudy, text = "서재에서 한 말", clip = "" },
                    new Utterance { id = UttALate, startMs = 6000, durationMs = 1000, voiceId = "v1", room = RoomA,     text = "A에서 나중에", clip = "" }
                }
            }.Normalized();
        }

        private ListeningSession NewSession(string room)
        {
            var session = new ListeningSession(_timeline, _audibility);
            session.MoveTo(room);
            return session;
        }

        private static VoiceAssignment Board()
        {
            return new VoiceAssignment(new[] { "v1", "v2" });
        }

        private SaveGame Saved(ListeningSession session, VoiceAssignment voices, EvidenceLog evidence)
        {
            return SaveGameCodec.Capture(CaseId, session, voices, evidence, SavedAt);
        }

        // ---------------------------------------------------------------- 뜨기

        [Test]
        public void Capture_WritesCurrentVersionAndCaseAndClock()
        {
            SaveGame save = Saved(NewSession(RoomA), Board(), new EvidenceLog());

            Assert.AreEqual(SaveGame.CurrentVersion, save.version);
            Assert.AreEqual(CaseId, save.caseId);
            Assert.AreEqual(SavedAt, save.savedAtUnixMs);
        }

        [Test]
        public void Capture_KeepsPositionAndListenerRoom()
        {
            ListeningSession session = NewSession(RoomStudy);
            session.SeekTo(4200);

            SaveGame save = Saved(session, Board(), new EvidenceLog());

            Assert.AreEqual(4200, save.positionMs);
            Assert.AreEqual(RoomStudy, save.listenerRoom);
        }

        /// <summary>a방에 서서 1.5초에 서면 a의 발화는 온전히, 서재의 발화는 아무것도 들리지 않는다.</summary>
        [Test]
        public void Capture_WritesOnlyWhatWasActuallyHeard()
        {
            ListeningSession session = NewSession(RoomA);
            session.SeekTo(1500);

            SaveGame save = Saved(session, Board(), new EvidenceLog());

            Assert.AreEqual(1, save.heard.Length, "닿지 않은 발화는 적지 않는다.");
            Assert.AreEqual(UttA, save.heard[0].utteranceId);
            Assert.AreEqual(Audibility.Full, save.heard[0].Level);
        }

        /// <summary>b방에서는 양쪽이 벽 너머로만 들린다 — 웅얼거림도 기록이다.</summary>
        [Test]
        public void Capture_WritesMuffledEncountersToo()
        {
            ListeningSession session = NewSession(RoomB);
            session.SeekTo(1800);

            SaveGame save = Saved(session, Board(), new EvidenceLog());

            Assert.AreEqual(2, save.heard.Length);
            for (int i = 0; i < save.heard.Length; i++)
                Assert.AreEqual(Audibility.Muffled, save.heard[i].Level, save.heard[i].utteranceId);
        }

        [Test]
        public void Capture_WritesVoiceBoardAndEvidenceInOrder()
        {
            VoiceAssignment voices = Board();
            voices.Assign("v2", "witness");

            var evidence = new EvidenceLog();
            evidence.Collect("ev_log");
            evidence.Collect("ev_glass");
            evidence.Collect("ev_log"); // 두 번 조사해도 한 줄이다

            SaveGame save = Saved(NewSession(RoomA), voices, evidence);

            Assert.AreEqual(1, save.voices.Length);
            Assert.AreEqual("v2", save.voices[0].voiceId);
            Assert.AreEqual("witness", save.voices[0].npcId);

            CollectionAssert.AreEqual(new[] { "ev_log", "ev_glass" }, save.evidence);
        }

        // ---------------------------------------------------------------- 되돌리기

        [Test]
        public void RoundTrip_RestoresEverything()
        {
            ListeningSession session = NewSession(RoomA);
            session.SeekTo(1500);
            session.MoveTo(RoomB);
            session.SeekTo(1800);

            VoiceAssignment voices = Board();
            voices.Assign("v1", "culprit");
            var evidence = new EvidenceLog();
            evidence.Collect("ev_glass");

            SaveGame save = Saved(session, voices, evidence);
            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.AreEqual(SaveCompatibility.Current, progress.Compatibility);
            CollectionAssert.IsEmpty(progress.Problems);
            Assert.AreEqual(1800, progress.PositionMs);
            Assert.AreEqual(RoomB, progress.ListenerRoom);
            Assert.AreEqual(CaseId, progress.CaseId);
            Assert.AreEqual(SavedAt, progress.SavedAtUnixMs);

            Assert.AreEqual(Audibility.Full, progress.HeardLevel(UttA), "a방에서 온전히 들은 것은 등급이 내려가지 않는다.");
            Assert.AreEqual(Audibility.Muffled, progress.HeardLevel(UttStudy));
            Assert.AreEqual(Audibility.None, progress.HeardLevel(UttALate));
            Assert.AreEqual(1, progress.FullyHeardCount);

            Assert.AreEqual(1, progress.VoicePairs.Length);
            Assert.AreEqual("culprit", progress.VoicePairs[0].npcId);
            CollectionAssert.AreEqual(new[] { "ev_glass" }, progress.Evidence);
        }

        [Test]
        public void ApplyTo_PutsSessionBoardAndEvidenceBack()
        {
            ListeningSession source = NewSession(RoomStudy);
            source.SeekTo(2000);

            VoiceAssignment sourceVoices = Board();
            sourceVoices.Assign("v1", "culprit");
            sourceVoices.Assign("v2", "witness");

            var sourceEvidence = new EvidenceLog();
            sourceEvidence.Collect("ev_glass");

            SaveGame save = Saved(source, sourceVoices, sourceEvidence);
            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            ListeningSession fresh = NewSession(RoomA);
            VoiceAssignment freshVoices = Board();
            var freshEvidence = new EvidenceLog();

            List<string> problems = SaveGameCodec.ApplyTo(progress, fresh, freshVoices, freshEvidence);

            CollectionAssert.IsEmpty(problems);
            Assert.AreEqual(2000, fresh.PositionMs);
            Assert.AreEqual(RoomStudy, fresh.ListenerRoom);
            Assert.AreEqual("culprit", freshVoices.AssignedNpc("v1"));
            Assert.AreEqual("witness", freshVoices.AssignedNpc("v2"));
            Assert.IsTrue(freshEvidence.Has("ev_glass"));
        }

        /// <summary>
        /// 되돌린 세션은 그 자리에서 들리는 것을 곧바로 듣는다 — 귀가 거기 있으니 당연하다.
        /// 그러나 <b>저장 이전에 들었던 것</b>까지 세션이 되찾지는 않는다. 그 몫은 HeardHistory가 한다.
        /// </summary>
        [Test]
        public void ApplyTo_DoesNotForgeSessionHistory()
        {
            ListeningSession source = NewSession(RoomA);
            source.SeekTo(1500);   // uA를 온전히 듣는다
            source.SeekTo(9000);   // 아무것도 울리지 않는 자리로 옮겨 저장한다

            SaveGame save = Saved(source, Board(), new EvidenceLog());
            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            ListeningSession fresh = NewSession(RoomA);
            SaveGameCodec.ApplyTo(progress, fresh, Board(), new EvidenceLog());

            Assert.AreEqual(Audibility.None, fresh.HeardLevel(UttA), "세션은 지나치지 않은 것을 들었다고 치지 않는다.");
            Assert.AreEqual(Audibility.Full, progress.HeardLevel(UttA), "저장은 들었던 것을 그대로 쥐고 있다.");
        }

        // ---------------------------------------------------------------- 형식 번호

        [Test]
        public void Restore_RejectsSaveWithNoVersion()
        {
            var save = new SaveGame { version = 0, positionMs = 5000, listenerRoom = RoomA };

            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.AreEqual(SaveCompatibility.Unversioned, progress.Compatibility);
            Assert.IsFalse(progress.IsUsable);
            Assert.AreEqual(0, progress.PositionMs, "거부한 저장은 반쯤 되살리지 않는다.");
            Assert.AreEqual(string.Empty, progress.ListenerRoom);
        }

        [Test]
        public void Restore_RejectsSaveFromNewerBuild()
        {
            SaveGame save = Saved(NewSession(RoomA), Board(), new EvidenceLog());
            save.version = SaveGame.CurrentVersion + 1;

            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.AreEqual(SaveCompatibility.FromFuture, progress.Compatibility);
            Assert.IsFalse(progress.IsUsable);
            Assert.AreEqual(SaveGame.CurrentVersion + 1, progress.Version, "거부해도 적혀 있던 번호는 알려 준다.");
        }

        [Test]
        public void Restore_RejectsMissingSave()
        {
            RestoredProgress progress = SaveGameCodec.Restore(null, _timeline, _audibility);

            Assert.AreEqual(SaveCompatibility.Missing, progress.Compatibility);
            Assert.IsFalse(progress.IsUsable);
        }

        [Test]
        public void TryUpgrade_LeavesCurrentSaveAloneAndRefusesUnversioned()
        {
            SaveGame current = Saved(NewSession(RoomA), Board(), new EvidenceLog());
            Assert.IsTrue(SaveGameCodec.TryUpgrade(current));
            Assert.AreEqual(SaveGame.CurrentVersion, current.version);

            Assert.IsFalse(SaveGameCodec.TryUpgrade(new SaveGame { version = 0 }));
            Assert.IsFalse(SaveGameCodec.TryUpgrade(null));
        }

        // ---------------------------------------------------------------- 망가진 저장

        [Test]
        public void Restore_ClampsPositionOutsideTheRun()
        {
            SaveGame save = Saved(NewSession(RoomA), Board(), new EvidenceLog());
            save.positionMs = 99999;

            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.AreEqual(_timeline.DurationMs, progress.PositionMs);
            Assert.AreEqual(1, progress.Problems.Count);
        }

        [Test]
        public void Restore_ClearsListenerRoomThatNoLongerExists()
        {
            SaveGame save = Saved(NewSession(RoomA), Board(), new EvidenceLog());
            save.listenerRoom = "room_that_burned_down";

            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.AreEqual(string.Empty, progress.ListenerRoom);
            Assert.AreEqual(1, progress.Problems.Count);
            Assert.IsTrue(progress.IsUsable, "방 하나가 사라졌다고 저장 전체를 버리지는 않는다.");
        }

        [Test]
        public void Restore_DropsUtterancesThatLeftTheScript()
        {
            SaveGame save = Saved(NewSession(RoomA), Board(), new EvidenceLog());
            save.heard = new[]
            {
                new HeardEntry { utteranceId = UttA, level = (int)Audibility.Full },
                new HeardEntry { utteranceId = "u_deleted", level = (int)Audibility.Full }
            };

            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.AreEqual(Audibility.Full, progress.HeardLevel(UttA));
            Assert.AreEqual(Audibility.None, progress.HeardLevel("u_deleted"));
            Assert.AreEqual(1, progress.Heard.Count);
            Assert.AreEqual(1, progress.Problems.Count);
        }

        [Test]
        public void Restore_DropsUnknownAudibilityLevel()
        {
            SaveGame save = Saved(NewSession(RoomA), Board(), new EvidenceLog());
            save.heard = new[] { new HeardEntry { utteranceId = UttA, level = 99 } };

            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.AreEqual(Audibility.None, progress.HeardLevel(UttA));
            Assert.AreEqual(1, progress.Problems.Count);
        }

        [Test]
        public void Restore_KeepsTheBetterOfDuplicateHeardRows()
        {
            SaveGame save = Saved(NewSession(RoomA), Board(), new EvidenceLog());
            save.heard = new[]
            {
                new HeardEntry { utteranceId = UttA, level = (int)Audibility.Muffled },
                new HeardEntry { utteranceId = UttA, level = (int)Audibility.Full }
            };

            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.AreEqual(Audibility.Full, progress.HeardLevel(UttA));
            Assert.AreEqual(1, progress.Heard.Count);
            Assert.AreEqual((int)Audibility.Full, progress.Heard[0].level);
            Assert.AreEqual(1, progress.Problems.Count);
        }

        [Test]
        public void Restore_DropsDuplicateVoiceRowsAndNamelessOnes()
        {
            SaveGame save = Saved(NewSession(RoomA), Board(), new EvidenceLog());
            save.voices = new[]
            {
                new SpeakerDefinition { voiceId = "v1", npcId = "culprit" },
                new SpeakerDefinition { voiceId = "v1", npcId = "witness" },
                new SpeakerDefinition { voiceId = "v2", npcId = "" }
            };

            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.AreEqual(1, progress.VoicePairs.Length);
            Assert.AreEqual("culprit", progress.VoicePairs[0].npcId);
            Assert.AreEqual(2, progress.Problems.Count);
        }

        [Test]
        public void Restore_SurvivesNullArraysFromJson()
        {
            var save = new SaveGame
            {
                version = SaveGame.CurrentVersion,
                caseId = null,
                listenerRoom = null,
                heard = null,
                voices = null,
                evidence = null
            };

            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            Assert.IsTrue(progress.IsUsable);
            Assert.AreEqual(string.Empty, progress.ListenerRoom);
            Assert.AreEqual(0, progress.Heard.Count);
            Assert.AreEqual(0, progress.VoicePairs.Length);
            Assert.AreEqual(0, progress.Evidence.Count);
        }

        [Test]
        public void MatchesCase_OnlyComplainsWhenBothSidesNameACase()
        {
            var save = new SaveGame { caseId = CaseId };

            Assert.IsTrue(save.MatchesCase(CaseId));
            Assert.IsFalse(save.MatchesCase("other_case"));
            Assert.IsTrue(save.MatchesCase(string.Empty), "사건 이름이 없으면 대조를 포기한다.");
        }

        // ---------------------------------------------------------------- 이어듣기

        [Test]
        public void HeardHistory_TakesTheBetterOfSaveAndLiveSession()
        {
            ListeningSession source = NewSession(RoomB);
            source.SeekTo(1800); // 둘 다 웅얼거림으로 스친다

            SaveGame save = Saved(source, Board(), new EvidenceLog());
            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            ListeningSession fresh = NewSession(RoomA);
            SaveGameCodec.ApplyTo(progress, fresh, Board(), new EvidenceLog());

            var history = new HeardHistory(fresh, progress);
            Assert.AreEqual(2, history.BaselineCount);
            Assert.AreEqual(Audibility.Muffled, history.Level(UttStudy));

            fresh.MoveTo(RoomA);
            fresh.SeekTo(1500); // 이번에는 a방에서 온전히 듣는다

            Assert.AreEqual(Audibility.Full, history.Level(UttA), "새로 더 잘 들은 쪽이 이긴다.");
            Assert.AreEqual(Audibility.Muffled, history.Level(UttStudy), "저장에만 있던 기록도 남는다.");
            Assert.AreEqual(Audibility.None, history.Level(UttALate));
        }

        [Test]
        public void SavingAgainAfterLoad_DoesNotLoseTheRestoredRecord()
        {
            ListeningSession source = NewSession(RoomB);
            source.SeekTo(1800);

            SaveGame first = Saved(source, Board(), new EvidenceLog());
            RestoredProgress progress = SaveGameCodec.Restore(first, _timeline, _audibility);

            ListeningSession fresh = NewSession(RoomA);
            SaveGameCodec.ApplyTo(progress, fresh, Board(), new EvidenceLog());
            var history = new HeardHistory(fresh, progress);

            fresh.MoveTo(RoomA);
            fresh.SeekTo(6200); // uA2를 온전히 듣는다

            SaveGame second = SaveGameCodec.Capture(CaseId, fresh, history, Board(), new EvidenceLog(), SavedAt + 1);
            RestoredProgress reloaded = SaveGameCodec.Restore(second, _timeline, _audibility);

            Assert.AreEqual(Audibility.Muffled, reloaded.HeardLevel(UttStudy), "첫 저장에만 있던 기록이 두 번째 저장에서 빠지면 안 된다.");
            Assert.AreEqual(Audibility.Full, reloaded.HeardLevel(UttALate));
            Assert.AreEqual(3, second.heard.Length);
        }

        [Test]
        public void HeardHistory_WithoutASaveIsJustTheSession()
        {
            ListeningSession session = NewSession(RoomA);
            var history = new HeardHistory(session);

            Assert.AreEqual(0, history.BaselineCount);
            Assert.AreEqual(Audibility.None, history.Level(UttA));

            session.SeekTo(1500);
            Assert.AreEqual(Audibility.Full, history.Level(UttA));
        }

        [Test]
        public void HeardHistory_ForgetBaselineKeepsWhatTheSessionItselfHeard()
        {
            ListeningSession source = NewSession(RoomB);
            source.SeekTo(1800);
            SaveGame save = Saved(source, Board(), new EvidenceLog());
            RestoredProgress progress = SaveGameCodec.Restore(save, _timeline, _audibility);

            ListeningSession fresh = NewSession(RoomA);
            fresh.SeekTo(1500);
            var history = new HeardHistory(fresh, progress);

            history.ForgetBaseline();

            Assert.AreEqual(0, history.BaselineCount);
            Assert.AreEqual(Audibility.None, history.Level(UttStudy));
            Assert.AreEqual(Audibility.Full, history.Level(UttA));
        }

        [Test]
        public void NowUnixMs_IsAfterTheProjectWasWritten()
        {
            // 2020-01-01T00:00:00Z. 시계가 엉뚱한 값이면 저장 목록의 정렬이 무너진다.
            Assert.Greater(SaveGame.NowUnixMs(), 1577836800000L);
        }
    }
}
