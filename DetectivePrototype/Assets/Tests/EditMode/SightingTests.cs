using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Dialogue;
using Detective.NPC;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>목격 판정: 같은 시각 + 같은 방 = 목격. 거짓말하는 시각의 목격은 털어놓지 않는다.</summary>
    public class SightingTests
    {
        private static NpcDefinition Get(CaseDatabase database, string id)
        {
            NpcDefinition npc;
            database.Npcs.TryGet(id, out npc);
            return npc;
        }

        [Test]
        public void Witnessed_SameTickSameRoom()
        {
            CaseDatabase database = TestCaseFactory.Database();
            List<Sighting> seen = SightingGenerator.Witnessed(database.Npcs, Get(database, "witness"));

            // witness: a b a a a a a / culprit: a b b study b a a
            Assert.IsTrue(seen.Exists(s => s.Tick == 0 && s.TargetId == "culprit" && s.RoomId == "a"));
            Assert.IsTrue(seen.Exists(s => s.Tick == 1 && s.TargetId == "culprit" && s.RoomId == "b"));
            Assert.IsFalse(seen.Exists(s => s.Tick == 2), "다른 방에 있으면 못 본다");
            Assert.IsFalse(seen.Exists(s => s.TargetId == "witness"), "자기 자신은 목격 대상이 아니다");
        }

        [Test]
        public void Reported_HidesSightingsAtTicksTheyLieAbout()
        {
            CaseDatabase database = TestCaseFactory.Database();
            NpcDefinition culprit = Get(database, "culprit");

            List<Sighting> seen = SightingGenerator.Witnessed(database.Npcs, culprit);
            List<Sighting> told = SightingGenerator.Reported(database.Npcs, culprit);

            Assert.IsTrue(seen.Exists(s => s.Tick == 3 && s.TargetId == "victim"), "18:30 서재에서 피해자를 봤다");
            Assert.IsFalse(told.Exists(s => s.Tick == 3), "하지만 그 시각엔 거짓말 중이라 말하지 않는다");
            Assert.IsTrue(told.Exists(s => s.Tick == 0 && s.TargetId == "witness"), "사실대로 말하는 시각의 목격은 말한다");
        }

        [Test]
        public void VictimNeverReports()
        {
            CaseDatabase database = TestCaseFactory.Database();
            Assert.AreEqual(0, SightingGenerator.Witnessed(database.Npcs, Get(database, "victim")).Count);
        }

        [Test]
        public void DeadVictimIsNotSeenAfterDeath()
        {
            CaseDatabase database = TestCaseFactory.Database();
            List<NpcDefinition> inStudyAfter = database.Npcs.OccupantsAt(4, "study");
            Assert.AreEqual(0, inStudyAfter.Count);
        }

        [Test]
        public void DefaultText_UsesTimeRoomAndParticle()
        {
            CaseDatabase database = TestCaseFactory.Database();
            string text = SightingGenerator.DefaultText(new Sighting("witness", "culprit", 1, "b"), database.Npcs, database.Layout);
            Assert.AreEqual("18:10쯤 B방에서 클라라를 봤습니다.", text);
        }
    }
}
