using Detective.Core;
using Detective.Data;
using Detective.NPC;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>스케줄 조회와 "같은 시각·같은 방" 질의. 목격 판정의 바탕이다. 시각은 ms로 묻는다.</summary>
    public class ScheduleTests
    {
        /// <summary>그 10분 칸이 시작하는 ms. 스케줄 데이터가 아직 틱 배열이라 테스트도 칸 단위로 적는다.</summary>
        private static int At(int tick) { return GameTime.TickToMs(tick); }

        private static NpcDefinition MakeLiar()
        {
            return new NpcDefinition
            {
                id = "liar",
                displayName = "거짓말쟁이",
                schedule = new[] { "a", "b", "c", "c", "b", "a", "a" },
                claims = new[] { "", "a", "a", "", "", "", "" }
            }.Normalized();
        }

        [Test]
        public void RoomAt_ReturnsScheduledRoom()
        {
            NpcDefinition npc = MakeLiar();
            Assert.AreEqual("a", NpcSchedule.RoomAt(npc, At(0)));
            Assert.AreEqual("c", NpcSchedule.RoomAt(npc, At(3)));
        }

        [Test]
        public void RoomAt_MidSlotMs_ReadsTheSlotItFallsIn()
        {
            NpcDefinition npc = MakeLiar();
            Assert.AreEqual("c", NpcSchedule.RoomAt(npc, At(3) + 5 * GameTime.MsPerMinute), "18:35는 18:30 칸");
            Assert.AreEqual("c", NpcSchedule.RoomAt(npc, At(4) - 1), "18:39:59.999도 18:30 칸");
            Assert.AreEqual("b", NpcSchedule.RoomAt(npc, At(4)), "18:40부터 다음 칸");
            Assert.IsFalse(NpcSchedule.IsHonestAt(npc, At(1) + 1), "거짓말 칸 안의 어느 ms든 거짓말");
        }

        [Test]
        public void RoomAt_OutOfRange_IsEmpty()
        {
            NpcDefinition npc = MakeLiar();
            Assert.AreEqual(string.Empty, NpcSchedule.RoomAt(npc, -1), "18:00 이전");
            Assert.AreEqual(string.Empty, NpcSchedule.RoomAt(npc, GameTime.EndMs + 1), "19:00 이후");
            Assert.AreEqual(string.Empty, NpcSchedule.RoomAt(null, At(0)));
        }

        [Test]
        public void ClaimedRoomAt_UsesClaimOnlyWhereGiven()
        {
            NpcDefinition npc = MakeLiar();
            Assert.AreEqual("a", NpcSchedule.ClaimedRoomAt(npc, At(1)), "거짓 주장");
            Assert.AreEqual("c", NpcSchedule.ClaimedRoomAt(npc, At(3)), "주장이 없으면 사실대로");
        }

        [Test]
        public void IsHonestAt_DetectsLies()
        {
            NpcDefinition npc = MakeLiar();
            Assert.IsTrue(NpcSchedule.IsHonestAt(npc, At(0)));
            Assert.IsFalse(NpcSchedule.IsHonestAt(npc, At(1)));
            Assert.IsFalse(NpcSchedule.IsHonestAt(npc, At(2)));
            Assert.IsTrue(NpcSchedule.IsHonestAt(npc, At(3)));
        }

        [Test]
        public void ClaimsMatchingTruth_CountAsHonest()
        {
            var npc = new NpcDefinition { id = "x", schedule = new[] { "a", "b", "a", "a", "a", "a", "a" }, claims = new[] { "a", "b", "", "", "", "", "" } }.Normalized();
            for (int t = 0; t < 7; t++) Assert.IsTrue(NpcSchedule.IsHonestAt(npc, At(t)), "t=" + t);
        }

        [Test]
        public void LastKnownRoom_SkipsEmptyTicks()
        {
            var victim = new NpcDefinition { id = "v", isVictim = true, schedule = new[] { "study", "study", "study", "study", "", "", "" } }.Normalized();
            Assert.AreEqual("study", NpcSchedule.LastKnownRoom(victim, At(6)));
            Assert.AreEqual(string.Empty, NpcSchedule.RoomAt(victim, At(6)));
        }

        [Test]
        public void Roster_OccupantsAt_FindsEveryoneInRoom()
        {
            var roster = new NpcRoster(new[]
            {
                new NpcDefinition { id = "n2", schedule = new[] { "a", "a", "a", "a", "a", "a", "a" } },
                new NpcDefinition { id = "n1", schedule = new[] { "a", "b", "b", "b", "b", "b", "b" } },
                new NpcDefinition { id = "v", isVictim = true, schedule = new[] { "b", "b", "", "", "", "", "" } }
            });

            Assert.AreEqual(2, roster.OccupantsAt(At(0), "a").Count);
            Assert.AreEqual(2, roster.OccupantsAt(At(1), "b").Count);
            Assert.AreEqual(1, roster.OccupantsAt(At(2), "b").Count, "사망 이후 피해자는 어디에도 없다");
            Assert.AreEqual(0, roster.OccupantsAt(At(0), "").Count);
        }

        [Test]
        public void Roster_IsSortedById_AndSeparatesVictim()
        {
            var roster = new NpcRoster(new[]
            {
                new NpcDefinition { id = "npc_b" },
                new NpcDefinition { id = "npc_a" },
                new NpcDefinition { id = "npc_victim", isVictim = true }
            });

            Assert.AreEqual("npc_a", roster.All[0].id);
            Assert.AreEqual(2, roster.Suspects.Count);
            Assert.AreEqual("npc_victim", roster.Victim.id);
        }
    }
}
