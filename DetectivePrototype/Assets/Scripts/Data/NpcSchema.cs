using System;

namespace Detective.Data
{
    /// <summary>
    /// npcs/*.json 하나 = 인물 한 명. 피해자도 같은 스키마를 쓴다(isVictim).
    /// 스케줄은 틱 인덱스(0~6)마다 방 id 하나 — JsonUtility가 다룰 수 있게 딕셔너리 대신 고정 길이 배열로 둔다.
    /// </summary>
    [Serializable]
    public class NpcDefinition
    {
        public string id;
        public string displayName;

        /// <summary>외모·인상. 수사 노트 인물 탭에 그대로 나온다.</summary>
        public string description;

        /// <summary>피해자와의 관계.</summary>
        public string relation;

        public bool isVictim;

        /// <summary>표시 색 "#RRGGBB".</summary>
        public string color;

        /// <summary>실제 행적. schedule[tick] = 그 시각에 있던 방 id. 빈 문자열 = 그 시각엔 없음(사망 이후 등).</summary>
        public string[] schedule;

        /// <summary>
        /// 본인이 주장하는 행적. 비어 있거나 claims[tick]이 빈 문자열이면 그 시각은 사실대로 말한다.
        /// 거짓말은 여기에만 적는다 — 목격 대사는 이 값과 schedule을 비교해 자동으로 걸러진다.
        /// </summary>
        public string[] claims;

        /// <summary>현재(19:00) 서 있는 자리. 방 중심에서의 오프셋.</summary>
        public float presentOffsetX;
        public float presentOffsetY;

        public bool IsSuspect { get { return !isVictim; } }

        public NpcDefinition Normalized()
        {
            if (schedule == null) schedule = new string[0];
            if (claims == null) claims = new string[0];
            return this;
        }
    }
}
