using Detective.Core;
using Detective.Data;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 잘못된 rooms.json이 조용히 통과하지 않는지 확인한다.
    /// 검증기가 아무것도 못 잡으면 데이터 오류를 사람이 Play로만 발견하게 된다.
    /// </summary>
    public class RoomLayoutValidatorTests
    {
        private static RoomTable MakeValidTable()
        {
            return new RoomTable
            {
                rooms = new[]
                {
                    new RoomDefinition { id = "a", displayName = "A", x = 0f, y = 0f, width = 10f, height = 10f },
                    new RoomDefinition { id = "b", displayName = "B", x = 10f, y = 0f, width = 10f, height = 10f }
                },
                doors = new[]
                {
                    new DoorDefinition { id = "d", roomA = "a", roomB = "b", x = 9.7f, y = 4f, width = 0.6f, height = 2f }
                },
                playerSpawnRoom = "a"
            };
        }

        [Test]
        public void ValidTable_HasNoErrors()
        {
            Assert.AreEqual(0, RoomLayoutValidator.Validate(MakeValidTable()).Count);
        }

        [Test]
        public void NullTable_IsReported()
        {
            Assert.GreaterOrEqual(RoomLayoutValidator.Validate(null).Count, 1);
        }

        [Test]
        public void OverlappingRooms_AreReported()
        {
            RoomTable table = MakeValidTable();
            table.rooms[1].x = 5f;

            Assert.GreaterOrEqual(RoomLayoutValidator.Validate(table).Count, 1);
        }

        [Test]
        public void DuplicateRoomId_IsReported()
        {
            RoomTable table = MakeValidTable();
            table.rooms[1].id = "a";

            Assert.GreaterOrEqual(RoomLayoutValidator.Validate(table).Count, 1);
        }

        [Test]
        public void DoorPointingAtMissingRoom_IsReported()
        {
            RoomTable table = MakeValidTable();
            table.doors[0].roomB = "missing";

            Assert.GreaterOrEqual(RoomLayoutValidator.Validate(table).Count, 1);
        }

        [Test]
        public void DoorThinnerThanWall_IsReported()
        {
            RoomTable table = MakeValidTable();
            table.doors[0].width = 0.1f; // 벽 두께 0.4보다 얇으면 구멍이 뚫리지 않는다.

            Assert.GreaterOrEqual(RoomLayoutValidator.Validate(table).Count, 1);
        }

        [Test]
        public void DoorNotOnAnyWall_IsReported()
        {
            RoomTable table = MakeValidTable();
            table.doors[0].x = 3f;
            table.doors[0].y = 3f;

            Assert.GreaterOrEqual(RoomLayoutValidator.Validate(table).Count, 1);
        }

        [Test]
        public void UnknownSpawnRoom_IsReported()
        {
            RoomTable table = MakeValidTable();
            table.playerSpawnRoom = "nowhere";

            Assert.GreaterOrEqual(RoomLayoutValidator.Validate(table).Count, 1);
        }
    }
}
