using System;

namespace Detective.Data
{
    /// <summary>고발 화면의 선택지 하나(동기·수법).</summary>
    [Serializable]
    public class ChoiceDefinition
    {
        public string id;
        public string label;
    }

    /// <summary>
    /// 사건의 정답. 고발 화면의 6개 항목과 1:1로 대응한다.
    /// 범인(npc id) / 동기(motives의 id) / 범행 시각(틱) / 범행 장소(방 id) / 수법(methods의 id) / 결정적 증거(evidence id)
    /// </summary>
    [Serializable]
    public class CaseAnswer
    {
        public string culprit;
        public string motive;
        public int tick = -1;
        public string room;
        public string method;
        public string evidence;
    }

    /// <summary>cases/case_XX.json. 새 사건은 이 파일과 인물·단서·대사 JSON만 추가하면 된다.</summary>
    [Serializable]
    public class CaseDefinition
    {
        public string id;
        public string title;

        /// <summary>시작 화면 문구.</summary>
        public string intro;

        /// <summary>정답/오답 결과 화면 문구.</summary>
        public string solvedText;
        public string failedText;

        public CaseAnswer answer;
        public ChoiceDefinition[] motives;
        public ChoiceDefinition[] methods;

        public CaseDefinition Normalized()
        {
            if (answer == null) answer = new CaseAnswer();
            if (motives == null) motives = new ChoiceDefinition[0];
            if (methods == null) methods = new ChoiceDefinition[0];
            return this;
        }

        public static string LabelOf(ChoiceDefinition[] choices, string id)
        {
            if (choices == null) return id;
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] != null && choices[i].id == id) return choices[i].label;
            }
            return id;
        }

        public static bool Contains(ChoiceDefinition[] choices, string id)
        {
            if (choices == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] != null && choices[i].id == id) return true;
            }
            return false;
        }
    }
}
