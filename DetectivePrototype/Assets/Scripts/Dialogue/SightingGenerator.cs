using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.NPC;

namespace Detective.Dialogue
{
    /// <summary>"observer가 Ms에 room에서 target을 봤다."</summary>
    public struct Sighting
    {
        public readonly string ObserverId;
        public readonly string TargetId;
        public readonly int Ms;
        public readonly string RoomId;

        public Sighting(string observerId, string targetId, int ms, string roomId)
        {
            ObserverId = observerId;
            TargetId = targetId;
            Ms = ms;
            RoomId = roomId;
        }

        /// <summary>Ms가 속한 10분 칸. 표시·대화 키용 파생값.</summary>
        public int Tick { get { return GameTime.TickOf(Ms); } }
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

            // 스케줄 데이터가 10분 칸이라 칸마다 한 번씩 본다.
            for (int tick = GameTime.FirstTick; tick <= GameTime.LastTick; tick++)
            {
                int ms = GameTime.TickToMs(tick);
                string room = NpcSchedule.RoomAt(observer, ms);
                if (string.IsNullOrEmpty(room)) continue;

                List<NpcDefinition> occupants = roster.OccupantsAt(ms, room);
                for (int i = 0; i < occupants.Count; i++)
                {
                    if (occupants[i].id == observer.id) continue;
                    result.Add(new Sighting(observer.id, occupants[i].id, ms, room));
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
                if (NpcSchedule.IsHonestAt(observer, all[i].Ms)) result.Add(all[i]);
            }
            return result;
        }

        /// <summary>자동 생성 문구. 예: "18:20쯤 복도에서 클라라 보스를 봤습니다."</summary>
        public static string DefaultText(Sighting sighting, NpcRoster roster, RoomLayout layout)
        {
            return GameTime.ToLabel(sighting.Ms) + "쯤 " + layout.DisplayNameOf(sighting.RoomId) + "에서 "
                + KoreanText.EulReul(roster.DisplayNameOf(sighting.TargetId)) + " 봤습니다.";
        }
    }
}
