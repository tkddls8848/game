using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Dialogue;
using Detective.Investigation;
using Detective.NPC;

namespace Detective.Case
{
    /// <summary>
    /// "단서와 증언만으로 범인을 유일하게 특정할 수 있는가"를 기계적으로 확인한다(순수 C#, Phase 5 DoD).
    ///
    /// 모든 단서를 얻고 모든 대사를 들은 플레이어를 가정해 타임라인을 복원한 뒤:
    ///   1. 정답 자체가 스케줄·선택지와 맞는가
    ///   2. 목격·물증 기록이 실제 스케줄과 어긋나지 않는가(데이터가 플레이어를 속이면 안 된다)
    ///   3. 범행 시각에 범인만 "믿을 만한 알리바이"(목격·물증)가 없는가 → 범인이 유일하게 좁혀진다
    ///   4. 증언 ↔ 목격·물증 모순이 최소 N건 있고, 범인의 증언에도 모순이 있는가
    /// 사람이 읽는 추리 흐름을 완전히 대신하진 못하지만, 데이터를 고치다 사건이 풀 수 없게 되는 것은 막는다.
    /// </summary>
    public static class CaseSolvabilityChecker
    {
        public const int DefaultMinimumContradictions = 2;

        public static List<string> Check(CaseDatabase database)
        {
            return Check(database, DefaultMinimumContradictions);
        }

        public static List<string> Check(CaseDatabase database, int minimumContradictions)
        {
            var problems = new List<string>();
            CaseAnswer answer = database.Case.answer;

            CheckAnswer(database, problems);
            if (problems.Count > 0) return problems; // 정답이 깨져 있으면 이후 판정은 의미가 없다.

            TimelineBoard board = TimelineBoard.Build(FullKnowledge(database));

            CheckRecordsAreTruthful(database, board, problems);

            List<NpcDefinition> suspects = database.Npcs.Suspects;
            for (int i = 0; i < suspects.Count; i++)
            {
                NpcDefinition npc = suspects[i];
                bool alibi = HasReliableAlibi(board, npc.id, answer.TimeMs, answer.room);
                if (npc.id == answer.culprit && alibi)
                    problems.Add("범인 " + npc.id + " 에게 " + GameTime.ToLabel(answer.TimeMs) + " 알리바이(목격·물증)가 있다 — 범인으로 좁힐 수 없다.");
                if (npc.id != answer.culprit && !alibi)
                    problems.Add(npc.id + " 는 " + GameTime.ToLabel(answer.TimeMs) + "에 믿을 만한 알리바이(목격·물증)가 없다 — 범인 후보에서 지울 수 없다.");
            }

            int total = 0;
            int culpritContradictions = 0;
            for (int i = 0; i < suspects.Count; i++)
            {
                int count = CountContradictions(board, suspects[i].id);
                total += count;
                if (suspects[i].id == answer.culprit) culpritContradictions = count;
            }
            if (total < minimumContradictions)
                problems.Add("증언과 목격·물증의 모순이 " + total + "건뿐이다(최소 " + minimumContradictions + "건).");
            if (culpritContradictions == 0)
                problems.Add("범인의 증언을 반박하는 목격·물증이 하나도 없다.");

            return problems;
        }

        /// <summary>모든 단서를 얻고, 그 상태에서 모든 인물의 모든 대사를 들은 플레이어.</summary>
        public static InvestigationState FullKnowledge(CaseDatabase database)
        {
            var state = new InvestigationState(database);
            IList<EvidenceDefinition> evidence = database.Evidence.All;
            for (int i = 0; i < evidence.Count; i++) state.CollectEvidence(evidence[i].id);

            IList<NpcDefinition> npcs = database.Npcs.All;
            for (int n = 0; n < npcs.Count; n++)
            {
                List<ConversationLine> lines = ConversationBuilder.Build(state, npcs[n].id);
                for (int i = 0; i < lines.Count; i++) state.MarkHeard(npcs[n].id, lines[i].Key);
            }
            return state;
        }

