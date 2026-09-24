using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Dialogue;
using Detective.Investigation;
using Detective.NPC;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>타임라인 복원: 들은 것과 얻은 것만으로 만들고, 어긋나는 기록은 그대로 둔다.</summary>
    public class TimelineBoardTests
    {
        private static void HearAll(InvestigationState state, string npcId)
        {
            List<ConversationLine> lines = ConversationBuilder.Build(state, npcId);
            for (int i = 0; i < lines.Count; i++) state.MarkHeard(npcId, lines[i].Key);
        }

        private static NpcDefinition Get(CaseDatabase database, string id)
        {
            NpcDefinition npc;
            database.Npcs.TryGet(id, out npc);
            return npc;
        }

        [Test]
        public void EmptyState_KnowsNothing()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            TimelineBoard board = TimelineBoard.Build(state);

            Assert.AreEqual(0, board.Records.Count);
            Assert.IsFalse(board.PlacementAt(Get(state.Database, "culprit"), GameTime.TickToMs(3)).Known, "실제 스케줄을 흘리면 안 된다");
        }

        [Test]
        public void Alibi_RecordsClaimsForEveryTick()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            state.MarkHeard("culprit", "culprit|alibi");
            TimelineBoard board = TimelineBoard.Build(state);

            for (int t = 0; t < GameTime.TickCount; t++)
            {
                List<TimelineRecord> records = board.RecordsInTick("culprit", t);
                Assert.AreEqual(1, records.Count, "t=" + t);
                Assert.AreEqual(RecordKind.Testimony, records[0].Kind);
            }
            Assert.AreEqual("a", board.RecordsInTick("culprit", 3)[0].RoomId, "18:30엔 A방에 있었다고 주장한다(거짓)");
        }

        [Test]
        public void Sighting_ContradictsTestimony_AndBothAreKept()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            state.MarkHeard("culprit", "culprit|alibi");
            HearAll(state, "witness");
            TimelineBoard board = TimelineBoard.Build(state);

            List<TimelineRecord> at1810 = board.RecordsInTick("culprit", 1);
            Assert.IsTrue(at1810.Exists(r => r.Kind == RecordKind.Testimony && r.RoomId == "a"));
            Assert.IsTrue(at1810.Exists(r => r.Kind == RecordKind.Sighting && r.RoomId == "b" && r.SourceId == "witness"));

            NpcPlacement placement = board.PlacementAt(Get(state.Database, "culprit"), GameTime.TickToMs(1));
            Assert.AreEqual("b", placement.RoomId, "관찰 화면은 목격을 증언보다 믿는다");
            Assert.AreEqual("마르코 목격", placement.Caption);
        }

        [Test]
        public void PlacementAt_AnyMsInsideTheSlot_UsesThatSlotsRecords()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            state.MarkHeard("culprit", "culprit|alibi");
            HearAll(state, "witness");
            TimelineBoard board = TimelineBoard.Build(state);

            NpcDefinition culprit = Get(state.Database, "culprit");
            Assert.AreEqual("b", board.PlacementAt(culprit, GameTime.TickToMs(1) + 7 * GameTime.MsPerMinute).RoomId, "18:17도 18:10 칸의 목격");
            Assert.AreEqual(GameTime.TickToMs(1), board.RecordsInTick("culprit", 1)[0].Ms, "틱 데이터의 기록은 칸이 시작하는 ms로 들어온다");
            Assert.IsFalse(board.PlacementAt(culprit, GameTime.EndMs + 1).Known, "19:00 이후는 모른다");
        }

        [Test]
        public void Evidence_RevealsWhereabouts_AndBeatsEverything()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            HearAll(state, "witness");
            state.CollectEvidence("ev_log");
            TimelineBoard board = TimelineBoard.Build(state);

            List<TimelineRecord> records = board.RecordsInTick("witness", 3);
            Assert.IsTrue(records.Exists(r => r.Kind == RecordKind.Evidence && r.SourceId == "ev_log"));
            Assert.AreEqual("물증: 기록부", board.PlacementAt(Get(state.Database, "witness"), GameTime.TickToMs(3)).Caption);
        }

        [Test]
        public void ConfessionClaim_AddsSecondTestimony()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            state.MarkHeard("culprit", "culprit|alibi");
            state.CollectEvidence("ev_log");
            HearAll(state, "culprit");
            TimelineBoard board = TimelineBoard.Build(state);

            List<TimelineRecord> records = board.RecordsInTick("culprit", 3);
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual("study", board.PlacementAt(Get(state.Database, "culprit"), GameTime.TickToMs(3)).RoomId, "나중에 한 말이 우선");
        }

        [Test]
        public void UnheardLines_AddNothing()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            state.CollectEvidence("ev_glass"); // 행적을 드러내지 않는 단서
            Assert.AreEqual(0, TimelineBoard.Build(state).Records.Count);
        }

        [Test]
        public void NotebookCell_ListsEveryRecordWithSource()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            state.MarkHeard("culprit", "culprit|alibi");
            HearAll(state, "witness");
            var presenter = new NotebookPresenter(state);

            string cell = presenter.TimelineCell(TimelineBoard.Build(state), "culprit", 1);
            StringAssert.Contains("A방", cell);
            StringAssert.Contains("본인", cell);
            StringAssert.Contains("B방", cell);
            StringAssert.Contains("마르코", cell);
            StringAssert.Contains("?", presenter.TimelineCell(TimelineBoard.Build(state), "victim", 0));
        }

        [Test]
        public void ShortName_TakesFirstWord()
        {
            Assert.AreEqual("마르코", TimelineBoard.ShortName("마르코 벨리니"));
            Assert.AreEqual("클라라", TimelineBoard.ShortName("클라라"));
        }
    }
}
