using System.Collections.Generic;
using Detective.Data;
using Detective.Investigation;
using Detective.NPC;

namespace Detective.Core
{
    /// <summary>
    /// 인물·단서·대화·사건 JSON 사이의 참조 무결성 검사(순수 C#).
    /// rooms.json 자체의 모양 검사는 RoomLayoutValidator가 따로 한다.
    /// 씬 빌더와 EditMode 테스트가 같은 규칙을 쓰므로, 여기서 통과하면 둘 다 통과한다.
    /// </summary>
    public static class GameDataValidator
    {
        public static List<string> Validate(CaseDatabase database)
        {
            var errors = new List<string>();
            if (database == null)
            {
                errors.Add("사건 데이터를 읽지 못했다 (database == null).");
                return errors;
            }

            ValidateNpcs(database, errors);
            ValidateEvidence(database, errors);
            return errors;
        }

        private static void ValidateNpcs(CaseDatabase database, List<string> errors)
        {
            IList<NpcDefinition> npcs = database.Npcs.All;
            if (npcs.Count == 0) errors.Add("인물이 하나도 없다.");

            var ids = new HashSet<string>();
            int victims = 0;
            for (int i = 0; i < npcs.Count; i++)
            {
                NpcDefinition npc = npcs[i];
                string label = string.IsNullOrEmpty(npc.id) ? "npcs[" + i + "]" : npc.id;

                if (string.IsNullOrEmpty(npc.id)) errors.Add(label + ": id가 비어 있다.");
                else if (!ids.Add(npc.id)) errors.Add(label + ": id가 중복된다.");
                if (string.IsNullOrEmpty(npc.displayName)) errors.Add(label + ": displayName이 비어 있다.");
                if (npc.isVictim) victims++;

                if (npc.schedule.Length != GameTime.TickCount)
                {
                    errors.Add(label + ": schedule 길이가 " + npc.schedule.Length + "이다(" + GameTime.TickCount + "이어야 한다).");
                }

                for (int t = 0; t < npc.schedule.Length; t++)
                {
                    string room = npc.schedule[t];
                    if (string.IsNullOrEmpty(room))
                    {
                        // 용의자는 매 시각 어딘가에 있어야 한다. 비어 있어도 되는 건 사망 이후의 피해자뿐이다.
                        if (!npc.isVictim) errors.Add(label + ": " + GameTime.ToLabel(t) + " 스케줄이 비어 있다.");
                        continue;
                    }
                    if (!HasRoom(database, room)) errors.Add(label + ": " + GameTime.ToLabel(t) + " 스케줄의 방 '" + room + "' 이 없다.");
                }

                if (npc.claims.Length != 0 && npc.claims.Length != GameTime.TickCount)
                {
                    errors.Add(label + ": claims 길이는 0 또는 " + GameTime.TickCount + "이어야 한다(현재 " + npc.claims.Length + ").");
                }
                for (int t = 0; t < npc.claims.Length; t++)
                {
                    string claim = npc.claims[t];
                    if (!string.IsNullOrEmpty(claim) && !HasRoom(database, claim))
                        errors.Add(label + ": " + GameTime.ToLabel(t) + " 주장의 방 '" + claim + "' 이 없다.");
                }
                if (npc.isVictim && npc.claims.Length > 0 && HasAnyClaim(npc))
                    errors.Add(label + ": 피해자는 증언하지 않으므로 claims가 비어 있어야 한다.");
            }

            if (victims != 1) errors.Add("피해자(isVictim)는 정확히 한 명이어야 한다(현재 " + victims + "명).");
        }

        /// <summary>소품이 벽에 박히지 않도록 방 가장자리에서 이만큼은 떨어져야 한다.</summary>
        public const float PlacementMargin = 0.8f;

