using System;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 대본의 발화 하나. 시각은 전부 정수 밀리초다(부동소수 시각 비교 금지).
    /// 재생 구간은 [startMs, startMs + durationMs) 반열린 구간이다.
    /// JsonUtility가 다룰 수 있도록 public 필드만 쓴다(프로퍼티/딕셔너리 금지).
    /// </summary>
    [Serializable]
    public class Utterance
    {
        public string id;
        public int startMs;
        public int durationMs;

        /// <summary>화면에 보이는 목소리 ID. 실제 인물은 <see cref="SpeakerDefinition"/>이 숨겨서 대응시킨다.</summary>
        public string voiceId;

        /// <summary>발화가 울리는 방. rooms.json의 방 ID.</summary>
        public string room;

        public string text;

        /// <summary>나중에 소리를 얹을 자리. 지금은 빈 문자열이다.</summary>
        public string clip;

        public int EndMs { get { return startMs + durationMs; } }
    }

    /// <summary>목소리 → 실제 인물 대응표의 한 줄. 플레이어에게는 숨기고 채점에만 쓴다.</summary>
    [Serializable]
    public class SpeakerDefinition
    {
        public string voiceId;
        public string npcId;
    }

    /// <summary>
    /// 들어야 알 수 있는 사실 하나. <c>utteranceIds</c> 중 하나라도 온전히(Full) 들으면 그 사실을 안다.
    /// </summary>
    [Serializable]
    public class ScriptFact
    {
        public string id;
        public string description;
        public string[] utteranceIds = new string[0];
    }

    /// <summary>여러 사실을 합쳐야 나오는 결론. <c>requiresFacts</c>를 전부 알아야 도출된다.</summary>
    [Serializable]
    public class ScriptConclusion
    {
        public string id;
        public string text;
        public string[] requiresFacts = new string[0];
    }

    /// <summary>대본 파일 최상위 객체(cases/.../script*.json).</summary>
    [Serializable]
    public class ScriptDefinition
    {
        public string caseId;

        /// <summary>한 회차 길이(ms). 0 이하면 잘못된 대본이다 — JsonUtility는 빠진 int를 0으로 채우므로 반드시 적는다.</summary>
        public int durationMs = -1;

        public SpeakerDefinition[] speakers = new SpeakerDefinition[0];
        public Utterance[] utterances = new Utterance[0];
        public ScriptFact[] facts = new ScriptFact[0];
        public ScriptConclusion[] conclusions = new ScriptConclusion[0];

        /// <summary>JSON에서 빠진 배열/문자열을 빈 값으로 채운다. 자기 자신을 돌려준다.</summary>
        public ScriptDefinition Normalized()
        {
            if (speakers == null) speakers = new SpeakerDefinition[0];
            if (utterances == null) utterances = new Utterance[0];
            if (facts == null) facts = new ScriptFact[0];
            if (conclusions == null) conclusions = new ScriptConclusion[0];

            for (int i = 0; i < utterances.Length; i++)
            {
                if (utterances[i] == null) continue;
                if (utterances[i].clip == null) utterances[i].clip = string.Empty;
                if (utterances[i].text == null) utterances[i].text = string.Empty;
            }
            for (int i = 0; i < facts.Length; i++)
            {
                if (facts[i] != null && facts[i].utteranceIds == null) facts[i].utteranceIds = new string[0];
            }
            for (int i = 0; i < conclusions.Length; i++)
            {
                if (conclusions[i] != null && conclusions[i].requiresFacts == null) conclusions[i].requiresFacts = new string[0];
            }
            return this;
        }
    }
}
