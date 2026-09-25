using System;
using Detective.Core;

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
    /// 범행 시각은 JSON 호환을 위해 아직 틱으로 적는다. 코드는 <see cref="TimeMs"/>로 읽는다.
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

        /// <summary>
        /// 회차 안의 정확한 시각(ms). 적혀 있으면 <see cref="tick"/>보다 우선한다.
        ///
        /// 엿듣기 사건은 한 회차가 10분(600,000ms)인데 틱 한 칸도 600,000ms다 —
        /// 즉 회차 전체가 칸 하나여서 tick으로는 "회차 시작 4분 1.5초"를 적을 수 없다.
        /// 기존 저택 사건(case_01)은 18:00~19:00을 7칸으로 쓰므로 tick 경로를 그대로 둔다.
        ///
        /// **데이터에 -1을 반드시 명시한다.** JsonUtility는 빠진 int를 0으로 채우는데
        /// 0은 유효한 시각(회차 시작)이라, 적지 않으면 tick으로 적은 사건의 정답을 조용히 덮어쓴다.
        /// </summary>
        public int timeMs = GameTime.NoTime;

        /// <summary>정답 시각(ms). timeMs가 적혀 있으면 그것을, 아니면 tick을 경계 함수로 옮긴 값을.</summary>
        public int TimeMs
        {
            get { return GameTime.IsValid(timeMs) ? timeMs : GameTime.TickToMs(tick); }
        }
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
