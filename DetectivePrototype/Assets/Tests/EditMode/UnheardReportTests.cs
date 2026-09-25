using System.Collections.Generic;
using System.Reflection;
using Detective.Core;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// Phase U-6 — 못 들은 것 표시(DEVELOPMENT_PLAN_UNHEARD.md §7 "놓친 발화를 영영 모른다").
    ///
    /// 두 가지를 동시에 지켜야 한다.
    ///   1. <b>어디에 남았는지는 말한다</b> — 없으면 답답함이 재미가 아니라 벽이 된다
    ///   2. <b>무슨 말이었는지는 말하지 않는다</b> — 흘리면 되돌려 들을 이유가 사라진다
    /// 두 번째는 <see cref="ReportNeverCarriesTheWordsThemselves"/>가 자료형을 훑어서 못을 박는다.
    ///
    /// 방은 TestCaseFactory의 a(0~10) · b(10~20) · study(20~30) 한 줄 배치다.
    /// a와 study는 벽을 맞대지 않고, 가운데 b는 양쪽과 맞닿는다.
    /// </summary>
    public class UnheardReportTests
    {
        private const string RoomA = "a";
        private const string RoomB = "b";
        private const string RoomStudy = "study";

        private const string UttA = "uA";
        private const string UttA2 = "uA2";
        private const string UttStudy = "uS";

        private const string TextA = "A에서 한 말";
        private const string TextA2 = "A에서 나중에";
        private const string TextStudy = "서재에서 한 말";

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

        /// <summary>a에서 둘(1초·6초), 서재에서 하나(1.5초). a의 둘째가 첫째보다 뒤에 적혀 있지 않다 — 정렬을 보려고 일부러 뒤섞는다.</summary>
        private static ScriptDefinition BuildScript()
        {
            return new ScriptDefinition
            {
                caseId = "unheard_test",
                durationMs = 10000,
                speakers = new[]
                {
                    new SpeakerDefinition { voiceId = "v1", npcId = "culprit" },
                    new SpeakerDefinition { voiceId = "v2", npcId = "witness" }
                },
                utterances = new[]
                {
                    new Utterance { id = UttA2,    startMs = 6000, durationMs = 1000, voiceId = "v1", room = RoomA,     text = TextA2,    clip = "" },
                    new Utterance { id = UttStudy, startMs = 1500, durationMs = 2000, voiceId = "v2", room = RoomStudy, text = TextStudy, clip = "" },
                    new Utterance { id = UttA,     startMs = 1000, durationMs = 2000, voiceId = "v1", room = RoomA,     text = TextA,     clip = "" }
                }
            }.Normalized();
        }

        /// <summary>발화 id → 등급을 코드로 지어낸 기록. 세션을 흘리지 않고 원하는 상태를 바로 만든다.</summary>
        private static HeardLookup Heard(Dictionary<string, Audibility> levels)
        {
            return delegate(string utteranceId)
            {
                Audibility level;
                return levels.TryGetValue(utteranceId, out level) ? level : Audibility.None;
            };
        }

        private UnheardReport Report(Dictionary<string, Audibility> levels)
        {
            return UnheardReport.Build(_timeline, _audibility, Heard(levels));
        }

        private static Dictionary<string, Audibility> Levels()
        {
            return new Dictionary<string, Audibility>();
        }

        // ---------------------------------------------------------------- 진행도

        [Test]
        public void FreshRun_HasHeardNothing()
        {
            UnheardReport report = Report(Levels());

            Assert.AreEqual(3, report.TotalCount);
            Assert.AreEqual(0, report.FullyHeardCount);
            Assert.AreEqual(3, report.UnheardCount);
            Assert.AreEqual(0, report.ProgressPermille);
            Assert.IsFalse(report.IsComplete);
        }

        [Test]
        public void ProgressCountsOnlyWhatWasHeardInFull()
        {
            var levels = Levels();
            levels[UttA] = Audibility.Full;
            levels[UttStudy] = Audibility.Muffled;

            UnheardReport report = Report(levels);

            Assert.AreEqual(1, report.FullyHeardCount);
            Assert.AreEqual(2, report.UnheardCount, "웅얼거림은 들은 것이 아니다.");
            Assert.AreEqual(333, report.ProgressPermille);
            Assert.AreEqual(1, report.MissedCount);
            Assert.AreEqual(1, report.UntouchedCount);
        }

        [Test]
        public void HearingEverythingEmptiesTheReport()
        {
            var levels = Levels();
            levels[UttA] = Audibility.Full;
            levels[UttA2] = Audibility.Full;
            levels[UttStudy] = Audibility.Full;

            UnheardReport report = Report(levels);

            Assert.IsTrue(report.IsComplete);
            Assert.AreEqual(1000, report.ProgressPermille);
            CollectionAssert.IsEmpty(report.Rooms);
            CollectionAssert.IsEmpty(report.RoomsToVisit);
            Assert.AreEqual(-1, report.EarliestUnheardMs);
        }

        [Test]
        public void EmptyScriptIsCountedAsFinishedRatherThanDividingByZero()
        {
            var empty = new ScriptTimeline(new ScriptDefinition { durationMs = 1000 }.Normalized());
            UnheardReport report = UnheardReport.Build(empty, _audibility, Heard(Levels()));

            Assert.AreEqual(0, report.TotalCount);
            Assert.AreEqual(1000, report.ProgressPermille);
            Assert.IsTrue(report.IsComplete);
        }

        // ---------------------------------------------------------------- 방별로

        [Test]
        public void UnheardUtterancesAreGroupedByTheRoomYouMustStandIn()
        {
            UnheardReport report = Report(Levels());

            Assert.AreEqual(2, report.Rooms.Count);
            Assert.AreEqual(RoomA, report.Rooms[0].RoomId, "방은 id 순으로 고정된다.");
            Assert.AreEqual(RoomStudy, report.Rooms[1].RoomId);

            RoomUnheard a = report.ForRoom(RoomA);
            Assert.AreEqual(2, a.TotalCount);
            Assert.AreEqual(UttA, a.Utterances[0].UtteranceId, "발화는 시각 순이다(대본에 적힌 순서가 아니다).");
            Assert.AreEqual(UttA2, a.Utterances[1].UtteranceId);
            Assert.AreEqual(1000, a.EarliestStartMs);

            Assert.IsNull(report.ForRoom(RoomB), "b에서는 아무도 말하지 않는다.");
            Assert.IsFalse(report.HasUnheardIn(RoomB));
        }

        [Test]
        public void RoomDropsOutOfTheReportOnceItIsFullyHeard()
        {
            var levels = Levels();
            levels[UttA] = Audibility.Full;
            levels[UttA2] = Audibility.Full;

            UnheardReport report = Report(levels);

            Assert.IsFalse(report.HasUnheardIn(RoomA));
            Assert.AreEqual(1, report.Rooms.Count);
            Assert.AreEqual(RoomStudy, report.Rooms[0].RoomId);
        }

        [Test]
        public void EntryRemembersWhereAndWhenButNothingElse()
        {
            var levels = Levels();
            levels[UttStudy] = Audibility.Muffled;

            UnheardUtterance entry = Report(levels).ForRoom(RoomStudy).Utterances[0];

            Assert.AreEqual(UttStudy, entry.UtteranceId);
            Assert.AreEqual(RoomStudy, entry.Room);
            Assert.AreEqual(1500, entry.StartMs);
            Assert.AreEqual(3500, entry.EndMs);
            Assert.AreEqual(Audibility.Muffled, entry.BestSoFar);
            Assert.IsTrue(entry.IsMissed);
            Assert.IsFalse(entry.IsUntouched);
        }

        // ---------------------------------------------------------------- 가야 할 방

        /// <summary>
        /// 스쳐서 존재를 아는 것만 "가야 할 방"이 된다.
        /// 한 번도 닿지 않은 발화까지 방 이름으로 띄우면, 아직 있는 줄도 모르는 대화를 알려 주는 셈이다.
        /// </summary>
        [Test]
        public void OnlyRoomsYouActuallyBrushedAgainstBecomeHints()
        {
            var levels = Levels();
            levels[UttStudy] = Audibility.Muffled;

            UnheardReport report = Report(levels);

            CollectionAssert.AreEqual(new[] { RoomStudy }, report.RoomsToVisit);
            Assert.AreEqual(1, report.MissedCountIn(RoomStudy));
            Assert.AreEqual(0, report.MissedCountIn(RoomA), "a의 둘은 아직 스치지도 않았다.");
            Assert.AreEqual(2, report.ForRoom(RoomA).UntouchedCount);
        }

        [Test]
        public void NeighboursToVisitPointsThroughTheWallNotAcrossTheMap()
        {
            var levels = Levels();
            levels[UttA] = Audibility.Muffled;
            levels[UttStudy] = Audibility.Muffled;

            UnheardReport report = Report(levels);

            CollectionAssert.AreEqual(new[] { RoomA, RoomStudy }, report.NeighboursToVisit(RoomB),
                "가운데 b만 양쪽과 벽을 맞댄다.");
            CollectionAssert.IsEmpty(report.NeighboursToVisit(RoomA),
                "a에서 서재는 벽을 맞대지 않는다 — 한 걸음에 닿지 않는 방은 가리키지 않는다.");
            CollectionAssert.IsEmpty(report.NeighboursToVisit(RoomStudy), "서재에서 a도 마찬가지다.");
        }

        [Test]
        public void StandingInARoomDoesNotPointAtItself()
        {
            var levels = Levels();
            levels[UttA] = Audibility.Muffled;
            levels[UttStudy] = Audibility.Muffled;

            UnheardReport report = Report(levels);

            CollectionAssert.Contains(report.RoomsToVisit, RoomA, "a에는 아직 못 들은 것이 남아 있다.");
            CollectionAssert.DoesNotContain(report.NeighboursToVisit(RoomA), RoomA, "여기 남은 것은 여기서 이미 들린다.");
            CollectionAssert.DoesNotContain(report.NeighboursToVisit(RoomB), RoomB);
        }

        [Test]
        public void EarliestUnheardMsIsWhereRewindingShouldLand()
        {
            var levels = Levels();
            levels[UttA] = Audibility.Full;

            UnheardReport report = Report(levels);

            Assert.AreEqual(1500, report.EarliestUnheardMs, "서재의 1.5초가 a의 6초보다 이르다.");
        }

        // ---------------------------------------------------------------- 닿을 수 없는 발화

        /// <summary>
        /// 평면도에 없는 방에서 울리는 발화는 갈 곳이 없다. 분모에 넣으면 100%가 영영 나오지 않는다.
        /// </summary>
        [Test]
        public void UtterancesInRoomsThatDoNotExistAreNotCounted()
        {
            var script = new ScriptDefinition
            {
                caseId = "ghost",
                durationMs = 5000,
                utterances = new[]
                {
                    new Utterance { id = "u_real",  startMs = 0, durationMs = 500, voiceId = "v1", room = RoomA,      text = "여기", clip = "" },
                    new Utterance { id = "u_ghost", startMs = 0, durationMs = 500, voiceId = "v1", room = "nowhere",  text = "저기", clip = "" }
                }
            }.Normalized();

            var levels = Levels();
            levels["u_real"] = Audibility.Full;

            UnheardReport report = UnheardReport.Build(new ScriptTimeline(script), _audibility, Heard(levels));

            Assert.AreEqual(1, report.TotalCount);
            Assert.IsTrue(report.IsComplete);
            Assert.IsNull(report.ForRoom("nowhere"));
        }

        // ---------------------------------------------------------------- 세션에서 바로

        [Test]
        public void BuildingFromALiveSessionSeesWhatThatSessionHeard()
        {
            var session = new ListeningSession(_timeline, _audibility);
            session.MoveTo(RoomA);
            session.SeekTo(1500);

            UnheardReport report = UnheardReport.Build(session, _audibility);

            Assert.AreEqual(1, report.FullyHeardCount);
            Assert.IsFalse(report.HasUnheardIn(RoomB));
            Assert.AreEqual(1, report.ForRoom(RoomA).TotalCount, "a에는 6초짜리가 남는다.");
        }

        [Test]
        public void BuildingFromNothingDoesNotThrow()
        {
            UnheardReport report = UnheardReport.Build((ListeningSession)null, _audibility);

            Assert.AreEqual(0, report.TotalCount);
            CollectionAssert.IsEmpty(report.Rooms);
            CollectionAssert.IsEmpty(report.NeighboursToVisit(RoomA));
        }

        /// <summary>평면도를 못 받았으면 대본에 적힌 방을 그대로 믿는다 — 검사기가 평면도 없이도 돌 수 있어야 한다.</summary>
        [Test]
        public void WithoutALayoutTheScriptRoomsAreTakenAtFaceValue()
        {
            UnheardReport report = UnheardReport.Build(_timeline, null, Heard(Levels()));

            Assert.AreEqual(3, report.TotalCount);
            Assert.AreEqual(2, report.Rooms.Count);
            CollectionAssert.IsEmpty(report.NeighboursToVisit(RoomB), "가청 판정이 없으면 벽 너머를 가리킬 수 없다.");
        }

        // ---------------------------------------------------------------- 새지 않는가

        /// <summary>
        /// 보고서가 들고 다니는 문자열 어디에도 대사가 묻어 있으면 안 된다.
        /// 필드를 손으로 하나씩 세지 않고 자료형을 훑는 것은, <b>나중에 필드를 더해도</b> 이 못이 버티게 하려는 것이다.
        /// </summary>
        [Test]
        public void ReportNeverCarriesTheWordsThemselves()
        {
            var levels = Levels();
            levels[UttA] = Audibility.Muffled;

            UnheardReport report = Report(levels);
            var forbidden = new[] { TextA, TextA2, TextStudy, "v1", "v2" };

            var seen = new List<string>();
            for (int i = 0; i < report.Rooms.Count; i++)
            {
                RoomUnheard room = report.Rooms[i];
                seen.Add(room.RoomId);

                for (int u = 0; u < room.Utterances.Count; u++) CollectStrings(room.Utterances[u], seen);
            }
            for (int i = 0; i < report.RoomsToVisit.Count; i++) seen.Add(report.RoomsToVisit[i]);

            Assert.Greater(seen.Count, 0, "볼 것이 없으면 이 검사는 아무것도 지키지 못한다.");
            for (int i = 0; i < seen.Count; i++)
            {
                string value = seen[i];
                if (value == null) continue;
                for (int f = 0; f < forbidden.Length; f++)
                    Assert.IsFalse(value.Contains(forbidden[f]), "보고서가 '" + forbidden[f] + "'을(를) 흘렸다: " + value);
            }
        }

        /// <summary>public 필드·프로퍼티의 문자열 값을 전부 걷는다.</summary>
        private static void CollectStrings(object target, List<string> into)
        {
            if (target == null) return;

            FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType != typeof(string)) continue;
                into.Add((string)fields[i].GetValue(target));
            }

            PropertyInfo[] properties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
            {
                if (properties[i].PropertyType != typeof(string)) continue;
                if (properties[i].GetIndexParameters().Length > 0) continue;
                into.Add((string)properties[i].GetValue(target, null));
            }
        }
    }
}
