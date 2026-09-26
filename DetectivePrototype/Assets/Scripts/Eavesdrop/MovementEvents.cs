using System.Collections.Generic;
using Detective.Core;
using Detective.Data;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 이동에서 소리를 만든다. <b>이 게임의 새 단서 층이다.</b>
    ///
    /// 사람이 방을 옮기면 필연적으로 소리가 난다 — 문이 열리고, 복도를 걷고, 문이 닫힌다.
    /// 그것을 작가가 손으로 수백 줄 적는 대신 이동 트랙에서 **기계적으로 뽑아낸다**.
    /// 트랙을 고치면 소리도 따라 고쳐지므로 둘이 어긋나지 않는다 — 손으로 적으면 반드시 어긋난다.
    ///
    /// 뽑아내는 것:
    ///   * 방을 떠날 때 그 방에서 문소리
    ///   * 이동 중 <b>지나가는 방마다</b> 발소리 (경로는 <see cref="RoomLayout.FindDoorPath"/>가 안다)
    ///   * 방에 들어갈 때 그 방에서 문소리
    ///
    /// 그래서 복도에 귀를 두면 누가 언제 지나갔는지 발소리로 셀 수 있다. 말은 한 마디도
    /// 안 들려도 "이 시각에 두 사람이 복도를 지났다"가 남는다. 그게 알리바이를 깬다.
    ///
    /// 이벤트 id는 <b>결정적</b>으로 만든다(npc·시각·종류). 같은 트랙이면 늘 같은 id가 나와야
    /// 들은 기록과 저장 파일이 회차를 다시 열어도 맞는다.
    /// </summary>
    public static class MovementEvents
    {
        /// <summary>문소리가 방을 떠나기 직전 / 들어간 직후 몇 ms에 나는가.</summary>
        private const int DoorOffsetMs = 200;

        /// <summary>발소리 한 덩이의 길이.</summary>
        private const int FootstepDurationMs = 1200;

        /// <summary>이 틈보다 짧은 이동은 소리를 만들지 않는다(같은 방 안에서의 자리 이동 취급).</summary>
        public const int MinTransitMs = 1500;

        /// <summary>
        /// 이동 트랙에서 소리를 뽑는다. 회차 길이를 넘는 것은 버린다.
        /// </summary>
        public static List<ScriptEvent> Derive(MovementTracks tracks, RoomLayout layout, int durationMs)
        {
            var result = new List<ScriptEvent>();
            if (tracks == null || layout == null) return result;

            foreach (MovementTrack track in tracks.All)
            {
                if (track == null || string.IsNullOrEmpty(track.npcId)) continue;
                TrackSegment[] segments = track.segments;
                if (segments == null) continue;

                for (int i = 1; i < segments.Length; i++)
                {
                    TrackSegment from = segments[i - 1];
                    TrackSegment to = segments[i];
                    if (from == null || to == null) continue;

                    int gap = to.startMs - from.endMs;
                    if (gap < MinTransitMs) continue;              // 자리만 옮긴 것으로 본다
                    if (from.room == to.room) continue;            // 같은 방으로 돌아온 기록
                    if (string.IsNullOrEmpty(from.room) || string.IsNullOrEmpty(to.room)) continue;

                    // 떠나는 문
                    Add(result, durationMs, Event(track.npcId, from.endMs - DoorOffsetMs, 0, from.room,
                        EventKind.Door, "문이 열린다", "door_out"));

                    // 지나가는 방마다 발소리. 복도에 귀를 두면 이것이 세어진다.
                    List<string> through = RoomsBetween(layout, from.room, to.room);
                    for (int r = 0; r < through.Count; r++)
                    {
                        // 이동 시간을 지나가는 방 수로 나눠 고르게 흩는다.
                        int at = from.endMs + (int)((long)gap * (r + 1) / (through.Count + 1))
                                 - FootstepDurationMs / 2;
                        Add(result, durationMs, Event(track.npcId, at, FootstepDurationMs, through[r],
                            EventKind.Footsteps, "발소리가 지나간다", "steps" + r));
                    }

                    // 들어가는 문
                    Add(result, durationMs, Event(track.npcId, to.startMs - DoorOffsetMs, 0, to.room,
                        EventKind.Door, "문이 닫힌다", "door_in"));
                }
            }

            result.Sort((a, b) =>
            {
                int byStart = a.startMs.CompareTo(b.startMs);
                return byStart != 0 ? byStart : string.CompareOrdinal(a.id, b.id);
            });
            return result;
        }

        /// <summary>
        /// 두 방 사이에 <b>거쳐 가는</b> 방들(양 끝은 뺀다). 문 연결을 따라간다 —
        /// 이 저택은 모든 방이 복도를 통해서만 이어지므로 보통 복도 하나가 나온다.
        /// </summary>
        public static List<string> RoomsBetween(RoomLayout layout, string fromRoom, string toRoom)
        {
            var through = new List<string>();
            List<DoorDefinition> path = layout.FindDoorPath(fromRoom, toRoom);
            if (path == null || path.Count == 0) return through;

            string here = fromRoom;
            for (int i = 0; i < path.Count; i++)
            {
                DoorDefinition door = path[i];
                // 문 반대쪽으로 넘어간다.
                string next = door.roomA == here ? door.roomB : door.roomA;
                if (string.IsNullOrEmpty(next)) break;
                if (next != toRoom) through.Add(next);
                here = next;
            }
            return through;
        }

        private static ScriptEvent Event(string npcId, int atMs, int durationMs, string room,
                                        string kind, string text, string tag)
        {
            return new ScriptEvent
            {
                // 결정적 id: 같은 트랙이면 늘 같은 값이 나와야 저장 파일이 맞는다.
                id = "mv_" + npcId + "_" + atMs + "_" + tag,
                startMs = atMs < 0 ? 0 : atMs,
                durationMs = durationMs,
                room = room,
                npcId = npcId,
                kind = kind,
                text = text,
                loud = false
            }.Normalized();
        }

        private static void Add(List<ScriptEvent> into, int durationMs, ScriptEvent e)
        {
            if (durationMs > 0 && e.startMs >= durationMs) return;
            into.Add(e);
        }
    }
}
