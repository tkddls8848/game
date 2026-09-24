using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.NPC;

namespace Detective.Dialogue
{
    /// <summary>"observer가 tick에 room에서 target을 봤다."</summary>
    public struct Sighting
    {
        public readonly string ObserverId;
        public readonly string TargetId;
        public readonly int Tick;
        public readonly string RoomId;

        public Sighting(string observerId, string targetId, int tick, string roomId)
        {
            ObserverId = observerId;
            TargetId = targetId;
            Tick = tick;
            RoomId = roomId;
        }
    }

    /// <summary>
    /// 목격 판정(순수 C#): 같은 시각 + 같은 방 = 목격. 시야 판정은 없다(§12).
    /// 거짓말하는 시각의 목격은 털어놓지 않는다 — 말하는 순간 자기가 그 방에 있었다는 게 드러나기 때문이다.
    /// </summary>
    public static class SightingGenerator
    {
        /// <summary>실제로 본 것 전부(거짓말 여부 무관). 사건 검증용.</summary>
        public static List<Sighting> Witnessed(NpcRoster roster, NpcDefinition observer)
        {
            var result = new List<Sighting>();
            if (observer == null || observer.isVictim) return result;

            for (int tick = GameTime.FirstTick; tick <= GameTime.LastTick; tick++)
            {
                string room = NpcSchedule.RoomAt(observer, tick);
                if (string.IsNullOrEmpty(room)) continue;

                List<NpcDefinition> occupants = roster.OccupantsAt(tick, room);
                for (int i = 0; i < occupants.Count; i++)
                {
                    if (occupants[i].id == observer.id) continue;
                    result.Add(new Sighting(observer.id, occupants[i].id, tick, room));
                }
            }
            return result;
        }

        /// <summary>대화에서 털어놓는 목격. 본인이 사실대로 말하는 시각의 것만.</summary>
        public static List<Sighting> Reported(NpcRoster roster, NpcDefinition observer)
        {
            var result = new List<Sighting>();
            List<Sighting> all = Witnessed(roster, observer);
            for (int i = 0; i < all.Count; i++)
            {
                if (NpcSchedule.IsHonestAt(observer, all[i].Tick)) result.Add(all[i]);
            }
            return result;
        }

        /// <summary>자동 생성 문구. 예: "18:20쯤 복도에서 클라라 보스를 봤습니다."</summary>
        public static string DefaultText(Sighting sighting, NpcRoster roster, RoomLayout layout)
        {
            return GameTime.ToLabel(sighting.Tick) + "쯤 " + layout.DisplayNameOf(sighting.RoomId) + "에서 "
                + KoreanText.EulReul(roster.DisplayNameOf(sighting.TargetId)) + " 봤습니다.";
        }
    }
}
