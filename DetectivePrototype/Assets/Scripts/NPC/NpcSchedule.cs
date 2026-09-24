using Detective.Core;
using Detective.Data;

namespace Detective.NPC
{
    /// <summary>
    /// 스케줄 조회 규칙(순수 C#). "실제로 어디 있었나"와 "본인은 어디 있었다고 말하나"를 분리해 둔다.
    /// 시각은 ms로 묻는다. 데이터는 아직 10분 칸(틱) 배열이라 그 ms가 속한 칸의 값을 돌려준다(18:35 → 18:30 칸).
    /// </summary>
    public static class NpcSchedule
    {
        /// <summary>실제 행적. 범위 밖이거나 비어 있으면 빈 문자열.</summary>
        public static string RoomAt(NpcDefinition npc, int ms)
        {
            if (npc == null) return string.Empty;
            return Slot(npc.schedule, GameTime.TickOf(ms));
        }

        /// <summary>본인이 주장하는 행적. 주장이 없으면 실제 행적을 그대로 말한다.</summary>
        public static string ClaimedRoomAt(NpcDefinition npc, int ms)
        {
            if (npc != null)
            {
                string claim = Slot(npc.claims, GameTime.TickOf(ms));
                if (!string.IsNullOrEmpty(claim)) return claim;
            }
            return RoomAt(npc, ms);
        }

        /// <summary>그 시각에 대해 사실대로 말하는가. 거짓말하는 시각의 목격 정보는 털어놓지 않는다.</summary>
        public static bool IsHonestAt(NpcDefinition npc, int ms)
        {
            return ClaimedRoomAt(npc, ms) == RoomAt(npc, ms);
        }

        /// <summary>그 시각부터 거슬러 올라가 마지막으로 있던 방. 없으면 빈 문자열.</summary>
        public static string LastKnownRoom(NpcDefinition npc, int ms)
        {
            if (npc == null) return string.Empty;
            for (int t = GameTime.TickOf(GameTime.Clamp(ms)); t >= GameTime.FirstTick; t--)
            {
                string room = Slot(npc.schedule, t);
                if (!string.IsNullOrEmpty(room)) return room;
            }
            return string.Empty;
        }

        /// <summary>틱 배열 데이터의 한 칸. 칸이 없거나 비어 있으면 빈 문자열.</summary>
        private static string Slot(string[] rooms, int tick)
        {
            if (rooms == null || !GameTime.IsValidTick(tick) || tick >= rooms.Length) return string.Empty;
            return rooms[tick] ?? string.Empty;
        }
    }
}
