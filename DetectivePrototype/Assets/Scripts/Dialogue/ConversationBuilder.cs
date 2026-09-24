using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Investigation;
using Detective.NPC;

namespace Detective.Dialogue
{
    /// <summary>대화창에 실제로 한 줄씩 띄울 대사. JSON 대사와 자동 생성 목격 대사를 같은 모양으로 다룬다.</summary>
    public sealed class ConversationLine
    {
        /// <summary>"npcId|lineId" 또는 "npcId|sighting|tick|targetId". 들었는지 기록하는 키다.</summary>
        public string Key;
        public string NpcId;
        public string Text;

        public bool RevealsClaims;
        public string RequiresEvidence;
        public int ClaimTick = -1;
        public string ClaimRoom;

        public bool IsSighting;
        public Sighting Sighting;

        public bool IsConditional { get { return !string.IsNullOrEmpty(RequiresEvidence); } }
    }

    /// <summary>
    /// 인물 한 명과의 대화를 조립한다(순수 C#).
    /// 순서: 일반 대사 → 목격 대사(시각순) → 조건부 대사(가진 단서에 반응).
    /// </summary>
    public static class ConversationBuilder
    {
        /// <summary>그 인물이 할 수 있는 모든 대사(조건 무시). 타임라인 복원과 사건 검증이 쓴다.</summary>
        public static List<ConversationLine> AllLines(CaseDatabase database, string npcId)
        {
            var result = new List<ConversationLine>();
            NpcDefinition npc;
            if (!database.Npcs.TryGet(npcId, out npc) || npc.isVictim) return result;

            DialogueLine[] lines = database.Dialogues.LinesOf(npcId);
            var conditional = new List<ConversationLine>();

            for (int i = 0; i < lines.Length; i++)
            {
                DialogueLine line = lines[i];
                if (line == null || line.IsSightingOverride) continue;

                var converted = new ConversationLine
                {
                    Key = npcId + "|" + line.id,
                    NpcId = npcId,
                    Text = line.text,
                    RevealsClaims = line.revealsClaims,
                    RequiresEvidence = line.requiresEvidence,
                    ClaimTick = line.IsClaim ? line.claimTick : -1,
                    ClaimRoom = line.IsClaim ? line.claimRoom : null
                };

                if (converted.IsConditional) conditional.Add(converted);
                else result.Add(converted);
            }

            List<Sighting> sightings = SightingGenerator.Reported(database.Npcs, npc);
            for (int i = 0; i < sightings.Count; i++)
            {
                Sighting sighting = sightings[i];
                result.Add(new ConversationLine
                {
                    Key = npcId + "|sighting|" + sighting.Tick + "|" + sighting.TargetId,
                    NpcId = npcId,
                    Text = OverrideText(lines, sighting) ?? SightingGenerator.DefaultText(sighting, database.Npcs, database.Layout),
                    IsSighting = true,
                    Sighting = sighting
                });
            }

            result.AddRange(conditional);
            return result;
        }

        /// <summary>
        /// 지금 들을 수 있는 대사. 아직 못 들은 대사가 있으면 그것만, 전부 들었으면 처음부터 다시 들려준다.
        /// </summary>
        public static List<ConversationLine> Build(InvestigationState state, string npcId)
        {
            var available = new List<ConversationLine>();
            List<ConversationLine> all = AllLines(state.Database, npcId);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsConditional && !state.Evidence.Has(all[i].RequiresEvidence)) continue;
                available.Add(all[i]);
            }

            var unheard = new List<ConversationLine>();
            for (int i = 0; i < available.Count; i++)
            {
                if (!state.HasHeard(available[i].Key)) unheard.Add(available[i]);
            }
            return unheard.Count > 0 ? unheard : available;
        }

        private static string OverrideText(DialogueLine[] lines, Sighting sighting)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                DialogueLine line = lines[i];
                if (line == null || !line.IsSightingOverride) continue;
                if (line.sightingTick == sighting.Tick && line.sightingTarget == sighting.TargetId) return line.text;
            }
            return null;
        }
    }
}
