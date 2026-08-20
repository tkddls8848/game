using System.Collections.Generic;
using System.Globalization;
using Detective.Data;

namespace Detective.Core
{
    /// <summary>
    /// rooms.json 무결성 검사(순수 C#).
    /// 씬 빌더가 씬을 만들기 전에 먼저 돌린다 — 잘못된 데이터로 만들어진 씬은
    /// 사람이 Play를 눌러야만 이상을 알아챌 수 있는데, 그건 이 프로젝트가 피하려는 상황이다.
    /// </summary>
    public static class RoomLayoutValidator
    {
        /// <summary>방끼리 이만큼 겹쳐도 오차로 본다.</summary>
        private const float OverlapEpsilon = 0.001f;

        /// <summary>문제 목록을 돌려준다. 비어 있으면 통과.</summary>
        public static List<string> Validate(RoomTable table)
        {
            var errors = new List<string>();
            if (table == null)
            {
                errors.Add("rooms.json을 읽지 못했다 (table == null).");
                return errors;
            }
            table.Normalized();

            if (table.rooms.Length == 0) errors.Add("방이 하나도 없다.");

            var roomIds = new HashSet<string>();
            for (int i = 0; i < table.rooms.Length; i++)
            {
                RoomDefinition room = table.rooms[i];
                string label = string.IsNullOrEmpty(room.id) ? "rooms[" + i + "]" : room.id;

                if (string.IsNullOrEmpty(room.id)) errors.Add(label + ": id가 비어 있다.");
                else if (!roomIds.Add(room.id)) errors.Add(label + ": id가 중복된다.");

                if (string.IsNullOrEmpty(room.displayName)) errors.Add(label + ": displayName이 비어 있다.");
                if (room.width <= 0f || room.height <= 0f) errors.Add(label + ": width/height가 0 이하다.");
            }

            for (int i = 0; i < table.rooms.Length; i++)
            {
                for (int j = i + 1; j < table.rooms.Length; j++)
                {
                    if (!Overlaps(table.rooms[i], table.rooms[j])) continue;
                    errors.Add(string.Format("{0} 와 {1} 의 영역이 겹친다.", table.rooms[i].id, table.rooms[j].id));
                }
            }

            var doorIds = new HashSet<string>();
            for (int i = 0; i < table.doors.Length; i++)
            {
                DoorDefinition door = table.doors[i];
                string label = string.IsNullOrEmpty(door.id) ? "doors[" + i + "]" : door.id;

                if (string.IsNullOrEmpty(door.id)) errors.Add(label + ": id가 비어 있다.");
                else if (!doorIds.Add(door.id)) errors.Add(label + ": id가 중복된다.");

                if (door.width <= 0f || door.height <= 0f) errors.Add(label + ": width/height가 0 이하다.");

                // 벽보다 얇은 문은 벽을 관통하지 못해서 구멍이 뚫리지 않는다.
                float thin = door.width < door.height ? door.width : door.height;
                if (thin <= RoomLayout.WallThickness)
                {
                    errors.Add(string.Format(
                        "{0}: 짧은 변이 {1}(월드 유닛)이라 벽 두께 {2}를 관통하지 못한다.",
                        label,
                        thin.ToString("0.###", CultureInfo.InvariantCulture),
                        RoomLayout.WallThickness.ToString("0.###", CultureInfo.InvariantCulture)));
                }

                if (!string.IsNullOrEmpty(door.roomA) && !roomIds.Contains(door.roomA))
                    errors.Add(label + ": roomA '" + door.roomA + "' 가 없는 방이다.");
                if (!string.IsNullOrEmpty(door.roomB) && !roomIds.Contains(door.roomB))
                    errors.Add(label + ": roomB '" + door.roomB + "' 가 없는 방이다.");
                if (string.IsNullOrEmpty(door.roomA) || string.IsNullOrEmpty(door.roomB))
                    errors.Add(label + ": roomA/roomB가 비어 있다.");
                else if (door.roomA == door.roomB)
                    errors.Add(label + ": roomA와 roomB가 같은 방이다.");
                else
                    ValidateDoorTouchesRooms(table, door, label, errors);
            }

            if (!string.IsNullOrEmpty(table.playerSpawnRoom) && !roomIds.Contains(table.playerSpawnRoom))
                errors.Add("playerSpawnRoom '" + table.playerSpawnRoom + "' 가 없는 방이다.");

            return errors;
        }

        private static void ValidateDoorTouchesRooms(RoomTable table, DoorDefinition door, string label, List<string> errors)
        {
            RoomDefinition a = Find(table, door.roomA);
            RoomDefinition b = Find(table, door.roomB);
            if (a == null || b == null) return;

            if (!TouchesRoom(door, a)) errors.Add(label + ": " + door.roomA + " 의 벽에 걸쳐 있지 않다.");
            if (!TouchesRoom(door, b)) errors.Add(label + ": " + door.roomB + " 의 벽에 걸쳐 있지 않다.");
        }

        private static RoomDefinition Find(RoomTable table, string id)
        {
            for (int i = 0; i < table.rooms.Length; i++)
            {
                if (table.rooms[i].id == id) return table.rooms[i];
            }
            return null;
        }

        /// <summary>문 사각형이 방의 테두리(벽 두께 범위)와 겹치는가.</summary>
        private static bool TouchesRoom(DoorDefinition door, RoomDefinition room)
        {
            float half = RoomLayout.WallThickness * 0.5f;

            bool xOverlapsRoom = door.MaxX > room.MinX - half && door.MinX < room.MaxX + half;
            bool yOverlapsRoom = door.MaxY > room.MinY - half && door.MinY < room.MaxY + half;
            if (!xOverlapsRoom || !yOverlapsRoom) return false;

            bool onHorizontalWall =
                (door.MinY < room.MinY + half && door.MaxY > room.MinY - half) ||
                (door.MinY < room.MaxY + half && door.MaxY > room.MaxY - half);
            bool onVerticalWall =
                (door.MinX < room.MinX + half && door.MaxX > room.MinX - half) ||
                (door.MinX < room.MaxX + half && door.MaxX > room.MaxX - half);

            return onHorizontalWall || onVerticalWall;
        }

        private static bool Overlaps(RoomDefinition a, RoomDefinition b)
        {
            return a.MinX < b.MaxX - OverlapEpsilon && b.MinX < a.MaxX - OverlapEpsilon
                && a.MinY < b.MaxY - OverlapEpsilon && b.MinY < a.MaxY - OverlapEpsilon;
        }
    }
}
