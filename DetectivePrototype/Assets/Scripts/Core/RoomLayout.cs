using System.Collections.Generic;
using System.Globalization;
using Detective.Data;

namespace Detective.Core
{
    /// <summary>
    /// rooms.json을 게임이 쓰는 형태로 바꿔 주는 순수 C# 레이어.
    /// 방 조회, 중심점 계산, 출입구가 뚫린 벽 조각 생성까지 전부 여기서 한다.
    /// UnityEngine을 참조하지 않으므로 EditMode 테스트로 전부 검증할 수 있다(§18-1).
    /// </summary>
    public sealed class RoomLayout
    {
        /// <summary>벽 두께(월드 유닛). 출입구는 이보다 두꺼워야 벽을 관통한다.</summary>
        public const float WallThickness = 0.4f;

        /// <summary>이보다 짧은 벽 조각은 버린다(부동소수 찌꺼기 제거).</summary>
        public const float MinSegmentLength = 0.01f;

        private readonly List<RoomDefinition> _rooms;
        private readonly List<DoorDefinition> _doors;
        private readonly Dictionary<string, RoomDefinition> _roomsById;
        private readonly string _spawnRoomId;

        private RoomLayout(List<RoomDefinition> rooms, List<DoorDefinition> doors, string spawnRoomId)
        {
            _rooms = rooms;
            _doors = doors;
            _roomsById = new Dictionary<string, RoomDefinition>();
            for (int i = 0; i < rooms.Count; i++)
            {
                if (string.IsNullOrEmpty(rooms[i].id)) continue;
                _roomsById[rooms[i].id] = rooms[i];
            }
            _spawnRoomId = spawnRoomId;
        }

        public static RoomLayout FromTable(RoomTable table)
        {
            if (table == null) table = new RoomTable();
            table.Normalized();

            var rooms = new List<RoomDefinition>(table.rooms);
            var doors = new List<DoorDefinition>(table.doors);

            string spawn = table.playerSpawnRoom;
            if (string.IsNullOrEmpty(spawn) && rooms.Count > 0) spawn = rooms[0].id;

            return new RoomLayout(rooms, doors, spawn);
        }

        public IList<RoomDefinition> Rooms { get { return _rooms.AsReadOnly(); } }
        public IList<DoorDefinition> Doors { get { return _doors.AsReadOnly(); } }
        public int RoomCount { get { return _rooms.Count; } }
        public string SpawnRoomId { get { return _spawnRoomId; } }

        public bool TryGetRoom(string roomId, out RoomDefinition room)
        {
            room = null;
            if (string.IsNullOrEmpty(roomId)) return false;
            return _roomsById.TryGetValue(roomId, out room);
        }

        /// <summary>NPC 이동 목적지 = 방 중심점. 없는 방이면 false.</summary>
        public bool TryGetRoomCenter(string roomId, out float centerX, out float centerY)
        {
            centerX = 0f;
            centerY = 0f;
            RoomDefinition room;
            if (!TryGetRoom(roomId, out room)) return false;
            centerX = room.CenterX;
            centerY = room.CenterY;
            return true;
        }

        /// <summary>좌표가 속한 방. 어느 방에도 없으면 null(방 경계가 겹치면 먼저 정의된 방).</summary>
        public RoomDefinition FindRoomAt(float x, float y)
        {
            for (int i = 0; i < _rooms.Count; i++)
            {
                if (_rooms[i].Contains(x, y)) return _rooms[i];
            }
            return null;
        }

        /// <summary>플레이어 시작 좌표(시작 방의 중심).</summary>
        public bool TryGetSpawnPosition(out float x, out float y)
        {
            return TryGetRoomCenter(_spawnRoomId, out x, out y);
        }

        /// <summary>방 하나의 벽 조각. 출입구와 겹치는 부분은 잘려 나간다.</summary>
        public List<WallSegment> BuildWallSegments(RoomDefinition room)
        {
            var segments = new List<WallSegment>();
            if (room == null) return segments;

            float half = WallThickness * 0.5f;

            // 가로 벽(아래/위): 모서리를 메우려고 양끝을 벽 두께의 절반만큼 늘린다.
            AppendHorizontalWall(segments, room.MinY, room.MinX - half, room.MaxX + half);
            AppendHorizontalWall(segments, room.MaxY, room.MinX - half, room.MaxX + half);

            // 세로 벽(왼/오): 가로 벽이 모서리를 이미 덮으므로 안쪽 구간만 만든다.
            AppendVerticalWall(segments, room.MinX, room.MinY + half, room.MaxY - half);
            AppendVerticalWall(segments, room.MaxX, room.MinY + half, room.MaxY - half);

            return segments;
        }

