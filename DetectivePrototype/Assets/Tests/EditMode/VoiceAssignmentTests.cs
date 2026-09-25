using System.Collections.Generic;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// Phase U-3 — 목소리 → 이름 배정 보드(DEVELOPMENT_PLAN_UNHEARD.md).
    /// 완료 조건 중 "배정한 이름이 기록되고 채점에 반영된다"의 앞쪽 절반을 본다.
    ///
    /// 이 보드가 지켜야 하는 것은 둘이다.
    ///   하나, 정답(<c>speakers</c>)을 들고 있지 않는다 — 보드가 답을 알면 UI로 새어 나갈 길이 생긴다.
    ///   둘, 한 사람이 두 목소리에 붙는 상태가 **만들어지지 않는다**. 나중에 잡는 것이 아니라 애초에 생기지 않는다.
    ///
    /// 인물 id는 TestCaseFactory와 같은 culprit·witness·victim을 쓴다.
    /// </summary>
    public class VoiceAssignmentTests
    {
        private const string V1 = "v1";
        private const string V2 = "v2";
        private const string V3 = "v3";

        private const string Culprit = "culprit";
        private const string Witness = "witness";
        private const string Victim = "victim";

        private VoiceAssignment _board;

        [SetUp]
        public void SetUp()
        {
            _board = VoiceAssignment.ForScript(Script());
        }

        /// <summary>목소리 셋. 발화는 보드와 상관없으므로 비워 둔다.</summary>
        private static ScriptDefinition Script()
        {
            return new ScriptDefinition
            {
                caseId = "voice_assignment_test",
                durationMs = 10000,
                speakers = new[]
                {
                    new SpeakerDefinition { voiceId = V1, npcId = Culprit },
                    new SpeakerDefinition { voiceId = V2, npcId = Witness },
                    new SpeakerDefinition { voiceId = V3, npcId = Victim }
                }
            }.Normalized();
        }

        // ── 보드 만들기 ──────────────────────────────────────────

        [Test]
        public void ForScript_TakesVoicesInOrder_AndStartsEmpty()
        {
            CollectionAssert.AreEqual(new[] { V1, V2, V3 }, _board.VoiceIds);
            Assert.AreEqual(0, _board.AssignedCount, "보드는 답을 미리 채워 두지 않는다");
            Assert.IsFalse(_board.IsComplete);

            for (int i = 0; i < _board.VoiceIds.Count; i++)
                Assert.AreEqual(string.Empty, _board.AssignedNpc(_board.VoiceIds[i]));
        }

        [Test]
        public void EmptyBoard_IsNotComplete()
        {
            var empty = new VoiceAssignment(new string[0]);
            Assert.AreEqual(0, empty.VoiceCount);
            Assert.IsFalse(empty.IsComplete, "채울 칸이 없는 보드를 완성으로 치면 채점이 공짜가 된다");
        }

        [Test]
        public void DuplicateAndEmptyVoiceIds_AreDroppedOnConstruction()
        {
            var board = new VoiceAssignment(new[] { V1, "", V1, V2, null });
            CollectionAssert.AreEqual(new[] { V1, V2 }, board.VoiceIds);
        }

        // ── 배정 ─────────────────────────────────────────────────

        [Test]
        public void Assign_RemembersBothDirections()
        {
            Assert.AreEqual(VoiceAssignResult.Assigned, _board.Assign(V1, Culprit));

            Assert.AreEqual(Culprit, _board.AssignedNpc(V1));
            Assert.AreEqual(V1, _board.VoiceOf(Culprit), "이름에서 목소리도 찾아갈 수 있어야 한다");
            Assert.IsTrue(_board.IsAssigned(V1));
            Assert.AreEqual(1, _board.AssignedCount);

            Assert.AreEqual(VoiceAssignResult.Unchanged, _board.Assign(V1, Culprit), "같은 이름을 또 붙이면 달라지는 것이 없다");
        }

        [Test]
        public void SameName_OnAnotherVoice_FallsOffTheFirst()
        {
            _board.Assign(V1, Culprit);
            Assert.AreEqual(VoiceAssignResult.Displaced, _board.Assign(V2, Culprit));

            Assert.AreEqual(string.Empty, _board.AssignedNpc(V1), "한 사람이 두 목소리일 수는 없다");
            Assert.AreEqual(Culprit, _board.AssignedNpc(V2));
            Assert.AreEqual(V2, _board.VoiceOf(Culprit));
            Assert.AreEqual(1, _board.AssignedCount, "떼어 온 것이지 늘어난 것이 아니다");
        }

        [Test]
        public void WouldDisplace_NamesTheVoiceThatWillEmpty()
        {
            _board.Assign(V1, Culprit);

            Assert.AreEqual(V1, _board.WouldDisplace(V2, Culprit), "붙이기 전에 어느 칸이 빌지 알려 줘야 한다");
            Assert.AreEqual(string.Empty, _board.WouldDisplace(V1, Culprit), "제자리에 다시 붙이는 것은 뺏는 것이 아니다");
            Assert.AreEqual(string.Empty, _board.WouldDisplace(V2, Witness), "아무도 안 쥔 이름은 뺏을 일이 없다");
        }

        [Test]
        public void ReplacingAName_ReleasesTheOldOne()
        {
            _board.Assign(V1, Culprit);
            _board.Assign(V1, Witness);

            Assert.AreEqual(Witness, _board.AssignedNpc(V1));
            Assert.AreEqual(string.Empty, _board.VoiceOf(Culprit), "밀려난 이름은 다시 아무 데도 붙어 있지 않아야 한다");
            Assert.AreEqual(1, _board.AssignedCount);
        }

        [Test]
        public void Clear_FreesTheName()
        {
            _board.Assign(V1, Culprit);

            Assert.AreEqual(VoiceAssignResult.Cleared, _board.Clear(V1));
            Assert.AreEqual(string.Empty, _board.AssignedNpc(V1));
            Assert.AreEqual(string.Empty, _board.VoiceOf(Culprit));
            Assert.AreEqual(VoiceAssignResult.Unchanged, _board.Clear(V1), "빈 칸을 또 비워도 달라질 것이 없다");
        }

        [Test]
        public void AssigningEmptyName_IsTheSameAsClearing()
        {
            _board.Assign(V1, Culprit);
            Assert.AreEqual(VoiceAssignResult.Cleared, _board.Assign(V1, string.Empty));
            Assert.AreEqual(string.Empty, _board.AssignedNpc(V1));
        }

        [Test]
        public void UnknownVoice_ChangesNothing()
        {
            Assert.AreEqual(VoiceAssignResult.UnknownVoice, _board.Assign("v9", Culprit));
            Assert.AreEqual(VoiceAssignResult.UnknownVoice, _board.Clear("v9"));
            Assert.AreEqual(VoiceAssignResult.UnknownVoice, _board.Cycle("v9", new[] { Culprit }, 1));

            Assert.AreEqual(0, _board.AssignedCount);
            Assert.AreEqual(string.Empty, _board.VoiceOf(Culprit));
        }

        [Test]
        public void IsComplete_OnlyWhenEveryVoiceHasAName()
        {
            _board.Assign(V1, Culprit);
            _board.Assign(V2, Witness);
            Assert.IsFalse(_board.IsComplete);

            _board.Assign(V3, Victim);
            Assert.IsTrue(_board.IsComplete);

            // 마지막 이름을 앞 칸으로 옮기면 뒤 칸이 비어 다시 미완성이 된다.
            _board.Assign(V1, Victim);
            Assert.IsFalse(_board.IsComplete);
            Assert.AreEqual(2, _board.AssignedCount);
        }

        [Test]
        public void ClearAll_EmptiesTheBoard()
        {
            _board.Assign(V1, Culprit);
            _board.Assign(V2, Witness);
            _board.ClearAll();

            Assert.AreEqual(0, _board.AssignedCount);
            Assert.AreEqual(string.Empty, _board.VoiceOf(Witness));
        }

        // ── 고리 돌리기(←→) ─────────────────────────────────────

        [Test]
        public void Cycle_WalksThroughNamesAndBackToNone()
        {
            var candidates = new List<string> { Culprit, Witness, Victim };

            Assert.AreEqual(VoiceAssignResult.Assigned, _board.Cycle(V1, candidates, 1));
            Assert.AreEqual(Culprit, _board.AssignedNpc(V1));

            _board.Cycle(V1, candidates, 1);
            Assert.AreEqual(Witness, _board.AssignedNpc(V1));

            _board.Cycle(V1, candidates, 1);
            Assert.AreEqual(Victim, _board.AssignedNpc(V1));

            Assert.AreEqual(VoiceAssignResult.Cleared, _board.Cycle(V1, candidates, 1), "고리 끝은 다시 빈 칸이다");
            Assert.AreEqual(string.Empty, _board.AssignedNpc(V1));
        }

        [Test]
        public void Cycle_Backwards_LandsOnTheLastName()
        {
            var candidates = new List<string> { Culprit, Witness, Victim };

            _board.Cycle(V1, candidates, -1);
            Assert.AreEqual(Victim, _board.AssignedNpc(V1));
        }

        [Test]
        public void Cycle_OntoATakenName_StealsIt()
        {
            var candidates = new List<string> { Culprit };
            _board.Assign(V2, Culprit);

            Assert.AreEqual(VoiceAssignResult.Displaced, _board.Cycle(V1, candidates, 1));
            Assert.AreEqual(Culprit, _board.AssignedNpc(V1));
            Assert.AreEqual(string.Empty, _board.AssignedNpc(V2));
        }

        // ── 저장·복원 ───────────────────────────────────────────

        [Test]
        public void ToPairs_AndRestore_RoundTrip()
        {
            _board.Assign(V1, Culprit);
            _board.Assign(V3, Victim);

            SpeakerDefinition[] saved = _board.ToPairs();
            Assert.AreEqual(2, saved.Length, "빈 칸은 저장하지 않는다");
            Assert.AreEqual(V1, saved[0].voiceId, "보드 순서를 지킨다");
            Assert.AreEqual(V3, saved[1].voiceId);

            VoiceAssignment restored = VoiceAssignment.ForScript(Script());
            List<string> problems = restored.Restore(saved);

            CollectionAssert.IsEmpty(problems, string.Join(" / ", problems.ToArray()));
            Assert.AreEqual(Culprit, restored.AssignedNpc(V1));
            Assert.AreEqual(string.Empty, restored.AssignedNpc(V2));
            Assert.AreEqual(Victim, restored.AssignedNpc(V3));
        }

        [Test]
        public void Restore_ReportsUnknownVoiceAndDuplicateName()
        {
            List<string> problems = _board.Restore(new[]
            {
                new SpeakerDefinition { voiceId = V1, npcId = Culprit },
                new SpeakerDefinition { voiceId = V2, npcId = Culprit },
                new SpeakerDefinition { voiceId = "v9", npcId = Witness }
            });

            Assert.AreEqual(2, problems.Count, string.Join(" / ", problems.ToArray()));
            StringAssert.Contains(V1, problems[0]);
            StringAssert.Contains("v9", problems[1]);

            // 문제를 알리되 보드는 규칙을 지킨 상태로 남는다.
            Assert.AreEqual(string.Empty, _board.AssignedNpc(V1));
            Assert.AreEqual(Culprit, _board.AssignedNpc(V2));
            Assert.AreEqual(1, _board.AssignedCount);
        }

        [Test]
        public void Restore_ClearsWhatWasThereBefore()
        {
            _board.Assign(V1, Culprit);
            _board.Restore(new[] { new SpeakerDefinition { voiceId = V2, npcId = Witness } });

            Assert.AreEqual(string.Empty, _board.AssignedNpc(V1), "복원은 덮어쓰기지 합치기가 아니다");
            Assert.AreEqual(Witness, _board.AssignedNpc(V2));
        }

        // ── 채점 ─────────────────────────────────────────────────

        [Test]
        public void Grade_PerfectBoard()
        {
            _board.Assign(V1, Culprit);
            _board.Assign(V2, Witness);
            _board.Assign(V3, Victim);

            VoiceAssignmentGrade grade = _board.Grade(Script());

            Assert.IsTrue(grade.IsPerfect);
            Assert.AreEqual(3, grade.CorrectCount);
            Assert.AreEqual(3, grade.TotalCount);
            Assert.AreEqual(0, grade.WrongCount);
            Assert.AreEqual(0, grade.UnassignedCount);
            Assert.IsTrue(grade.IsCorrect(V2));
        }

        [Test]
        public void Grade_CountsPartialCredit()
        {
            _board.Assign(V1, Culprit); // 맞음
            _board.Assign(V2, Victim);  // 틀림
            // V3은 끝내 못 붙였다

            VoiceAssignmentGrade grade = _board.Grade(Script());

            Assert.IsFalse(grade.IsPerfect);
            Assert.AreEqual(3, grade.TotalCount);
            Assert.AreEqual(1, grade.CorrectCount, "몇 개를 맞췄는가가 남아야 부분 점수를 줄 수 있다");
            Assert.AreEqual(1, grade.WrongCount);
            Assert.AreEqual(1, grade.UnassignedCount);

            CollectionAssert.AreEqual(new[] { V1 }, grade.CorrectVoices);
            CollectionAssert.AreEqual(new[] { V2 }, grade.WrongVoices);
            CollectionAssert.AreEqual(new[] { V3 }, grade.UnassignedVoices);

            Assert.IsTrue(grade.IsCorrect(V1));
            Assert.IsFalse(grade.IsCorrect(V2));
            Assert.IsFalse(grade.IsCorrect(V3));
        }

        [Test]
        public void Grade_EmptyBoardIsAllUnassigned_AndNotPerfect()
        {
            VoiceAssignmentGrade grade = _board.Grade(Script());

            Assert.IsFalse(grade.IsPerfect);
            Assert.AreEqual(0, grade.CorrectCount);
            Assert.AreEqual(3, grade.UnassignedCount);
        }

        [Test]
        public void Grade_AnswerWithoutNpcId_IsNeverCorrect()
        {
            var board = new VoiceAssignment(new[] { V1 });
            board.Assign(V1, Culprit);

            VoiceAssignmentGrade grade = board.Grade(new[] { new SpeakerDefinition { voiceId = V1, npcId = string.Empty } });

            Assert.AreEqual(1, grade.TotalCount);
            Assert.AreEqual(0, grade.CorrectCount, "정답이 비어 있는데 맞다고 해 주면 채점이 무너진다");
            Assert.AreEqual(1, grade.WrongCount);
        }

        [Test]
        public void Grade_WithoutAnAnswer_ScoresNothing()
        {
            _board.Assign(V1, Culprit);

            VoiceAssignmentGrade grade = _board.Grade((ScriptDefinition)null);

            Assert.AreEqual(0, grade.TotalCount);
            Assert.IsFalse(grade.IsPerfect, "채점할 것이 없는 상태를 만점으로 치지 않는다");
        }
    }
}
