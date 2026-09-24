using Detective.Data;
using Detective.NPC;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>스케줄 조회와 "같은 시각·같은 방" 질의. 목격 판정의 바탕이다.</summary>
    public class ScheduleTests
    {
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
            Assert.AreEqual("a", NpcSchedule.RoomAt(npc, 0));
            Assert.AreEqual("c", NpcSchedule.RoomAt(npc, 3));
        }

        [Test]
        public void RoomAt_OutOfRange_IsEmpty()
        {
            NpcDefinition npc = MakeLiar();
            Assert.AreEqual(string.Empty, NpcSchedule.RoomAt(npc, -1));
            Assert.AreEqual(string.Empty, NpcSchedule.RoomAt(npc, 7));
            Assert.AreEqual(string.Empty, NpcSchedule.RoomAt(null, 0));
        }

        [Test]
        public void ClaimedRoomAt_UsesClaimOnlyWhereGiven()
        {
            NpcDefinition npc = MakeLiar();
            Assert.AreEqual("a", NpcSchedule.ClaimedRoomAt(npc, 1), "거짓 주장");
            Assert.AreEqual("c", NpcSchedule.ClaimedRoomAt(npc, 3), "주장이 없으면 사실대로");
        }

        [Test]
        public void IsHonestAt_DetectsLies()
        {
            NpcDefinition npc = MakeLiar();
            Assert.IsTrue(NpcSchedule.IsHonestAt(npc, 0));
            Assert.IsFalse(NpcSchedule.IsHonestAt(npc, 1));
            Assert.IsFalse(NpcSchedule.IsHonestAt(npc, 2));
            Assert.IsTrue(NpcSchedule.IsHonestAt(npc, 3));
        }

        [Test]
        public void ClaimsMatchingTruth_CountAsHonest()
        {
            var npc = new NpcDefinition { id = "x", schedule = new[] { "a", "b", "a", "a", "a", "a", "a" }, claims = new[] { "a", "b", "", "", "", "", "" } }.Normalized();
            for (int t = 0; t < 7; t++) Assert.IsTrue(NpcSchedule.IsHonestAt(npc, t), "t=" + t);
        }

        [Test]
        public void LastKnownRoom_SkipsEmptyTicks()
        {
            var victim = new NpcDefinition { id = "v", isVictim = true, schedule = new[] { "study", "study", "study", "study", "", "", "" } }.Normalized();
            Assert.AreEqual("study", NpcSchedule.LastKnownRoom(victim, 6));
            Assert.AreEqual(string.Empty, NpcSchedule.RoomAt(victim, 6));
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

            Assert.AreEqual(2, roster.OccupantsAt(0, "a").Count);
            Assert.AreEqual(2, roster.OccupantsAt(1, "b").Count);
            Assert.AreEqual(1, roster.OccupantsAt(2, "b").Count, "사망 이후 피해자는 어디에도 없다");
            Assert.AreEqual(0, roster.OccupantsAt(0, "").Count);
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
