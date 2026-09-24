using System.Collections.Generic;
using Detective.Core;
using Detective.Data;

namespace Detective.Eavesdrop
{
    /// <summary>청취점에서 발화가 어떻게 들리는가(DEVELOPMENT_PLAN_UNHEARD.md §4).</summary>
    public enum Audibility
    {
        /// <summary>아무것도 들리지 않는다.</summary>
        None = 0,

        /// <summary>문 너머 웅얼거림. 누군가 말하고 있다는 것만 안다 — 내용·목소리는 모른다.</summary>
        Muffled = 1,

        /// <summary>같은 방. 대사 전문과 목소리가 보인다.</summary>
        Full = 2
    }

    /// <summary>청취점에 실제로 전달되는 발화 하나. Muffled면 내용이 비어 있다.</summary>
    public sealed class PerceivedUtterance
    {
        public Audibility Level;

        /// <summary>엔진 쪽 식별용(놓친 발화 표시 등). 플레이어에게 보여 줄 내용이 아니다.</summary>
        public string UtteranceId;

        /// <summary>소리가 나는 방. 웅얼거림도 "어느 문 너머인지"는 안다.</summary>
        public string Room;

        /// <summary>Full일 때만 채워진다. Muffled면 빈 문자열.</summary>
        public string VoiceId;

        /// <summary>Full일 때만 채워진다. Muffled면 빈 문자열.</summary>
        public string Text;
    }

    /// <summary>
    /// 방 단위 가청 판정. 같은 방 = Full, rooms.json의 문으로 바로 이어진 방 = Muffled, 그 밖 = None.
    /// 방 인접 정보는 RoomLayout의 doors에서만 가져온다(새 데이터 없음).
    /// </summary>
    public sealed class AudibilityModel
    {
        private readonly RoomLayout _layout;
        private readonly HashSet<string> _adjacentPairs = new HashSet<string>();

        public AudibilityModel(RoomLayout layout)
        {
            _layout = layout;
            if (layout == null) return;

            IList<DoorDefinition> doors = layout.Doors;
            for (int i = 0; i < doors.Count; i++)
            {
                DoorDefinition door = doors[i];
                if (string.IsNullOrEmpty(door.roomA) || string.IsNullOrEmpty(door.roomB)) continue;
                _adjacentPairs.Add(PairKey(door.roomA, door.roomB));
                _adjacentPairs.Add(PairKey(door.roomB, door.roomA));
            }
        }

        public RoomLayout Layout { get { return _layout; } }

        public Audibility Judge(string listenerRoom, string speakerRoom)
        {
            if (_layout == null) return Audibility.None;

            RoomDefinition ignored;
            if (!_layout.TryGetRoom(listenerRoom, out ignored)) return Audibility.None;
            if (!_layout.TryGetRoom(speakerRoom, out ignored)) return Audibility.None;

            if (listenerRoom == speakerRoom) return Audibility.Full;
            if (_adjacentPairs.Contains(PairKey(listenerRoom, speakerRoom))) return Audibility.Muffled;
            return Audibility.None;
        }

        /// <summary>timeMs 순간 listenerRoom에 전달되는 발화들. None인 발화는 빠진다.</summary>
        public List<PerceivedUtterance> Perceive(ScriptTimeline timeline, int timeMs, string listenerRoom)
        {
            var result = new List<PerceivedUtterance>();
            if (timeline == null) return result;

            List<Utterance> active = timeline.ActiveAt(timeMs);
            for (int i = 0; i < active.Count; i++)
            {
                Utterance u = active[i];
                Audibility level = Judge(listenerRoom, u.room);
                if (level == Audibility.None) continue;

                bool full = level == Audibility.Full;
                result.Add(new PerceivedUtterance
                {
                    Level = level,
                    UtteranceId = u.id,
                    Room = u.room,
                    VoiceId = full ? u.voiceId : string.Empty,
                    Text = full ? u.text : string.Empty
                });
            }
            return result;
        }

        private static string PairKey(string a, string b)
        {
            return a + "|" + b;
        }
    }
}
