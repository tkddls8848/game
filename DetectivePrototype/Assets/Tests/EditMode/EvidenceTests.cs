using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Investigation;
using Detective.NPC;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>단서 획득(중복 등록 금지)과 evidence 참조 무결성.</summary>
    public class EvidenceTests
    {
        [Test]
        public void EvidenceLog_CollectsOnce_InOrder()
        {
            var log = new EvidenceLog();
            Assert.IsTrue(log.Collect("b"));
            Assert.IsTrue(log.Collect("a"));
            Assert.IsFalse(log.Collect("b"), "재조사 시 중복 등록 안 됨");
            Assert.IsFalse(log.Collect(""));

            Assert.AreEqual(2, log.Count);
            Assert.AreEqual("b", log.InOrder[0]);
            Assert.IsTrue(log.Has("a"));
            Assert.IsFalse(log.Has("c"));
        }

        [Test]
        public void State_RejectsUnknownEvidence()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            Assert.IsFalse(state.CollectEvidence("ev_missing"));
            Assert.IsTrue(state.CollectEvidence("ev_glass"));
            Assert.IsFalse(state.CollectEvidence("ev_glass"));
        }

        [Test]
        public void Notebook_ShowsOnlyCollectedEvidence()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            var presenter = new NotebookPresenter(state);

            Assert.AreEqual(0, presenter.EvidenceEntries().Count);
            Assert.AreEqual(string.Empty, presenter.EvidenceDetail("ev_glass"), "얻기 전에는 내용을 보여 주지 않는다");

            state.CollectEvidence("ev_glass");
            List<NotebookEntry> entries = presenter.EvidenceEntries();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("와인잔", entries[0].Title);
            StringAssert.Contains("서재", presenter.EvidenceDetail("ev_glass"));
            StringAssert.Contains("18:30", presenter.EvidenceDetail("ev_glass"));
        }

        [Test]
        public void Notebook_PeopleListStartsWithVictim()
        {
            var presenter = new NotebookPresenter(new InvestigationState(TestCaseFactory.Database()));
            List<NotebookEntry> people = presenter.PeopleEntries();
            Assert.AreEqual(3, people.Count);
            Assert.AreEqual("victim", people[0].Id);
        }

        [Test]
        public void Validator_AcceptsTestCase()
        {
            List<string> errors = GameDataValidator.Validate(TestCaseFactory.Database());
            Assert.AreEqual(0, errors.Count, string.Join("\n", errors.ToArray()));
        }

        [Test]
        public void Validator_CatchesBrokenEvidenceReferences()
        {
            EvidenceTable table = TestCaseFactory.Evidence();
            table.evidence[0].foundRoom = "nowhere";
            table.evidence[1].revealNpc = "ghost";
            table.evidence[1].relatedTick = 9;

            var database = new CaseDatabase(RoomLayout.FromTable(TestCaseFactory.Rooms()), new NpcRoster(TestCaseFactory.Npcs()), table);
            List<string> errors = GameDataValidator.Validate(database);

            Assert.IsTrue(errors.Exists(e => e.Contains("nowhere")), string.Join("\n", errors.ToArray()));
            Assert.IsTrue(errors.Exists(e => e.Contains("ghost")));
            Assert.IsTrue(errors.Exists(e => e.Contains("relatedTick")));
        }

        [Test]
        public void Validator_CatchesPropOutsideRoom()
        {
            EvidenceTable table = TestCaseFactory.Evidence();
            table.evidence[0].offsetX = 4.9f; // 방 폭 10 → 허용 한계 5 - 0.8 = 4.2

            var database = new CaseDatabase(RoomLayout.FromTable(TestCaseFactory.Rooms()), new NpcRoster(TestCaseFactory.Npcs()), table);
            Assert.IsTrue(GameDataValidator.Validate(database).Exists(e => e.Contains("밖으로")));
        }

        [Test]
        public void Validator_CatchesBadSchedule()
        {
            NpcDefinition[] npcs = TestCaseFactory.Npcs();
            npcs[1].schedule = new[] { "a", "b", "", "study" };

            var database = new CaseDatabase(RoomLayout.FromTable(TestCaseFactory.Rooms()), new NpcRoster(npcs), TestCaseFactory.Evidence());
            List<string> errors = GameDataValidator.Validate(database);
            Assert.IsTrue(errors.Exists(e => e.Contains("schedule 길이")), string.Join("\n", errors.ToArray()));
            Assert.IsTrue(errors.Exists(e => e.Contains("비어 있다")));
        }
    }
}
