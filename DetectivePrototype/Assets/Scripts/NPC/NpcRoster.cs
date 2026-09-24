using System.Collections.Generic;
using Detective.Data;

namespace Detective.NPC
{
    /// <summary>등장인물 목록(순수 C#). id 조회와 "같은 시각·같은 방" 질의를 담당한다.</summary>
    public sealed class NpcRoster
    {
        private readonly List<NpcDefinition> _all;
        private readonly Dictionary<string, NpcDefinition> _byId = new Dictionary<string, NpcDefinition>();

        public NpcRoster(IEnumerable<NpcDefinition> npcs)
        {
            _all = new List<NpcDefinition>();
            if (npcs == null) return;

            foreach (NpcDefinition npc in npcs)
            {
                if (npc == null) continue;
                npc.Normalized();
                _all.Add(npc);
                if (!string.IsNullOrEmpty(npc.id)) _byId[npc.id] = npc;
            }

            // 파일 로드 순서와 무관하게 항상 같은 순서가 되도록 id로 정렬한다.
            _all.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        }

        public IList<NpcDefinition> All { get { return _all.AsReadOnly(); } }

        /// <summary>피해자를 뺀 용의자들(고발 대상).</summary>
        public List<NpcDefinition> Suspects
        {
            get
            {
                var result = new List<NpcDefinition>();
                for (int i = 0; i < _all.Count; i++) if (_all[i].IsSuspect) result.Add(_all[i]);
                return result;
            }
        }

        public NpcDefinition Victim
        {
            get
            {
                for (int i = 0; i < _all.Count; i++) if (_all[i].isVictim) return _all[i];
                return null;
            }
        }

        public bool TryGet(string id, out NpcDefinition npc)
        {
            npc = null;
            if (string.IsNullOrEmpty(id)) return false;
            return _byId.TryGetValue(id, out npc);
        }

        public string DisplayNameOf(string id)
        {
            NpcDefinition npc;
            return TryGet(id, out npc) ? npc.displayName : id;
        }

        /// <summary>그 시각(ms) 그 방에 실제로 있던 인물들.</summary>
        public List<NpcDefinition> OccupantsAt(int ms, string roomId)
        {
            var result = new List<NpcDefinition>();
            if (string.IsNullOrEmpty(roomId)) return result;

            for (int i = 0; i < _all.Count; i++)
            {
                if (NpcSchedule.RoomAt(_all[i], ms) == roomId) result.Add(_all[i]);
            }
            return result;
        }
    }
}
