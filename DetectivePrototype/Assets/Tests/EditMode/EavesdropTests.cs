using System.Collections.Generic;
using System.IO;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 엿듣기 수직 슬라이스(DEVELOPMENT_PLAN_UNHEARD.md §8).
    /// "같은 90초를 두 위치에서 들으면 서로 다른 것이 들리고, 둘을 합쳐야 한 가지 사실이 나온다"를 증명한다.
    /// 실제 rooms.json과 script_slice.json을 읽는다. Unity 밖(헤드리스)에서는 Newtonsoft.Json으로 대신 읽는다.
    /// </summary>
    public class EavesdropTests
    {
        private const string RoomsPath = "Assets/Resources/GameData/rooms.json";
        private const string SlicePath = "Assets/Resources/GameData/cases/slice/script_slice.json";

        private const string Dining = "room_dining";
        private const string Hall = "room_hall";
        private const string Lobby = "room_lobby";

        private ScriptDefinition _script;
        private RoomLayout _layout;
        private ScriptTimeline _timeline;
        private AudibilityModel _model;

        [SetUp]
        public void SetUp()
        {
            _layout = RoomLayout.FromTable(FromJson<RoomTable>(ReadProjectFile(RoomsPath)));
            _script = FromJson<ScriptDefinition>(ReadProjectFile(SlicePath)).Normalized();
            _timeline = new ScriptTimeline(_script);
            _model = new AudibilityModel(_layout);
        }

        // ── 데이터 ───────────────────────────────────────────────

        [Test]
        public void SliceScript_Is90Seconds_TwoRooms_TwoVoices_WithEmptyClipSlots()
        {
            Assert.AreEqual(90000, _script.durationMs);

            var rooms = new HashSet<string>();
            var voices = new HashSet<string>();
            for (int i = 0; i < _script.utterances.Length; i++)
            {
                Utterance u = _script.utterances[i];
                rooms.Add(u.room);
                voices.Add(u.voiceId);
                Assert.AreEqual(string.Empty, u.clip, u.id + " clip은 빈 자리여야 한다");
                RoomDefinition room;
                Assert.IsTrue(_layout.TryGetRoom(u.room, out room), u.id + "의 방이 rooms.json에 없다");
            }
            Assert.AreEqual(2, rooms.Count);
            Assert.AreEqual(2, voices.Count);
        }

        [Test]
        public void SliceScript_PassesSolvabilityCheck()
        {
            SliceSolvabilityReport report = SliceSolvability.Check(_script, _layout);
            Assert.IsEmpty(report.Problems, string.Join("\n", report.Problems.ToArray()));
            Assert.IsTrue(report.EveryFactAudibleSomewhere, "(a) 필요한 사실이 각각 어딘가에서는 Full로 들려야 한다");
            Assert.IsTrue(report.NoSingleRoomSuffices, "(b) 한 방에 고정돼서는 전부 못 들어야 한다");
            Assert.IsTrue(report.IsSolvable);
        }

        // ── 시간 축 ──────────────────────────────────────────────

        [Test]
        public void Timeline_ActiveAt_UsesHalfOpenIntervals()
        {
            Assert.IsEmpty(_timeline.ActiveAt(0));
            CollectionAssert.AreEqual(new[] { "u01" }, Ids(_timeline.ActiveAt(2000)));  // 시작 순간 포함
            CollectionAssert.AreEqual(new[] { "u01" }, Ids(_timeline.ActiveAt(4999)));
            CollectionAssert.AreEqual(new[] { "u02" }, Ids(_timeline.ActiveAt(5500)));  // u01 끝(5500)은 제외
            CollectionAssert.AreEqual(new[] { "u06", "u07" }, Ids(_timeline.ActiveAt(33000)));  // 두 방에서 동시에
            Assert.IsEmpty(_timeline.ActiveAt(89999));
        }

        // ── 가청 판정 ────────────────────────────────────────────

        [Test]
        public void Audibility_SameRoomFull_WallNeighbourMuffled_ElseNone()
        {
            Assert.AreEqual(Audibility.Full, _model.Judge(Dining, Dining));
            Assert.AreEqual(Audibility.Muffled, _model.Judge(Dining, Hall));      // door_dining_hall
            Assert.AreEqual(Audibility.Muffled, _model.Judge(Hall, Dining));
            Assert.AreEqual(Audibility.Muffled, _model.Judge(Lobby, Hall));       // door_lobby_hall

            // 문이 없어도 벽 한 장을 맞대고 있으면 들린다(로비 x0~12, 식당 x12~24가 x=12에서 만난다).
            Assert.AreEqual(Audibility.Muffled, _model.Judge(Lobby, Dining));
            Assert.AreEqual(Audibility.Muffled, _model.Judge(Dining, Lobby));

            // 떨어져 있으면 여전히 안 들린다. 서재(y14~24)와 식당(y0~10) 사이에는 복도가 통째로 놓여 있다.
            Assert.AreEqual(Audibility.None, _model.Judge("room_victim", Dining));
            Assert.AreEqual(Audibility.None, _model.Judge("room_victim", "room_suspect"));
            Assert.AreEqual(Audibility.None, _model.Judge("no_such_room", Dining));
        }

        [Test]
        public void Audibility_WallAdjacency_BreaksTheHallOnlyStar()
        {
            // 문만 보면 문 5개가 전부 한쪽이 복도라, 복도가 아닌 두 방은 서로 영원히 무음이 된다.
            // 벽 맞닿음을 넣으면 로비↔식당·식당↔창고가 살아나 복도 편중이 풀린다.
            Assert.AreEqual(Audibility.Muffled, _model.Judge(Dining, "room_storage"));
            Assert.AreEqual(Audibility.Muffled, _model.Judge("room_storage", Dining));

            // 로비와 창고는 식당을 사이에 두고 떨어져 있다 — 벽을 맞대지 않는다.
            Assert.AreEqual(Audibility.None, _model.Judge(Lobby, "room_storage"));
        }

        [Test]
        public void Perceive_NeighbourRoomIsMuffled_AndHidesContent()
        {
            // 33.000초: 식당에서 u07(전화), 복도에서 u06(혼잣말)이 동시에 울린다
            List<PerceivedUtterance> fromDining = _model.Perceive(_timeline, 33000, Dining);
            Assert.AreEqual(2, fromDining.Count);

            PerceivedUtterance phone = Find(fromDining, "u07");
            Assert.AreEqual(Audibility.Full, phone.Level);
            Assert.AreEqual("v1", phone.VoiceId);
            StringAssert.Contains("초록 넥타이", phone.Text);

            PerceivedUtterance mutter = Find(fromDining, "u06");
            Assert.AreEqual(Audibility.Muffled, mutter.Level);
            Assert.AreEqual(Hall, mutter.Room, "웅얼거림도 어느 벽 너머인지는 알려 준다");
            Assert.AreEqual(string.Empty, mutter.Text, "웅얼거림은 내용이 보이면 안 된다");
            Assert.AreEqual(string.Empty, mutter.VoiceId, "웅얼거림은 누구 목소리인지도 보이면 안 된다");

            // 로비는 복도와 문으로, 식당과 벽으로 이어진다 → 둘 다 웅얼거림으로 들리되 내용은 없다
            List<PerceivedUtterance> fromLobby = _model.Perceive(_timeline, 33000, Lobby);
            Assert.AreEqual(2, fromLobby.Count);
            for (int i = 0; i < fromLobby.Count; i++)
            {
                Assert.AreEqual(Audibility.Muffled, fromLobby[i].Level);
                Assert.AreEqual(string.Empty, fromLobby[i].Text);
                Assert.AreEqual(string.Empty, fromLobby[i].VoiceId);
            }
            Assert.IsNotNull(Find(fromLobby, "u06"), "복도 혼잣말이 문 너머로");
            Assert.IsNotNull(Find(fromLobby, "u07"), "식당 통화가 벽 너머로");
        }

        // ── 핵심 판정 ────────────────────────────────────────────

        [Test]
        public void SameNinetySeconds_FromTwoRooms_HearsDifferentUtterances()
        {
            HashSet<string> dining = SliceSolvability.HeardUtteranceIds(_timeline, _model, ListeningPlan.Fixed(Dining));
            HashSet<string> hall = SliceSolvability.HeardUtteranceIds(_timeline, _model, ListeningPlan.Fixed(Hall));

            Assert.IsFalse(dining.SetEquals(hall));
            Assert.IsTrue(dining.Contains("u07") && !hall.Contains("u07"));
            Assert.IsTrue(hall.Contains("u06") && !dining.Contains("u06"));
            Assert.IsFalse(dining.Overlaps(hall), "이 슬라이스에서는 두 방이 온전히 듣는 발화가 하나도 겹치지 않는다");
        }

        [Test]
        public void StayingInAnyOneRoomForAllNinetySeconds_NeverYieldsEveryFact()
        {
            ScriptConclusion conclusion = _script.conclusions[0];
            IList<RoomDefinition> rooms = _layout.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                HashSet<string> heard = SliceSolvability.HeardUtteranceIds(_timeline, _model, ListeningPlan.Fixed(rooms[i].id));
                HashSet<string> known = SliceSolvability.KnownFacts(_script, heard);

                Assert.Less(Count(conclusion.requiresFacts, known), conclusion.requiresFacts.Length,
                    rooms[i].id + "에 고정돼 있기만 해도 필요한 사실을 전부 듣는다");
                Assert.IsEmpty(SliceSolvability.DerivedConclusions(_script, known), rooms[i].id);
            }
        }

        [Test]
        public void CombiningBothRooms_YieldsTheConclusion()
        {
            var heard = new HashSet<string>(SliceSolvability.HeardUtteranceIds(_timeline, _model, ListeningPlan.Fixed(Dining)));
            HashSet<string> diningFacts = SliceSolvability.KnownFacts(_script, heard);
            heard.UnionWith(SliceSolvability.HeardUtteranceIds(_timeline, _model, ListeningPlan.Fixed(Hall)));
            HashSet<string> combined = SliceSolvability.KnownFacts(_script, heard);

            CollectionAssert.AreEquivalent(new[] { "fact_key_holder_wears_green_tie" }, diningFacts);
            CollectionAssert.AreEquivalent(new[] { "fact_key_holder_wears_green_tie", "fact_v2_wears_green_tie" }, combined);
            CollectionAssert.AreEqual(new[] { "concl_v2_holds_key" }, SliceSolvability.DerivedConclusions(_script, combined));
        }

        [Test]
        public void KeyUtterancesOverlap_SoOnePassCannotCatchBoth_MustReplay()
        {
            Assert.IsFalse(SliceSolvability.Check(_script, _layout).SinglePassPossible);

            // 복도에서 u06을 끝까지 듣고(~37.0초) 식당으로 가면 u07(32.0초 시작)은 이미 반쯤 지나갔다
            ListeningPlan hallThenDining = new ListeningPlan().Then(0, Hall).Then(37000, Dining);
            HashSet<string> heard = SliceSolvability.HeardUtteranceIds(_timeline, _model, hallThenDining);
            Assert.IsTrue(heard.Contains("u06"));
            Assert.IsFalse(heard.Contains("u07"), "도중에 들어온 발화는 온전히 들은 것이 아니다");
        }

        // ── 검사기가 헛돌지 않는가 (합성 대본) ──────────────────────

        [Test]
        public void Checker_FlagsScriptWhereOneRoomHearsEverything()
        {
            ScriptDefinition script = MakeTwoFactScript(Dining, Dining);
            SliceSolvabilityReport report = SliceSolvability.Check(script, _layout);

            Assert.IsTrue(report.EveryFactAudibleSomewhere);
            Assert.IsFalse(report.NoSingleRoomSuffices);
            CollectionAssert.AreEqual(new[] { Dining }, report.RoomsHearingEverything);
            Assert.IsFalse(report.IsSolvable);
        }

        [Test]
        public void Checker_FlagsFactThatIsNeverFullyAudible()
        {
            ScriptDefinition script = MakeTwoFactScript(Dining, Hall);
            script.utterances[1].room = "room_nowhere";
            SliceSolvabilityReport report = SliceSolvability.Check(script, _layout);

            Assert.IsFalse(report.EveryFactAudibleSomewhere);
            Assert.IsFalse(report.IsSolvable);
        }

        [Test]
        public void Checker_AcceptsSplitScript_AndSeesSequentialOnePass()
        {
            ScriptDefinition script = MakeTwoFactScript(Dining, Hall);  // 겹치지 않게 배치됨
            SliceSolvabilityReport report = SliceSolvability.Check(script, _layout);

            Assert.IsTrue(report.IsSolvable, string.Join("\n", report.Problems.ToArray()));
            Assert.IsTrue(report.SinglePassPossible, "시간이 겹치지 않으면 한 번의 재생에서 옮겨 다니며 모을 수 있다");
        }

        // ── 도우미 ───────────────────────────────────────────────

        private static ScriptDefinition MakeTwoFactScript(string roomA, string roomB)
        {
            return new ScriptDefinition
            {
                caseId = "synthetic",
                durationMs = 20000,
                speakers = new[] { new SpeakerDefinition { voiceId = "v1", npcId = "npc_a" } },
                utterances = new[]
                {
                    new Utterance { id = "a", startMs = 1000, durationMs = 3000, voiceId = "v1", room = roomA, text = "A", clip = "" },
                    new Utterance { id = "b", startMs = 8000, durationMs = 3000, voiceId = "v1", room = roomB, text = "B", clip = "" }
                },
                facts = new[]
                {
                    new ScriptFact { id = "fa", utteranceIds = new[] { "a" } },
                    new ScriptFact { id = "fb", utteranceIds = new[] { "b" } }
                },
                conclusions = new[] { new ScriptConclusion { id = "c", requiresFacts = new[] { "fa", "fb" } } }
            }.Normalized();
        }

        private static List<string> Ids(List<Utterance> utterances)
        {
            var ids = new List<string>();
            for (int i = 0; i < utterances.Count; i++) ids.Add(utterances[i].id);
            return ids;
        }

        private static PerceivedUtterance Find(List<PerceivedUtterance> list, string utteranceId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].UtteranceId == utteranceId) return list[i];
            }
            Assert.Fail(utteranceId + "이(가) 들리지 않는다");
            return null;
        }

        private static int Count(string[] ids, HashSet<string> set)
        {
            int n = 0;
            for (int i = 0; i < ids.Length; i++) if (set.Contains(ids[i])) n++;
            return n;
        }

        private static T FromJson<T>(string json)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<T>(json);
#else
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
#endif
        }

        /// <summary>프로젝트 루트(Assets가 있는 곳)를 현재 디렉터리·테스트 디렉터리에서 위로 찾아 올라간다.</summary>
        private static string ReadProjectFile(string relativePath)
        {
            string[] starts = { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory };
            for (int s = 0; s < starts.Length; s++)
            {
                for (DirectoryInfo dir = new DirectoryInfo(starts[s]); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, relativePath);
                    if (File.Exists(candidate)) return File.ReadAllText(candidate);
                }
            }
            Assert.Fail(relativePath + "을(를) 찾지 못했다");
            return null;
        }
    }
}
