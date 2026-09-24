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

        /// <summary>모든 방을 감싸는 사각형. 방이 없으면 false.</summary>
        public bool TryGetBounds(out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = maxX = maxY = 0f;
            if (_rooms.Count == 0) return false;

            minX = _rooms[0].MinX; minY = _rooms[0].MinY;
            maxX = _rooms[0].MaxX; maxY = _rooms[0].MaxY;
            for (int i = 1; i < _rooms.Count; i++)
            {
                if (_rooms[i].MinX < minX) minX = _rooms[i].MinX;
                if (_rooms[i].MinY < minY) minY = _rooms[i].MinY;
                if (_rooms[i].MaxX > maxX) maxX = _rooms[i].MaxX;
                if (_rooms[i].MaxY > maxY) maxY = _rooms[i].MaxY;
            }
            return true;
        }

        public string DisplayNameOf(string roomId)
        {
            RoomDefinition room;
            return TryGetRoom(roomId, out room) ? room.displayName : roomId;
        }

        /// <summary>플레이어 시작 좌표(시작 방의 중심).</summary>
        public bool TryGetSpawnPosition(out float x, out float y)
        {
            return TryGetRoomCenter(_spawnRoomId, out x, out y);
        }

        /// <summary>
        /// from 방에서 to 방까지 지나야 하는 문 목록(너비 우선 탐색 → 최소 문 개수).
        /// 같은 방이면 빈 목록, 이어지지 않으면 null.
        /// </summary>
        public List<DoorDefinition> FindDoorPath(string fromRoomId, string toRoomId)
        {
            if (!_roomsById.ContainsKey(fromRoomId ?? string.Empty)) return null;
            if (!_roomsById.ContainsKey(toRoomId ?? string.Empty)) return null;
            if (fromRoomId == toRoomId) return new List<DoorDefinition>();

            var cameThrough = new Dictionary<string, DoorDefinition>();
            var previousRoom = new Dictionary<string, string>();
            var visited = new HashSet<string> { fromRoomId };
            var queue = new Queue<string>();
            queue.Enqueue(fromRoomId);

            while (queue.Count > 0)
            {
                string room = queue.Dequeue();
                if (room == toRoomId) break;

                for (int i = 0; i < _doors.Count; i++)
                {
                    DoorDefinition door = _doors[i];
                    string next = door.roomA == room ? door.roomB : door.roomB == room ? door.roomA : null;
                    if (next == null || !visited.Add(next)) continue;

                    cameThrough[next] = door;
                    previousRoom[next] = room;
                    queue.Enqueue(next);
                }
            }

            if (!visited.Contains(toRoomId)) return null;

            var path = new List<DoorDefinition>();
            for (string room = toRoomId; room != fromRoomId; room = previousRoom[room])
            {
                path.Add(cameThrough[room]);
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// NPC 이동 경로: 지나야 할 문 중심을 차례로 거쳐 목적지 좌표에 도착한다.
        /// 벽을 뚫고 가로지르지 않도록 문을 경유하는 것 외에 길찾기는 하지 않는다(§8).
        /// 방을 모르거나 이어지지 않으면 목적지로 곧장 간다.
        /// </summary>
        public List<Point2> BuildRoute(string fromRoomId, string toRoomId, float targetX, float targetY)
        {
            var route = new List<Point2>();
            List<DoorDefinition> doors = FindDoorPath(fromRoomId, toRoomId);
            if (doors != null)
            {
                for (int i = 0; i < doors.Count; i++) route.Add(new Point2(doors[i].CenterX, doors[i].CenterY));
            }
            route.Add(new Point2(targetX, targetY));
            return route;
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