        private static void ValidateEvidence(CaseDatabase database, List<string> errors)
        {
            EvidenceCatalog catalog = database.Evidence;
            if (catalog.All.Count == 0) errors.Add("단서(evidence.json)가 하나도 없다.");

            var ids = new HashSet<string>();
            for (int i = 0; i < catalog.All.Count; i++)
            {
                EvidenceDefinition evidence = catalog.All[i];
                string label = string.IsNullOrEmpty(evidence.id) ? "evidence[" + i + "]" : evidence.id;

                if (string.IsNullOrEmpty(evidence.id)) errors.Add(label + ": id가 비어 있다.");
                else if (!ids.Add(evidence.id)) errors.Add(label + ": id가 중복된다.");
                if (string.IsNullOrEmpty(evidence.name)) errors.Add(label + ": name이 비어 있다.");
                if (string.IsNullOrEmpty(evidence.description)) errors.Add(label + ": description이 비어 있다.");

                ValidatePlacement(database, label, evidence.foundRoom, evidence.offsetX, evidence.offsetY, errors);

                if (!string.IsNullOrEmpty(evidence.relatedNpc) && !HasNpc(database, evidence.relatedNpc))
                    errors.Add(label + ": relatedNpc '" + evidence.relatedNpc + "' 가 없는 인물이다.");
                if (evidence.relatedTick != -1 && !GameTime.IsValidTick(evidence.relatedTick))
                    errors.Add(label + ": relatedTick " + evidence.relatedTick + " 은 -1 또는 0~" + GameTime.LastTick + "이어야 한다.");

                if (evidence.RevealsWhereabouts)
                {
                    if (!HasNpc(database, evidence.revealNpc))
                        errors.Add(label + ": revealNpc '" + evidence.revealNpc + "' 가 없는 인물이다.");
                    if (!GameTime.IsValidTick(evidence.revealTick))
                        errors.Add(label + ": revealTick " + evidence.revealTick + " 이 범위 밖이다.");
                    if (!HasRoom(database, evidence.revealRoom))
                        errors.Add(label + ": revealRoom '" + evidence.revealRoom + "' 이 없는 방이다.");
                }
                else if (evidence.revealTick != -1 || !string.IsNullOrEmpty(evidence.revealRoom))
                {
                    errors.Add(label + ": revealNpc 없이 revealTick/revealRoom만 적혀 있다.");
                }
            }

            for (int i = 0; i < catalog.Props.Count; i++)
            {
                PropDefinition prop = catalog.Props[i];
                string label = "props[" + i + "] " + prop.name;
                if (string.IsNullOrEmpty(prop.name)) errors.Add(label + ": name이 비어 있다.");
                ValidatePlacement(database, label, prop.room, prop.offsetX, prop.offsetY, errors);
            }
        }

        private static void ValidatePlacement(CaseDatabase database, string label, string roomId, float offsetX, float offsetY, List<string> errors)
        {
            RoomDefinition room;
            if (!database.Layout.TryGetRoom(roomId, out room))
            {
                errors.Add(label + ": 방 '" + roomId + "' 이 없다.");
                return;
            }

            float limitX = room.width * 0.5f - PlacementMargin;
            float limitY = room.height * 0.5f - PlacementMargin;
            if (offsetX < -limitX || offsetX > limitX || offsetY < -limitY || offsetY > limitY)
                errors.Add(label + ": 오프셋 (" + offsetX + ", " + offsetY + ") 이 방 " + roomId + " 밖으로 나간다.");
        }

        private static bool HasNpc(CaseDatabase database, string npcId)
        {
            NpcDefinition npc;
            return database.Npcs.TryGet(npcId, out npc);
        }

        private static bool HasAnyClaim(NpcDefinition npc)
        {
            for (int t = 0; t < npc.claims.Length; t++) if (!string.IsNullOrEmpty(npc.claims[t])) return true;
            return false;
        }

        private static bool HasRoom(CaseDatabase database, string roomId)
        {
            RoomDefinition room;
            return database.Layout.TryGetRoom(roomId, out room);
        }
    }
}
