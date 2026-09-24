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

        /// <summary>정답: culprit / 금전 / 18:30 / 서재 / 독살 / 와인잔.</summary>
        public static CaseDefinition Case()
        {
            return new CaseDefinition
            {
                id = "test_case",
                title = "테스트 사건",
                intro = "테스트용 사건이다.",
                answer = new CaseAnswer { culprit = "culprit", motive = "money", tick = 3, room = "study", method = "poison", evidence = "ev_glass" },
                motives = new[] { new ChoiceDefinition { id = "money", label = "돈" }, new ChoiceDefinition { id = "love", label = "사랑" } },
                methods = new[] { new ChoiceDefinition { id = "poison", label = "독살" }, new ChoiceDefinition { id = "blunt", label = "둔기" } }
            };
        }

        /// <summary>
        /// culprit은 알리바이 대사와 와인잔 반응, witness는 알리바이 대사와 18:10 목격 덮어쓰기를 가진다.
        /// </summary>
        public static DialogueFile[] Dialogues()
        {
            return new[]
            {
                new DialogueFile
                {
                    npcId = "culprit",
                    lines = new[]
                    {
                        new DialogueLine { id = "alibi", revealsClaims = true, text = "계속 A방에 있었어요." },
                        new DialogueLine { id = "on_glass", requiresEvidence = "ev_glass", text = "그 잔은 모르는 일이에요." },
                        new DialogueLine { id = "confess", requiresEvidence = "ev_log", claimTick = 3, claimRoom = "study", text = "…서재에 갔었어요." }
                    }
                },
                new DialogueFile
                {
                    npcId = "witness",
                    lines = new[]
                    {
                        new DialogueLine { id = "alibi", revealsClaims = true, text = "18:10에만 B방에 다녀왔습니다." },
                        new DialogueLine { id = "saw", sightingTick = 1, sightingTarget = "culprit", text = "18:10에 B방에서 클라라가 서두르더군요." }
                    }
                }
            };
        }

        public static CaseDatabase Database()
        {
            return Database(Npcs(), Evidence(), Dialogues());
        }

        public static CaseDatabase Database(NpcDefinition[] npcs, EvidenceTable evidence, DialogueFile[] dialogues)
        {
            return Database(npcs, evidence, dialogues, Case());
        }

        public static CaseDatabase Database(NpcDefinition[] npcs, EvidenceTable evidence, DialogueFile[] dialogues, CaseDefinition caseDefinition)
        {
            return new CaseDatabase(RoomLayout.FromTable(Rooms()), new NpcRoster(npcs), evidence, dialogues, caseDefinition);
        }
    }
}
