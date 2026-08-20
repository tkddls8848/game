using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 실제로 배포되는 rooms.json을 검사한다.
    /// 로직이 아니라 데이터가 깨졌을 때도 테스트가 빨간불이 되도록 하는 회귀 방어선이다.
    /// </summary>
    public class GameDataTests
    {
        [Test]
        public void RoomsJson_LoadsAndPassesValidation()
        {
            RoomTable table = GameDataLoader.LoadRoomTable();

            Assert.Greater(table.rooms.Length, 0, "rooms.json에서 방을 하나도 읽지 못했다.");

            List<string> errors = RoomLayoutValidator.Validate(table);
            Assert.AreEqual(0, errors.Count, string.Join("\n", errors.ToArray()));
        }

        [Test]
        public void RoomsJson_HasSixPrototypeRooms()
        {
            RoomTable table = GameDataLoader.LoadRoomTable();

            // §5: 로비 / 식당 / 복도 / 피해자 방 / 용의자 방 / 창고
            Assert.AreEqual(6, table.rooms.Length);
        }

        [Test]
        public void RoomsJson_EveryDoorConnectsTwoDifferentRooms()
        {
            RoomTable table = GameDataLoader.LoadRoomTable();
            RoomLayout layout = RoomLayout.FromTable(table);

            for (int i = 0; i < layout.Doors.Count; i++)
            {
                DoorDefinition door = layout.Doors[i];
                RoomDefinition room;

                Assert.IsTrue(layout.TryGetRoom(door.roomA, out room), door.id + ": roomA가 없다.");
                Assert.IsTrue(layout.TryGetRoom(door.roomB, out room), door.id + ": roomB가 없다.");
                Assert.AreNotEqual(door.roomA, door.roomB, door.id + ": 같은 방을 잇고 있다.");
            }
        }

        [Test]
        public void RoomsJson_AllRoomsAreReachableFromSpawn()
        {
            RoomTable table = GameDataLoader.LoadRoomTable();
            RoomLayout layout = RoomLayout.FromTable(table);

            var reachable = new HashSet<string> { layout.SpawnRoomId };
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < layout.Doors.Count; i++)
                {
                    DoorDefinition door = layout.Doors[i];
                    if (reachable.Contains(door.roomA) && reachable.Add(door.roomB)) grew = true;
                    if (reachable.Contains(door.roomB) && reachable.Add(door.roomA)) grew = true;
                }
            }

            Assert.AreEqual(layout.RoomCount, reachable.Count,
                "시작 방에서 문으로 갈 수 없는 방이 있다.");
        }
    }
}