        /// <summary>
        /// 맵 전체의 벽 조각. 맞닿은 방이 같은 선 위에 겹쳐 만든 벽은 하나로 합친다.
        /// 출입구로 뚫린 구멍은 어느 방에서 만든 벽이든 똑같이 잘려 있으므로 병합해도 다시 막히지 않는다.
        /// </summary>
        public List<WallSegment> BuildAllWallSegments()
        {
            // 같은 직선 위에 놓인 벽끼리 모은다. key = "H|y" 또는 "V|x"
            var lines = new List<string>();
            var spansByLine = new Dictionary<string, List<Interval>>();
            var coordByLine = new Dictionary<string, float>();

            for (int i = 0; i < _rooms.Count; i++)
            {
                List<WallSegment> segments = BuildWallSegments(_rooms[i]);
                for (int s = 0; s < segments.Count; s++)
                {
                    WallSegment seg = segments[s];
                    bool horizontal = seg.Width >= seg.Height;
                    float lineCoord = horizontal ? seg.CenterY : seg.CenterX;
                    string key = (horizontal ? "H|" : "V|")
                        + lineCoord.ToString("0.###", CultureInfo.InvariantCulture);

                    List<Interval> spans;
                    if (!spansByLine.TryGetValue(key, out spans))
                    {
                        spans = new List<Interval>();
                        spansByLine[key] = spans;
                        coordByLine[key] = lineCoord;
                        lines.Add(key);
                    }

                    float half = (horizontal ? seg.Width : seg.Height) * 0.5f;
                    float center = horizontal ? seg.CenterX : seg.CenterY;
                    spans.Add(new Interval(center - half, center + half));
                }
            }

            var result = new List<WallSegment>();
            for (int i = 0; i < lines.Count; i++)
            {
                string key = lines[i];
                bool horizontal = key[0] == 'H';
                float lineCoord = coordByLine[key];

                List<Interval> merged = Interval.Merge(spansByLine[key]);
                for (int m = 0; m < merged.Count; m++)
                {
                    if (merged[m].Length < MinSegmentLength) continue;
                    float center = (merged[m].Min + merged[m].Max) * 0.5f;
                    result.Add(horizontal
                        ? new WallSegment(center, lineCoord, merged[m].Length, WallThickness)
                        : new WallSegment(lineCoord, center, WallThickness, merged[m].Length));
                }
            }
            return result;
        }

        private void AppendHorizontalWall(List<WallSegment> target, float wallY, float spanMin, float spanMax)
        {
            float half = WallThickness * 0.5f;
            var spans = new List<Interval> { new Interval(spanMin, spanMax) };

            for (int i = 0; i < _doors.Count; i++)
            {
                DoorDefinition door = _doors[i];
                bool crossesWall = door.MinY < wallY + half && door.MaxY > wallY - half;
                if (!crossesWall) continue;
                spans = Interval.Subtract(spans, door.MinX, door.MaxX);
            }

            for (int i = 0; i < spans.Count; i++)
            {
                if (spans[i].Length < MinSegmentLength) continue;
                float cx = (spans[i].Min + spans[i].Max) * 0.5f;
                target.Add(new WallSegment(cx, wallY, spans[i].Length, WallThickness));
            }
        }

        private void AppendVerticalWall(List<WallSegment> target, float wallX, float spanMin, float spanMax)
        {
            float half = WallThickness * 0.5f;
            var spans = new List<Interval> { new Interval(spanMin, spanMax) };

            for (int i = 0; i < _doors.Count; i++)
            {
                DoorDefinition door = _doors[i];
                bool crossesWall = door.MinX < wallX + half && door.MaxX > wallX - half;
                if (!crossesWall) continue;
                spans = Interval.Subtract(spans, door.MinY, door.MaxY);
            }

            for (int i = 0; i < spans.Count; i++)
            {
                if (spans[i].Length < MinSegmentLength) continue;
                float cy = (spans[i].Min + spans[i].Max) * 0.5f;
                target.Add(new WallSegment(wallX, cy, WallThickness, spans[i].Length));
            }
        }
    }
}
