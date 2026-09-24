using Detective.Data;

namespace Detective.Case
{
    /// <summary>고발 화면의 6개 항목. 순서가 곧 화면 순서다.</summary>
    public enum AccusationField
    {
        Culprit = 0,
        Motive = 1,
        Time = 2,
        Place = 3,
        Method = 4,
        Evidence = 5
    }

    /// <summary>채점 결과. 표시는 SOLVED/FAILED 두 가지뿐이지만 항목별 정오는 남겨 둔다(부분 정답·다중 엔딩 확장용, §16).</summary>
    public sealed class CaseGradeResult
    {
        public const int FieldCount = 6;

        private readonly bool[] _correct = new bool[FieldCount];

        public bool IsCorrect(AccusationField field) { return _correct[(int)field]; }

        internal void Set(AccusationField field, bool correct) { _correct[(int)field] = correct; }

        public int CorrectCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < FieldCount; i++) if (_correct[i]) count++;
                return count;
            }
        }

        public bool Solved { get { return CorrectCount == FieldCount; } }
    }

    /// <summary>채점(순수 함수). 정답과 제출을 같은 CaseAnswer 모양으로 받아 항목별로 비교한다.</summary>
    public static class CaseGrader
    {
        public static CaseGradeResult Grade(CaseAnswer answer, CaseAnswer submission)
        {
            var result = new CaseGradeResult();
            if (answer == null || submission == null) return result;

            result.Set(AccusationField.Culprit, Same(answer.culprit, submission.culprit));
            result.Set(AccusationField.Motive, Same(answer.motive, submission.motive));
            result.Set(AccusationField.Time, answer.tick >= 0 && answer.tick == submission.tick);
            result.Set(AccusationField.Place, Same(answer.room, submission.room));
            result.Set(AccusationField.Method, Same(answer.method, submission.method));
            result.Set(AccusationField.Evidence, Same(answer.evidence, submission.evidence));
            return result;
        }

        /// <summary>빈 값끼리는 정답으로 치지 않는다(아무것도 안 고른 제출이 빈 정답과 맞아떨어지지 않게).</summary>
        private static bool Same(string expected, string actual)
        {
            return !string.IsNullOrEmpty(expected) && expected == actual;
        }
    }
}
