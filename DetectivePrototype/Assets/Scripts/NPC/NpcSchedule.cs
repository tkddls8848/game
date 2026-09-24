using Detective.Core;
using Detective.Data;

namespace Detective.NPC
{
    /// <summary>
    /// 스케줄 조회 규칙(순수 C#). "실제로 어디 있었나"와 "본인은 어디 있었다고 말하나"를 분리해 둔다.
    /// </summary>
    public static class NpcSchedule
    {
        /// <summary>실제 행적. 범위 밖이거나 비어 있으면 빈 문자열.</summary>
        public static string RoomAt(NpcDefinition npc, int tick)
        {
            if (npc == null || npc.schedule == null) return string.Empty;
            if (!GameTime.IsValidTick(tick) || tick >= npc.schedule.Length) return string.Empty;
            return npc.schedule[tick] ?? string.Empty;
        }

        /// <summary>본인이 주장하는 행적. 주장이 없으면 실제 행적을 그대로 말한다.</summary>
        public static string ClaimedRoomAt(NpcDefinition npc, int tick)
        {
            if (npc != null && npc.claims != null && GameTime.IsValidTick(tick) && tick < npc.claims.Length)
            {
                string claim = npc.claims[tick];
                if (!string.IsNullOrEmpty(claim)) return claim;
            }
            return RoomAt(npc, tick);
        }

        /// <summary>그 시각에 대해 사실대로 말하는가. 거짓말하는 시각의 목격 정보는 털어놓지 않는다.</summary>
        public static bool IsHonestAt(NpcDefinition npc, int tick)
        {
            return ClaimedRoomAt(npc, tick) == RoomAt(npc, tick);
        }

        /// <summary>가장 늦은 시각부터 거슬러 올라가 마지막으로 있던 방. 없으면 빈 문자열.</summary>
        public static string LastKnownRoom(NpcDefinition npc, int tick)
        {
            for (int t = GameTime.Clamp(tick); t >= GameTime.FirstTick; t--)
            {
                string room = RoomAt(npc, t);
                if (!string.IsNullOrEmpty(room)) return room;
            }
            return string.Empty;
        }
    }
}
