using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 맵 레이아웃 로직 테스트. UnityEngine에 의존하지 않는 부분만 다루므로
    /// 씬을 열지 않고도 벽/출입구 계산이 맞는지 확인할 수 있다(§18-1).
    /// </summary>
    public class RoomLayoutTests
    {
        /// <summary>10×10 방 두 개가 x=10에서 맞닿고, 그 벽에 문이 하나 뚫린 최소 구성.</summary>
        private static RoomTable MakeTwoRoomTable()
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

        private static bool AnyWallCovers(List<WallSegment> walls, float x, float y)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                WallSegment wall = walls[i];
                float halfWidth = wall.Width * 0.5f;
                float halfHeight = wall.Height * 0.5f;

                if (x > wall.CenterX - halfWidth && x < wall.CenterX + halfWidth &&
                    y > wall.CenterY - halfHeight && y < wall.CenterY + halfHeight)
                {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void RoomCenter_IsRectangleCenter()
        {
            RoomLayout layout = RoomLayout.FromTable(MakeTwoRoomTable());

            float x, y;
            Assert.IsTrue(layout.TryGetRoomCenter("b", out x, out y));
            Assert.AreEqual(15f, x, 0.0001f);
            Assert.AreEqual(5f, y, 0.0001f);
        }

        [Test]
        public void SpawnPosition_UsesPlayerSpawnRoom()
        {
            RoomLayout layout = RoomLayout.FromTable(MakeTwoRoomTable());

            float x, y;
            Assert.IsTrue(layout.TryGetSpawnPosition(out x, out y));
            Assert.AreEqual(5f, x, 0.0001f);
            Assert.AreEqual(5f, y, 0.0001f);
        }

        [Test]
        public void UnknownRoom_IsNotFound()
        {
            RoomLayout layout = RoomLayout.FromTable(MakeTwoRoomTable());

            RoomDefinition room;
            Assert.IsFalse(layout.TryGetRoom("nope", out room));
            Assert.IsNull(room);
        }

        [Test]
        public void FindRoomAt_ReturnsContainingRoom()
        {
            RoomLayout layout = RoomLayout.FromTable(MakeTwoRoomTable());

            Assert.AreEqual("a", layout.FindRoomAt(1f, 1f).id);
            Assert.AreEqual("b", layout.FindRoomAt(19f, 9f).id);
            Assert.IsNull(layout.FindRoomAt(-1f, -1f));
        }

        [Test]
        public void Door_PunchesHoleInSharedWall()
        {
            RoomLayout layout = RoomLayout.FromTable(MakeTwoRoomTable());
            List<WallSegment> walls = layout.BuildAllWallSegments();

            Assert.IsFalse(AnyWallCovers(walls, 10f, 5f), "문 한가운데는 벽이 없어야 한다.");
            Assert.IsTrue(AnyWallCovers(walls, 10f, 8f), "문에서 벗어난 같은 벽은 막혀 있어야 한다.");
        }

        [Test]
        public void Walls_HaveNoDuplicateSegments()
        {
            RoomLayout layout = RoomLayout.FromTable(MakeTwoRoomTable());
            List<WallSegment> walls = layout.BuildAllWallSegments();

            var seen = new HashSet<string>();
            for (int i = 0; i < walls.Count; i++)
            {
                string key = string.Format("{0:0.###}|{1:0.###}|{2:0.###}|{3:0.###}",
                    walls[i].CenterX, walls[i].CenterY, walls[i].Width, walls[i].Height);
                Assert.IsTrue(seen.Add(key), "같은 벽 조각이 두 번 생성됐다: " + key);
            }
        }

        [Test]
        public void OuterWalls_EncloseTheMap()
        {
            RoomLayout layout = RoomLayout.FromTable(MakeTwoRoomTable());
            List<WallSegment> walls = layout.BuildAllWallSegments();

            Assert.IsTrue(AnyWallCovers(walls, 5f, 0f), "아래쪽 바깥 벽");
            Assert.IsTrue(AnyWallCovers(walls, 5f, 10f), "위쪽 바깥 벽");
            Assert.IsTrue(AnyWallCovers(walls, 0f, 5f), "왼쪽 바깥 벽");
            Assert.IsTrue(AnyWallCovers(walls, 20f, 5f), "오른쪽 바깥 벽");
        }

        [Test]
        public void EmptyTable_ProducesNoWalls()
        {
            RoomLayout layout = RoomLayout.FromTable(new RoomTable());

            Assert.AreEqual(0, layout.RoomCount);
            Assert.AreEqual(0, layout.BuildAllWallSegments().Count);
        }

        [Test]
        public void Interval_SubtractSplitsAndClears()
        {
            var spans = new List<Interval> { new Interval(0f, 10f) };

            List<Interval> split = Interval.Subtract(spans, 4f, 6f);
            Assert.AreEqual(2, split.Count);
            Assert.AreEqual(4f, split[0].Max, 0.0001f);
            Assert.AreEqual(6f, split[1].Min, 0.0001f);

            Assert.AreEqual(0, Interval.Subtract(spans, -1f, 11f).Count);
            Assert.AreEqual(1, Interval.Subtract(spans, 20f, 30f).Count);
        }

        [Test]
        public void Interval_MergeJoinsTouchingSpans()
        {
            List<Interval> merged = Interval.Merge(new List<Interval>
            {
                new Interval(5f, 10f),
                new Interval(0f, 5f),
                new Interval(20f, 25f)
            });

            Assert.AreEqual(2, merged.Count);
            Assert.AreEqual(0f, merged[0].Min, 0.0001f);
            Assert.AreEqual(10f, merged[0].Max, 0.0001f);
        }
    }
}
