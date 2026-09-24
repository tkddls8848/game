using System.Collections.Generic;
using Detective.Data;
using Detective.Dialogue;
using Detective.Investigation;
using Detective.NPC;

namespace Detective.Core
{
    /// <summary>
    /// 사건 하나를 이루는 읽기 전용 데이터 묶음(순수 C#).
    /// 로딩(UnityEngine)은 GameDataLoader가, 조회는 이 객체가 맡는다.
    /// 런타임 진행 상태(획득한 단서 등)는 여기에 넣지 않는다 — 데이터 파일은 읽기 전용이다.
    /// </summary>
    public sealed class CaseDatabase
    {
        public readonly RoomLayout Layout;
        public readonly NpcRoster Npcs;
        public readonly EvidenceCatalog Evidence;
        public readonly DialogueCatalog Dialogues;

        /// <summary>사건 정의(정답·선택지·문구). 항상 null이 아니다.</summary>
        public readonly CaseDefinition Case;

        public CaseDatabase(RoomLayout layout, NpcRoster npcs, EvidenceTable evidence, IEnumerable<DialogueFile> dialogues,
            CaseDefinition caseDefinition)
        {
            Case = (caseDefinition ?? new CaseDefinition()).Normalized();
            Layout = layout;
            Npcs = npcs ?? new NpcRoster(null);
            Evidence = new EvidenceCatalog(evidence);
            Dialogues = new DialogueCatalog(dialogues);
        }
    }
}
