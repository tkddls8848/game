using System.Collections.Generic;
using System.Text;
using Detective.Core;
using Detective.Data;
using Detective.Dialogue;
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
        public const string AccentHex = "#8A2E2A"; // 붉은 잉크
        public const string MutedHex = "#6B5F52";  // 바랜 잉크

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

            AppendStatements(sb, npc);
            AppendSightingsOf(sb, npc.id);
            AppendRelatedEvidence(sb, npc.id);
            return sb.ToString();
        }

        /// <summary>이 사람에게 직접 들은 말(목격 포함). 들은 것만.</summary>
        private void AppendStatements(StringBuilder sb, NpcDefinition npc)
        {
            if (npc.isVictim) return;
            if (!_state.HasTalkedTo(npc.id))
            {
                sb.Append("\n").Append(Muted("아직 이야기를 나누지 않았다.")).Append("\n");
                return;
            }

            sb.Append("\n").Append(Accent("들은 이야기")).Append("\n");
            List<ConversationLine> lines = ConversationBuilder.AllLines(Database, npc.id);
            for (int i = 0; i < lines.Count; i++)
            {
                if (!_state.HasHeard(lines[i].Key)) continue;
                sb.Append("· ").Append(lines[i].Text).Append("\n");
            }
        }

        /// <summary>다른 사람들이 이 사람을 봤다고 한 것.</summary>
        private void AppendSightingsOf(StringBuilder sb, string npcId)
        {
            bool header = false;
            IList<NpcDefinition> all = Database.Npcs.All;
            for (int n = 0; n < all.Count; n++)
            {
                List<ConversationLine> lines = ConversationBuilder.AllLines(Database, all[n].id);
                for (int i = 0; i < lines.Count; i++)
                {
                    ConversationLine line = lines[i];
                    if (!line.IsSighting || line.Sighting.TargetId != npcId || !_state.HasHeard(line.Key)) continue;

                    if (!header)
                    {
                        sb.Append("\n").Append(Accent("다른 사람의 목격")).Append("\n");
                        header = true;
                    }
                    sb.Append("· ").Append(GameTime.ToLabel(line.Sighting.Ms)).Append(" ")
                        .Append(Database.Layout.DisplayNameOf(line.Sighting.RoomId))
                        .Append(Muted(" — " + all[n].displayName)).Append("\n");
                }
            }
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

            if (!string.IsNullOrEmpty(evidence.relatedNpc) || GameTime.IsValid(evidence.RelatedMs))
            {
                sb.Append("\n").Append(Accent("메모")).Append("\n");
                if (!string.IsNullOrEmpty(evidence.relatedNpc))
                    sb.Append("· 관련 인물: ").Append(Database.Npcs.DisplayNameOf(evidence.relatedNpc)).Append("\n");
                if (GameTime.IsValid(evidence.RelatedMs))
                    sb.Append("· 관련 시각: ").Append(GameTime.ToLabel(evidence.RelatedMs)).Append("\n");
            }
            return sb.ToString();
        }

        // ----- 타임라인 ----------------------------------------------------------

        /// <summary>타임라인 표의 행: 피해자 → 용의자.</summary>
        public List<NotebookEntry> TimelineRows()
        {
            return PeopleEntries();
        }

        /// <summary>
        /// 표 한 칸(10분, 틱): 그 칸 그 사람에 대한 기록을 한 줄씩. 예: "식당 (본인)", "창고 (마르코)", "식당 (물증)".
        /// 서로 어긋나는 기록도 나란히 적는다 — 어느 쪽이 참인지는 플레이어가 판단한다.
        /// </summary>
        public string TimelineCell(TimelineBoard board, string npcId, int tick)
        {
            List<TimelineRecord> records = board.RecordsInTick(npcId, tick);
            if (records.Count == 0) return Muted("?");

            var sb = new StringBuilder();
            for (int i = 0; i < records.Count; i++)
            {
                if (i > 0) sb.Append("\n");
                sb.Append(Database.Layout.DisplayNameOf(records[i].RoomId))
                    .Append(" ").Append(Muted("(" + board.SourceLabel(records[i], false) + ")"));
            }
            return sb.ToString();
        }

        public const string TimelineLegend = "(본인) 본인 증언   (이름) 그 사람이 목격   (물증) 단서로 확인   ? 모름";

        // ----- 공통 ------------------------------------------------------------

        public static string Accent(string text) { return "<color=" + AccentHex + ">" + text + "</color>"; }
        public static string Muted(string text) { return "<color=" + MutedHex + ">" + text + "</color>"; }
    }
}
