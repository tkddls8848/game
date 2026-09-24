using System;
using Detective.Core;

namespace Detective.Data
{
    /// <summary>
    /// evidence.json의 단서 하나. 획득 여부는 데이터가 아니라 런타임 상태다(EvidenceLog).
    /// JsonUtility는 빠진 int 필드를 0(= 18:00)으로 채우므로, 시각이 없는 필드는 반드시 -1을 적는다.
    /// </summary>
    [Serializable]
    public class EvidenceDefinition
    {
        public string id;
        public string name;
        public string description;

        /// <summary>놓여 있는 방 id와 방 중심으로부터의 오프셋.</summary>
        public string foundRoom;
        public float offsetX;
        public float offsetY;

        /// <summary>관련 인물(없으면 빈 문자열)과 관련 시각(없으면 -1). 수사 노트 표시용.</summary>
        public string relatedNpc;
        public int relatedTick = -1;

        /// <summary>
        /// 물증이 확정해 주는 행적. revealNpc가 revealTick에 revealRoom에 있었다는 사실을 타임라인에 기록한다.
        /// 기록할 게 없으면 revealNpc를 비워 둔다.
        /// </summary>
        public string revealNpc;
        public int revealTick = -1;
        public string revealRoom;

        /// <summary>relatedTick을 ms로(경계 함수). 없으면 GameTime.NoTime.</summary>
        public int RelatedMs { get { return GameTime.TickToMs(relatedTick); } }

        /// <summary>revealTick을 ms로(경계 함수). 없으면 GameTime.NoTime.</summary>
        public int RevealMs { get { return GameTime.TickToMs(revealTick); } }

        public bool RevealsWhereabouts { get { return !string.IsNullOrEmpty(revealNpc); } }
    }

    /// <summary>단서가 아닌 분위기용 조사 대상. 조사하면 설명만 나온다.</summary>
    [Serializable]
    public class PropDefinition
    {
        public string name;
        public string description;
        public string room;
        public float offsetX;
        public float offsetY;
    }

    /// <summary>evidence.json 최상위 객체.</summary>
    [Serializable]
    public class EvidenceTable
    {
        public EvidenceDefinition[] evidence;
        public PropDefinition[] props;

        public EvidenceTable Normalized()
        {
            if (evidence == null) evidence = new EvidenceDefinition[0];
            if (props == null) props = new PropDefinition[0];
            return this;
        }
    }
}
