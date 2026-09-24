using System.Collections.Generic;
using System.Text;
using Detective.Core;
using Detective.Data;
using Detective.NPC;

namespace Detective.Investigation
{
    /// <summary>수사 노트 목록 한 줄.</summary>
    public struct NotebookEntry
    {
        public readonly string Id;
        public readonly string Title;

        public NotebookEntry(string id, string title)
        {
            Id = id;
            Title = title;
        }
    }

    /// <summary>
    /// 수사 노트에 표시할 문구를 만든다(순수 C#). uGUI는 이 문자열을 그대로 찍기만 한다.
    /// 플레이어가 알아낸 것만 보여 준다 — 자동 추론이나 모순 표시는 하지 않는다(§11, §14).
    /// </summary>
    public sealed class NotebookPresenter
    {
        public const string AccentHex = "#FFD98C";
        public const string MutedHex = "#9EA8B8";

        private readonly InvestigationState _state;

        public NotebookPresenter(InvestigationState state)
        {
            _state = state;
        }

        private CaseDatabase Database { get { return _state.Database; } }

        // ----- 인물 ------------------------------------------------------------

        /// <summary>용의자 전원 + 피해자. 도착하자마자 모두 소개받았다는 설정이라 처음부터 보인다.</summary>
        public List<NotebookEntry> PeopleEntries()
        {
            var result = new List<NotebookEntry>();
            NpcDefinition victim = Database.Npcs.Victim;
            if (victim != null) result.Add(new NotebookEntry(victim.id, victim.displayName + " (피해자)"));

            List<NpcDefinition> suspects = Database.Npcs.Suspects;
            for (int i = 0; i < suspects.Count; i++) result.Add(new NotebookEntry(suspects[i].id, suspects[i].displayName));
            return result;
        }

        public string PersonDetail(string npcId)
        {
            NpcDefinition npc;
            if (!Database.Npcs.TryGet(npcId, out npc)) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("<size=34><b>").Append(npc.displayName).Append("</b></size>\n");
            sb.Append(Muted(npc.relation)).Append("\n\n");
            sb.Append(npc.description).Append("\n");

            AppendRelatedEvidence(sb, npc.id);
            return sb.ToString();
        }

        private void AppendRelatedEvidence(StringBuilder sb, string npcId)
        {
            bool header = false;
            IList<string> collected = _state.Evidence.InOrder;
            for (int i = 0; i < collected.Count; i++)
            {
                EvidenceDefinition evidence;
                if (!Database.Evidence.TryGet(collected[i], out evidence)) continue;
                if (evidence.relatedNpc != npcId) continue;

                if (!header)
                {
                    sb.Append("\n").Append(Accent("관련 단서")).Append("\n");
                    header = true;
                }
                sb.Append("· ").Append(evidence.name).Append("\n");
            }
        }

        // ----- 증거 ------------------------------------------------------------

        /// <summary>획득한 단서만, 획득한 순서대로.</summary>
        public List<NotebookEntry> EvidenceEntries()
        {
            var result = new List<NotebookEntry>();
            IList<string> collected = _state.Evidence.InOrder;
            for (int i = 0; i < collected.Count; i++)
            {
                result.Add(new NotebookEntry(collected[i], Database.Evidence.NameOf(collected[i])));
            }
            return result;
        }

        public string EvidenceDetail(string evidenceId)
        {
            EvidenceDefinition evidence;
            if (!_state.Evidence.Has(evidenceId) || !Database.Evidence.TryGet(evidenceId, out evidence)) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("<size=34><b>").Append(evidence.name).Append("</b></size>\n");
            sb.Append(Muted("발견 위치: " + Database.Layout.DisplayNameOf(evidence.foundRoom))).Append("\n\n");
            sb.Append(evidence.description).Append("\n");

            if (!string.IsNullOrEmpty(evidence.relatedNpc) || GameTime.IsValidTick(evidence.relatedTick))
            {
                sb.Append("\n").Append(Accent("메모")).Append("\n");
                if (!string.IsNullOrEmpty(evidence.relatedNpc))
                    sb.Append("· 관련 인물: ").Append(Database.Npcs.DisplayNameOf(evidence.relatedNpc)).Append("\n");
                if (GameTime.IsValidTick(evidence.relatedTick))
                    sb.Append("· 관련 시각: ").Append(GameTime.ToLabel(evidence.relatedTick)).Append("\n");
            }
            return sb.ToString();
        }

        // ----- 공통 ------------------------------------------------------------

        public static string Accent(string text) { return "<color=" + AccentHex + ">" + text + "</color>"; }
        public static string Muted(string text) { return "<color=" + MutedHex + ">" + text + "</color>"; }
    }
}