        /// <summary>그 시각에 범행 장소가 아닌 다른 방에 있었다는 목격·물증이 있는가.</summary>
        public static bool HasReliableAlibi(TimelineBoard board, string npcId, int ms, string crimeRoom)
        {
            List<TimelineRecord> records = board.RecordsAt(npcId, ms);
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Kind != RecordKind.Testimony && records[i].RoomId != crimeRoom) return true;
            }
            return false;
        }

        /// <summary>본인 증언과 다른 방을 가리키는 목격·물증이 있는 10분 칸(틱)의 수.</summary>
        public static int CountContradictions(TimelineBoard board, string npcId)
        {
            int count = 0;
            for (int t = GameTime.FirstTick; t <= GameTime.LastTick; t++)
            {
                List<TimelineRecord> records = board.RecordsInTick(npcId, t);
                bool contradicted = false;
                for (int a = 0; a < records.Count && !contradicted; a++)
                {
                    if (records[a].Kind != RecordKind.Testimony) continue;
                    for (int b = 0; b < records.Count; b++)
                    {
                        if (records[b].Kind == RecordKind.Testimony) continue;
                        if (records[b].RoomId != records[a].RoomId) { contradicted = true; break; }
                    }
                }
                if (contradicted) count++;
            }
            return count;
        }

        private static void CheckAnswer(CaseDatabase database, List<string> problems)
        {
            CaseDefinition definition = database.Case;
            CaseAnswer answer = definition.answer;

            NpcDefinition culprit;
            if (!database.Npcs.TryGet(answer.culprit, out culprit)) problems.Add("정답 범인 '" + answer.culprit + "' 이 없는 인물이다.");
            else if (culprit.isVictim) problems.Add("정답 범인이 피해자다.");

            if (!GameTime.IsValid(answer.TimeMs)) problems.Add("정답 시각(tick) " + answer.tick + " 이 범위 밖이다.");

            RoomDefinition room;
            if (!database.Layout.TryGetRoom(answer.room, out room)) problems.Add("정답 장소 '" + answer.room + "' 이 없는 방이다.");

            if (!CaseDefinition.Contains(definition.motives, answer.motive)) problems.Add("정답 동기 '" + answer.motive + "' 가 선택지에 없다.");
            if (!CaseDefinition.Contains(definition.methods, answer.method)) problems.Add("정답 수법 '" + answer.method + "' 이 선택지에 없다.");

            EvidenceDefinition decisive;
            if (!database.Evidence.TryGet(answer.evidence, out decisive)) problems.Add("결정적 증거 '" + answer.evidence + "' 가 없는 단서다.");
            else if (decisive.relatedNpc != answer.culprit) problems.Add("결정적 증거의 relatedNpc가 범인이 아니다.");

            if (problems.Count > 0) return;

            if (NpcSchedule.RoomAt(culprit, answer.TimeMs) != answer.room)
                problems.Add("범인의 스케줄상 " + GameTime.ToLabel(answer.TimeMs) + "에 " + answer.room + " 에 있지 않다.");

            NpcDefinition victim = database.Npcs.Victim;
            if (victim != null && NpcSchedule.RoomAt(victim, answer.TimeMs) != answer.room)
                problems.Add("피해자의 스케줄상 " + GameTime.ToLabel(answer.TimeMs) + "에 " + answer.room + " 에 있지 않다.");
        }

        private static void CheckRecordsAreTruthful(CaseDatabase database, TimelineBoard board, List<string> problems)
        {
            IList<TimelineRecord> records = board.Records;
            for (int i = 0; i < records.Count; i++)
            {
                TimelineRecord record = records[i];
                if (record.Kind == RecordKind.Testimony) continue;

                NpcDefinition npc;
                if (!database.Npcs.TryGet(record.NpcId, out npc)) continue;
                if (NpcSchedule.RoomAt(npc, record.Ms) == record.RoomId) continue;

                problems.Add(record.Kind + "(" + record.SourceId + ") 이 " + record.NpcId + " 의 " + GameTime.ToLabel(record.Ms)
                    + " 위치를 " + record.RoomId + " 로 기록하지만 실제 스케줄과 다르다.");
            }
        }
    }
}
