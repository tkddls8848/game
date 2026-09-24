using Detective.Core;
using Detective.Data;
using Detective.NPC;

namespace Detective.Tests
{
    /// <summary>
    /// 테스트용 최소 사건. 실제 JSON에 기대지 않고 규칙만 검증하려고 코드로 만든다.
    ///   방: a, b, study(서재)
    ///   culprit: 18:30에 study에 있었지만 계속 a에 있었다고 주장한다.
    ///   witness: 18:10에 b에서 culprit을 봤다(정직).
    /// </summary>
    public static class TestCaseFactory
    {
        public static RoomTable Rooms()
        {
            return new RoomTable
            {
                rooms = new[]
                {
                    new RoomDefinition { id = "a", displayName = "A방", x = 0f, y = 0f, width = 10f, height = 10f },
                    new RoomDefinition { id = "b", displayName = "B방", x = 10f, y = 0f, width = 10f, height = 10f },
                    new RoomDefinition { id = "study", displayName = "서재", x = 20f, y = 0f, width = 10f, height = 10f }
                },
                doors = new[]
                {
                    new DoorDefinition { id = "d_ab", roomA = "a", roomB = "b", x = 9.7f, y = 4f, width = 0.6f, height = 2f },
                    new DoorDefinition { id = "d_bs", roomA = "b", roomB = "study", x = 19.7f, y = 4f, width = 0.6f, height = 2f }
                },
                playerSpawnRoom = "a"
            };
        }

        public static NpcDefinition[] Npcs()
        {
            return new[]
            {
                new NpcDefinition
                {
                    id = "victim", displayName = "피해자", isVictim = true,
                    schedule = new[] { "study", "study", "study", "study", "", "", "" }
                },
                new NpcDefinition
                {
                    id = "culprit", displayName = "클라라",
                    schedule = new[] { "a", "b", "b", "study", "b", "a", "a" },
                    claims = new[] { "", "a", "a", "a", "a", "", "" }
                },
                new NpcDefinition
                {
                    id = "witness", displayName = "마르코",
                    schedule = new[] { "a", "b", "a", "a", "a", "a", "a" }
                }
            };
        }

        public static EvidenceTable Evidence()
        {
            return new EvidenceTable
            {
                evidence = new[]
                {
                    new EvidenceDefinition
                    {
                        id = "ev_glass", name = "와인잔", description = "독이 든 잔.", foundRoom = "study",
                        relatedNpc = "culprit", relatedTick = 3, revealTick = -1
                    },
                    new EvidenceDefinition
                    {
                        id = "ev_log", name = "기록부", description = "18:30 a방 통화.", foundRoom = "a",
                        relatedNpc = "witness", relatedTick = 3, revealNpc = "witness", revealTick = 3, revealRoom = "a"
                    }
                }
            };
        }

        public static CaseDatabase Database()
        {
            return new CaseDatabase(RoomLayout.FromTable(Rooms()), new NpcRoster(Npcs()), Evidence());
        }
    }
}
