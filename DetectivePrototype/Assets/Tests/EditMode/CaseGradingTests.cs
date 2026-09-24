using Detective.Case;
using Detective.Data;
using Detective.Investigation;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>Phase 6 DoD: 전정답 / 부분정답 / 전오답 채점과 고발 입력 상태.</summary>
    public class CaseGradingTests
    {
        private static CaseAnswer Answer()
        {
            return new CaseAnswer { culprit = "npc_a", motive = "money", tick = 3, room = "study", method = "poison", evidence = "ev_glass" };
        }

        [Test]
        public void AllCorrect_IsSolved()
        {
            CaseGradeResult result = CaseGrader.Grade(Answer(), Answer());
            Assert.IsTrue(result.Solved);
            Assert.AreEqual(6, result.CorrectCount);
        }

        [Test]
        public void PartiallyCorrect_IsNotSolved_ButCountsFields()
        {
            CaseAnswer submission = Answer();
            submission.motive = "love";
            submission.tick = 2;

            CaseGradeResult result = CaseGrader.Grade(Answer(), submission);
            Assert.IsFalse(result.Solved);
            Assert.AreEqual(4, result.CorrectCount);
            Assert.IsTrue(result.IsCorrect(AccusationField.Culprit));
            Assert.IsFalse(result.IsCorrect(AccusationField.Motive));
            Assert.IsFalse(result.IsCorrect(AccusationField.Time));
        }

        [Test]
        public void AllWrong_ScoresZero()
        {
            var submission = new CaseAnswer { culprit = "npc_b", motive = "love", tick = 0, room = "a", method = "blunt", evidence = "ev_log" };
            CaseGradeResult result = CaseGrader.Grade(Answer(), submission);
            Assert.IsFalse(result.Solved);
            Assert.AreEqual(0, result.CorrectCount);
        }

        [Test]
        public void EmptySubmission_NeverMatchesEmptyAnswer()
        {
            CaseGradeResult result = CaseGrader.Grade(new CaseAnswer(), new CaseAnswer());
            Assert.AreEqual(0, result.CorrectCount);
            Assert.AreEqual(0, CaseGrader.Grade(null, Answer()).CorrectCount);
        }

        [Test]
        public void Form_OffersOnlyCollectedEvidence()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            var form = new AccusationForm(state);
            Assert.AreEqual(1, form.OptionCount(AccusationField.Evidence));
            Assert.AreEqual(string.Empty, form.Selected(AccusationField.Evidence).id, "단서가 없으면 빈 선택지 하나");

            state.CollectEvidence("ev_log");
            state.CollectEvidence("ev_glass");
            form = new AccusationForm(state);
            Assert.AreEqual(2, form.OptionCount(AccusationField.Evidence));
            Assert.AreEqual("ev_log", form.Selected(AccusationField.Evidence).id, "얻은 순서");
        }

        [Test]
        public void Form_ListsSuspectsNotVictim_AndAllTicksAndRooms()
        {
            var form = new AccusationForm(new InvestigationState(TestCaseFactory.Database()));
            Assert.AreEqual(2, form.OptionCount(AccusationField.Culprit));
            Assert.AreEqual(7, form.OptionCount(AccusationField.Time));
            Assert.AreEqual(3, form.OptionCount(AccusationField.Place));
            Assert.AreEqual(2, form.OptionCount(AccusationField.Motive));
        }

        [Test]
        public void Form_CycleWrapsAround()
        {
            var form = new AccusationForm(new InvestigationState(TestCaseFactory.Database()));
            form.Cycle(AccusationField.Time, -1);
            Assert.AreEqual(6, form.SelectedIndex(AccusationField.Time));
            form.Cycle(AccusationField.Time, +1);
            Assert.AreEqual(0, form.SelectedIndex(AccusationField.Time));
        }

        [Test]
        public void Form_CorrectSelections_SolveTheTestCase()
        {
            var state = new InvestigationState(TestCaseFactory.Database());
            state.CollectEvidence("ev_glass");
            var form = new AccusationForm(state);

            // 테스트 사건 정답: culprit / money / 18:30 / study / poison / ev_glass
            while (form.Selected(AccusationField.Culprit).id != "culprit") form.Cycle(AccusationField.Culprit, 1);
            form.Cycle(AccusationField.Time, 3);
            while (form.Selected(AccusationField.Place).id != "study") form.Cycle(AccusationField.Place, 1);

            Assert.AreEqual("1800000", form.Selected(AccusationField.Time).id, "선택지 id는 칸이 시작하는 ms");
            Assert.AreEqual("18:30", form.Selected(AccusationField.Time).label);

            CaseAnswer submission = form.ToSubmission();
            Assert.AreEqual(3, submission.tick);
            Assert.IsTrue(CaseGrader.Grade(state.Database.Case.answer, submission).Solved);
        }
    }
}
