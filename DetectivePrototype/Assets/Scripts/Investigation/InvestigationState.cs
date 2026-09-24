using Detective.Core;

namespace Detective.Investigation
{
    /// <summary>
    /// 한 판의 수사 진행 상태(순수 C#). 데이터(CaseDatabase)는 읽기만 하고, 플레이어가 알아낸 것만 여기에 쌓는다.
    /// 수사 노트·타임라인 관찰·고발 화면은 전부 이 객체를 읽는다.
    /// </summary>
    public sealed class InvestigationState
    {
        public readonly CaseDatabase Database;
        public readonly EvidenceLog Evidence = new EvidenceLog();

        public InvestigationState(CaseDatabase database)
        {
            Database = database;
        }

        /// <summary>데이터에 있는 단서만 등록한다. 새로 얻었으면 true.</summary>
        public bool CollectEvidence(string evidenceId)
        {
            Data.EvidenceDefinition evidence;
            if (!Database.Evidence.TryGet(evidenceId, out evidence)) return false;
            return Evidence.Collect(evidenceId);
        }
    }
}
