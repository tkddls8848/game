using Detective.Data;

namespace Detective.NPC
{
    /// <summary>타임라인 관찰 화면에서 한 인물을 어느 방에, 어떤 설명과 함께 보여 줄지.</summary>
    public struct NpcPlacement
    {
        public readonly bool Known;
        public readonly string RoomId;

        /// <summary>이름표 아래 작은 글씨. 예: "본인 증언", "마르코 목격".</summary>
        public readonly string Caption;

        public NpcPlacement(string roomId, string caption)
        {
            Known = !string.IsNullOrEmpty(roomId);
            RoomId = roomId ?? string.Empty;
            Caption = caption ?? string.Empty;
        }

        public static readonly NpcPlacement Unknown = new NpcPlacement(null, null);
    }

    /// <summary>타임라인 관찰 화면이 인물 위치를 물어보는 곳.</summary>
    public interface ITimelinePlacementSource
    {
        NpcPlacement PlacementOf(NpcDefinition npc, int tick);
    }

    /// <summary>실제 스케줄을 그대로 보여 주는 소스. 개발 중 확인용(정답을 그대로 드러낸다).</summary>
    public sealed class ScheduleTruthSource : ITimelinePlacementSource
    {
        public NpcPlacement PlacementOf(NpcDefinition npc, int tick)
        {
            string room = NpcSchedule.RoomAt(npc, tick);
            return string.IsNullOrEmpty(room) ? NpcPlacement.Unknown : new NpcPlacement(room, "실제 행적");
        }
    }
}
