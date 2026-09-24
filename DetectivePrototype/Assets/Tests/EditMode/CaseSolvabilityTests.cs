using System.Collections.Generic;
using Detective.Case;
using Detective.Data;
using Detective.Investigation;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 추리 가능성 검증기 자체의 테스트. 테스트 사건(TestCaseFactory)은 모순이 1건뿐이라 기준을 1로 낮춰 검사한다.
    /// 실제 사건(case_01)은 GameDataTests에서 기본 기준(2건)으로 검사한다.
    /// </summary>
    public class CaseSolvabilityTests
    {
        [Test]
        public void TestCase_IsSolvable()
        {
            List<string> problems = CaseSolvabilityChecker.Check(TestCaseFactory.Database(), 1);
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void TestCase_FailsDefaultContradictionThreshold()
        {
            List<string> problems = CaseSolvabilityChecker.Check(TestCaseFactory.Database());
            Assert.IsTrue(problems.Exists(p => p.Contains("모순")), string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void RemovingAlibiEvidence_MakesWitnessASuspect()
        {
            EvidenceTable evidence = TestCaseFactory.Evidence();
            evidence.evidence[1].revealNpc = "";
            evidence.evidence[1].revealTick = -1;
            evidence.evidence[1].revealRoom = "";

            List<string> problems = CaseSolvabilityChecker.Check(
                TestCaseFactory.Database(TestCaseFactory.Npcs(), evidence, TestCaseFactory.Dialogues()), 1);
            Assert.IsTrue(problems.Exists(p => p.Contains("witness") && p.Contains("알리바이")), string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void WrongAnswerRoom_IsReported()
        {
            CaseDefinition definition = TestCaseFactory.Case();
            definition.answer.room = "a";

            List<string> problems = CaseSolvabilityChecker.Check(
                TestCaseFactory.Database(TestCaseFactory.Npcs(), TestCaseFactory.Evidence(), TestCaseFactory.Dialogues(), definition), 1);
            Assert.IsTrue(problems.Exists(p => p.Contains("스케줄상")), string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void EvidenceThatLies_IsReported()
        {
            EvidenceTable evidence = TestCaseFactory.Evidence();
            evidence.evidence[1].revealRoom = "b"; // witness는 18:30에 실제로 a에 있었다

            List<string> problems = CaseSolvabilityChecker.Check(
                TestCaseFactory.Database(TestCaseFactory.Npcs(), evidence, TestCaseFactory.Dialogues()), 1);
            Assert.IsTrue(problems.Exists(p => p.Contains("실제 스케줄과 다르다")), string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void DecisiveEvidence_MustPointAtCulprit()
        {
            CaseDefinition definition = TestCaseFactory.Case();
            definition.answer.evidence = "ev_log";

            List<string> problems = CaseSolvabilityChecker.Check(
                TestCaseFactory.Database(TestCaseFactory.Npcs(), TestCaseFactory.Evidence(), TestCaseFactory.Dialogues(), definition), 1);
            Assert.IsTrue(problems.Exists(p => p.Contains("결정적 증거")));
        }

        [Test]
        public void FullKnowledge_HearsConditionalLinesToo()
        {
            InvestigationState state = CaseSolvabilityChecker.FullKnowledge(TestCaseFactory.Database());
            Assert.IsTrue(state.HasHeard("culprit|on_glass"));
            Assert.IsTrue(state.HasHeard("culprit|confess"));
            Assert.AreEqual(2, state.Evidence.Count);
        }

        [Test]
        public void CountContradictions_CountsTicksNotRecords()
        {
            TimelineBoard board = TimelineBoard.Build(CaseSolvabilityChecker.FullKnowledge(TestCaseFactory.Database()));
            Assert.AreEqual(1, CaseSolvabilityChecker.CountContradictions(board, "culprit"), "18:10 한 곳");
            Assert.AreEqual(0, CaseSolvabilityChecker.CountContradictions(board, "witness"));
        }
    }
}
