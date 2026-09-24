using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>NPC 이동 경로: 벽을 뚫지 않고 문을 차례로 거친다.</summary>
    public class RouteTests
    {
        /// <summary>a | hall | b 가 가로로 붙어 있고 a-hall, hall-b 사이에만 문이 있다. c는 어디와도 이어지지 않는다.</summary>
        private static RoomLayout MakeLayout()
        {
            return RoomLayout.FromTable(new RoomTable
            {
                rooms = new[]
                {
                    new RoomDefinition { id = "a", displayName = "A", x = 0f, y = 0f, width = 10f, height = 10f },
                    new RoomDefinition { id = "hall", displayName = "Hall", x = 10f, y = 0f, width = 4f, height = 10f },
                    new RoomDefinition { id = "b", displayName = "B", x = 14f, y = 0f, width = 10f, height = 10f },
                    new RoomDefinition { id = "c", displayName = "C", x = 0f, y = 20f, width = 5f, height = 5f }
                },
                doors = new[]
                {
                    new DoorDefinition { id = "d1", roomA = "a", roomB = "hall", x = 9.7f, y = 4f, width = 0.6f, height = 2f },
                    new DoorDefinition { id = "d2", roomA = "hall", roomB = "b", x = 13.7f, y = 4f, width = 0.6f, height = 2f }
                }
            });
        }

        [Test]
        public void FindDoorPath_SameRoom_IsEmpty()
        {
            Assert.AreEqual(0, MakeLayout().FindDoorPath("a", "a").Count);
        }

        [Test]
        public void FindDoorPath_ThroughHall_UsesBothDoorsInOrder()
        {
            List<DoorDefinition> path = MakeLayout().FindDoorPath("a", "b");
            Assert.AreEqual(2, path.Count);
            Assert.AreEqual("d1", path[0].id);
            Assert.AreEqual("d2", path[1].id);

            List<DoorDefinition> back = MakeLayout().FindDoorPath("b", "a");
            Assert.AreEqual("d2", back[0].id);
            Assert.AreEqual("d1", back[1].id);
        }

        [Test]
        public void FindDoorPath_Unreachable_IsNull()
        {
            Assert.IsNull(MakeLayout().FindDoorPath("a", "c"));
            Assert.IsNull(MakeLayout().FindDoorPath("a", "nowhere"));
        }

        [Test]
        public void BuildRoute_EndsAtTargetAfterDoorCenters()
        {
            List<Point2> route = MakeLayout().BuildRoute("a", "b", 19f, 5f);
            Assert.AreEqual(3, route.Count);
            Assert.AreEqual(10f, route[0].X, 0.001f);
            Assert.AreEqual(5f, route[0].Y, 0.001f);
            Assert.AreEqual(14f, route[1].X, 0.001f);
            Assert.AreEqual(19f, route[2].X, 0.001f);
        }

        [Test]
        public void BuildRoute_Unreachable_GoesStraightToTarget()
        {
            List<Point2> route = MakeLayout().BuildRoute("a", "c", 2f, 22f);
            Assert.AreEqual(1, route.Count);
        }

        [Test]
        public void TryGetBounds_CoversAllRooms()
        {
            float minX, minY, maxX, maxY;
            Assert.IsTrue(MakeLayout().TryGetBounds(out minX, out minY, out maxX, out maxY));
            Assert.AreEqual(0f, minX);
            Assert.AreEqual(0f, minY);
            Assert.AreEqual(24f, maxX);
            Assert.AreEqual(25f, maxY);
        }
    }
}
