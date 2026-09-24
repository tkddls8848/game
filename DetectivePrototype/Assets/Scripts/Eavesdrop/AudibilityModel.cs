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

        /// <summary>벽 너머 웅얼거림. 누군가 말하고 있다는 것만 안다 — 내용·목소리는 모른다.</summary>
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
    /// 방 단위 가청 판정. 같은 방 = Full, 벽을 맞댄 방 = Muffled, 그 밖 = None.
    ///
    /// 인접은 문이 아니라 **벽 맞닿음**으로 본다. 소리는 문으로만 새지 않는다.
    /// 문만 보면 이 저택은 복도를 중심으로 한 별 모양이 되어(문 5개가 전부 한쪽이 복도),
    /// 복도에 서면 다섯 방이 전부 들리고 복도가 아닌 두 방은 서로 영원히 무음이 된다.
    /// 벽 하나를 사이에 둔 로비와 식당이 서로 안 들리는 것은 물리적으로도 어색하고,
    /// "옆방에서 누가 말하는데 내용을 모르겠다 → 가 보자"는 엿듣기의 동력도 복도에서만 작동하게 된다.
    ///
    /// 새 데이터는 필요 없다. 방이 축 정렬 사각형이므로 rooms.json의 좌표만으로 계산된다.
    /// </summary>
    public sealed class AudibilityModel
    {
        /// <summary>좌표 비교 허용 오차. 방 좌표는 정수 단위로 적히므로 이보다 훨씬 크게 떨어져 있다.</summary>
        private const float Epsilon = 0.001f;

        private readonly RoomLayout _layout;
        private readonly HashSet<string> _adjacentPairs = new HashSet<string>();

        public AudibilityModel(RoomLayout layout)
        {
            _layout = layout;
            if (layout == null) return;

            // 문으로 이어진 방은 당연히 들린다. 문이 벽 밖에 적혀 있어도(별도 좌표) 놓치지 않는다.
            IList<DoorDefinition> doors = layout.Doors;
            for (int i = 0; i < doors.Count; i++)
            {
                DoorDefinition door = doors[i];
                if (string.IsNullOrEmpty(door.roomA) || string.IsNullOrEmpty(door.roomB)) continue;
                AddPair(door.roomA, door.roomB);
            }

            // 벽을 맞댄 방도 들린다. 문이 없어도 벽 너머로 새는 소리다.
            IList<RoomDefinition> rooms = layout.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    if (SharesWall(rooms[i], rooms[j])) AddPair(rooms[i].id, rooms[j].id);
                }
            }
        }

        /// <summary>두 방이 벽 한 장을 맞대고 있는가. 한 변이 겹쳐 닿고 그 변을 따라 실제로 겹치는 구간이 있어야 한다.</summary>
        private static bool SharesWall(RoomDefinition a, RoomDefinition b)
        {
            if (a == null || b == null) return false;

            bool touchesVertically = Touches(a.MaxX, b.MinX) || Touches(b.MaxX, a.MinX);
            if (touchesVertically && Overlap(a.MinY, a.MaxY, b.MinY, b.MaxY) > Epsilon) return true;

            bool touchesHorizontally = Touches(a.MaxY, b.MinY) || Touches(b.MaxY, a.MinY);
            if (touchesHorizontally && Overlap(a.MinX, a.MaxX, b.MinX, b.MaxX) > Epsilon) return true;

            return false;
        }

        /// <summary>모서리만 스치는 경우를 벽으로 세지 않도록 겹친 길이를 돌려준다.</summary>
        private static float Overlap(float aMin, float aMax, float bMin, float bMax)
        {
            float low = aMin > bMin ? aMin : bMin;
            float high = aMax < bMax ? aMax : bMax;
            return high - low;
        }

        private static bool Touches(float p, float q)
        {
            float d = p - q;
            return (d < 0 ? -d : d) <= Epsilon;
        }

        private void AddPair(string a, string b)
        {
            _adjacentPairs.Add(PairKey(a, b));
            _adjacentPairs.Add(PairKey(b, a));
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
