using System.Collections.Generic;
using System.IO;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 이동에서 뽑아낸 소리를 검사한다.
    ///
    /// 이 층이 하는 일은 "말이 안 들려도 정황은 남는다"다. 복도에 귀를 두면 누가 언제
    /// 지나갔는지 발소리로 셀 수 있어야 하고, 그것이 알리바이를 깨는 재료가 된다.
    ///
    /// 조용히 깨질 수 있는 지점:
    ///   * id가 실행마다 달라지면 들은 기록과 저장 파일이 회차를 다시 열 때 맞지 않는다.
    ///   * 지나가는 방을 잘못 짚으면(예: 복도를 빠뜨리면) 발소리가 아무도 못 듣는 곳에서 난다.
    ///   * 이벤트가 말을 밀어내면(들은 수에 끼면) 퍼즐 진행도가 망가진다.
    ///
    /// 실제 tracks.json과 rooms.json에 대고 검사한다 — 만들어 낸 예제로는 진짜 데이터에서도
    /// 성립하는지 보증하지 못한다.
    /// </summary>
    public class MovementEventTests
    {
        private const string RoomsPath = "Assets/Resources/GameData/rooms.json";
        private const string TracksPath = "Assets/Resources/GameData/cases/case_02/tracks.json";
        private const int RunDurationMs = 600000;

        private RoomLayout _layout;
        private MovementTracks _tracks;
        private List<ScriptEvent> _events;

        [SetUp]
        public void SetUp()
        {
            _layout = RoomLayout.FromTable(FromJson<RoomTable>(ReadProjectFile(RoomsPath)));
            _tracks = MovementTracks.FromTable(FromJson<MovementTrackTable>(ReadProjectFile(TracksPath)));
            _events = MovementEvents.Derive(_tracks, _layout, RunDurationMs);
        }

        // ── 뽑아낸 결과 ────────────────────────────────────────

        [Test]
        public void RealTracks_ProduceSounds()
        {
            Assert.IsNotEmpty(_events, "5명이 10분 동안 움직이는데 소리가 하나도 안 난다면 뽑기가 실패한 것이다");
        }

        [Test]
        public void EverySound_LandsInARealRoom()
        {
            // 방 이름이 틀리면 아무도 못 듣는 곳에서 소리가 난다 — 화면에도 안 뜨므로 눈치채기 어렵다.
            foreach (ScriptEvent e in _events)
            {
                RoomDefinition room;
                Assert.IsTrue(_layout.TryGetRoom(e.room, out room),
                    e.id + " 의 방 '" + e.room + "' 이 rooms.json에 없다");
            }
        }

        [Test]
        public void EverySound_FallsInsideTheRun()
        {
            foreach (ScriptEvent e in _events)
            {
                Assert.GreaterOrEqual(e.startMs, 0, e.id);
                Assert.Less(e.startMs, RunDurationMs, e.id + " 가 회차 밖에서 난다");
            }
        }

        [Test]
        public void Ids_AreStableAcrossRuns()
        {
            // 같은 트랙이면 같은 id. 아니면 저장 파일이 회차를 다시 열 때 맞지 않는다.
            List<ScriptEvent> again = MovementEvents.Derive(_tracks, _layout, RunDurationMs);
            Assert.AreEqual(_events.Count, again.Count);
            for (int i = 0; i < _events.Count; i++)
                Assert.AreEqual(_events[i].id, again[i].id, i + "번째 소리의 id가 실행마다 달라진다");
        }

        [Test]
        public void Ids_AreUnique()
        {
            var seen = new HashSet<string>();
            foreach (ScriptEvent e in _events)
                Assert.IsTrue(seen.Add(e.id), "id가 겹친다: " + e.id);
        }

        [Test]
        public void Sounds_AreSortedByTime()
        {
            for (int i = 1; i < _events.Count; i++)
                Assert.LessOrEqual(_events[i - 1].startMs, _events[i].startMs,
                    "시간 순으로 정렬돼 있어야 ActiveAt이 일찍 끊을 수 있다");
        }

        // ── 경로 판정 ──────────────────────────────────────────

        [Test]
        public void MovingBetweenTwoRooms_PassesThroughTheHall()
        {
            // 이 저택은 모든 방이 복도를 통해서만 이어진다. 식당→창고는 복도를 지나야 한다.
            List<string> through = MovementEvents.RoomsBetween(_layout, "room_dining", "room_storage");

            Assert.IsNotEmpty(through, "거쳐 가는 방이 없으면 발소리가 어디서도 나지 않는다");
            Assert.Contains("room_hall", through);
        }

        [Test]
        public void RoomsBetween_ExcludesBothEnds()
        {
            List<string> through = MovementEvents.RoomsBetween(_layout, "room_dining", "room_storage");
            Assert.IsFalse(through.Contains("room_dining"), "출발한 방은 '거쳐 가는' 방이 아니다");
            Assert.IsFalse(through.Contains("room_storage"), "도착한 방은 '거쳐 가는' 방이 아니다");
        }

        [Test]
        public void FootstepsAppearInTheHall()
        {
            int inHall = 0;
            foreach (ScriptEvent e in _events)
                if (e.kind == EventKind.Footsteps && e.room == "room_hall") inHall++;

            Assert.Greater(inHall, 0,
                "복도에 귀를 두면 지나가는 발소리를 셀 수 있어야 한다 — 그게 이 층의 목적이다");
        }

        [Test]
        public void EveryTransitMakesADoorSoundOnBothSides()
        {
            // 떠나는 문과 들어가는 문이 짝을 이룬다. 한쪽만 나면 어느 방에서 나갔는지 알 수 없다.
            int doors = 0;
            foreach (ScriptEvent e in _events) if (e.kind == EventKind.Door) doors++;
            Assert.AreEqual(0, doors % 2, "문소리는 떠남·들어옴이 짝이므로 짝수여야 한다");
            Assert.Greater(doors, 0);
        }

        // ── 듣기 ───────────────────────────────────────────────

        [Test]
        public void SameRoom_HearsTheDescription_WallHearsOnlyAKind()
        {
            var audibility = new AudibilityModel(_layout);
            var timeline = new EventTimeline(new[]
            {
                new ScriptEvent { id = "e1", startMs = 1000, durationMs = 1000, room = "room_victim",
                                  kind = EventKind.Object, text = "무언가 서랍에 놓인다" }
            });

            List<PerceivedEvent> inRoom = audibility.PerceiveEvents(timeline, 1500, "room_victim");
            Assert.AreEqual(1, inRoom.Count);
            Assert.AreEqual(Audibility.Full, inRoom[0].Level);
            Assert.AreEqual("무언가 서랍에 놓인다", inRoom[0].Text);

            List<PerceivedEvent> throughWall = audibility.PerceiveEvents(timeline, 1500, "room_hall");
            Assert.AreEqual(1, throughWall.Count);
            Assert.AreEqual(Audibility.Muffled, throughWall[0].Level);
            Assert.AreNotEqual("무언가 서랍에 놓인다", throughWall[0].Text,
                "벽 너머에서 내용까지 알면 벽이 의미가 없다");
        }

        [Test]
        public void LoudSounds_CarryThroughWalls()
        {
            // 유리가 깨지면 옆 방에서도 유리가 깨졌다는 것을 안다. 말과 다른 점이다.
            var audibility = new AudibilityModel(_layout);
            var timeline = new EventTimeline(new[]
            {
                new ScriptEvent { id = "e1", startMs = 0, durationMs = 1000, room = "room_victim",
                                  kind = EventKind.Break, text = "유리가 깨진다" }
            });

            List<PerceivedEvent> throughWall = audibility.PerceiveEvents(timeline, 500, "room_hall");
            Assert.AreEqual(1, throughWall.Count);
            Assert.AreEqual(Audibility.Full, throughWall[0].Level, "큰 소리는 벽을 넘어도 또렷하다");
            Assert.AreEqual("유리가 깨진다", throughWall[0].Text);
        }

        [Test]
        public void WhoMadeTheSound_IsNeverRevealed()
        {
            // 누가 그 방에 있었는지가 플레이어가 알아내야 하는 것이다. 소리가 이름을 말하면 안 된다.
            var audibility = new AudibilityModel(_layout);
            var timeline = new EventTimeline(new[]
            {
                new ScriptEvent { id = "e1", startMs = 0, durationMs = 1000, room = "room_victim",
                                  npcId = "npc_a", kind = EventKind.Door, text = "문이 닫힌다" }
            });

            List<PerceivedEvent> heard = audibility.PerceiveEvents(timeline, 500, "room_victim");
            Assert.AreEqual(string.Empty, heard[0].NpcId, "소리는 누가 냈는지 알려 주지 않는다");
        }

        [Test]
        public void Events_DoNotCountTowardHeardUtterances()
        {
            // 이벤트는 정황이고 말은 증언이다. 섞이면 진행도(들은 대사 n/m)가 망가진다.
            var script = new ScriptDefinition
            {
                caseId = "event_isolation",
                durationMs = 10000,
                speakers = new[] { new SpeakerDefinition { voiceId = "v1", npcId = "npc_a" } },
                utterances = new[]
                {
                    new Utterance { id = "u1", startMs = 5000, durationMs = 1000, voiceId = "v1",
                                    room = "room_victim", text = "말", clip = "" }
                }
            }.Normalized();

            var events = new EventTimeline(new[]
            {
                new ScriptEvent { id = "e1", startMs = 0, durationMs = 2000, room = "room_victim",
                                  kind = EventKind.Door, text = "문" }
            });

            var session = new ListeningSession(new ScriptTimeline(script),
                                               new AudibilityModel(_layout), events);
            session.MoveTo("room_victim");
            session.SeekTo(500);

            Assert.IsNotEmpty(session.CurrentEvents, "문소리는 들려야 한다");
            Assert.AreEqual(0, session.FullyHeardCount, "그런데 들은 '대사' 수는 그대로여야 한다");
        }

        [Test]
        public void EventTimeline_Between_FiltersToTheWindow()
        {
            // 영상 진행 바에 "여기서 뭔가 났다"를 찍으려면 범위로 물어봐야 한다.
            // 실제 데이터에 의존하지 않도록 경계가 분명한 이벤트로 검사한다.
            var timeline = new EventTimeline(new[]
            {
                new ScriptEvent { id = "before", startMs = 1000, durationMs = 500,  room = "room_hall", kind = EventKind.Door },
                new ScriptEvent { id = "inside", startMs = 5000, durationMs = 500,  room = "room_hall", kind = EventKind.Door },
                new ScriptEvent { id = "after",  startMs = 9000, durationMs = 500,  room = "room_hall", kind = EventKind.Door }
            });

            List<ScriptEvent> window = timeline.Between(4000, 6000);

            Assert.AreEqual(1, window.Count, "창 안의 것만 나와야 눈금이 제자리에 찍힌다");
            Assert.AreEqual("inside", window[0].id);
        }

        [Test]
        public void EventTimeline_Between_IncludesSoundsStillRinging()
        {
            // 창이 시작될 때 이미 울리고 있던 소리도 그 구간의 일부다.
            var timeline = new EventTimeline(new[]
            {
                new ScriptEvent { id = "long", startMs = 1000, durationMs = 5000, room = "room_hall", kind = EventKind.Struggle }
            });

            Assert.AreEqual(1, timeline.Between(3000, 4000).Count);
        }

        [Test]
        public void DerivedSounds_SpanMostOfTheRun()
        {
            // 소리가 한 구간에만 뭉쳐 있으면 회차 대부분이 조용하다는 뜻이다.
            // 이동 동선이 빈약하면 여기서 드러난다.
            var timeline = new EventTimeline(_events);
            Assert.IsNotEmpty(timeline.All);

            int first = timeline.All[0].startMs;
            int last = timeline.All[timeline.Count - 1].startMs;
            Assert.Greater(last - first, RunDurationMs / 3,
                "이동에서 나는 소리가 회차의 1/3도 못 덮으면 사람들이 거의 움직이지 않는다는 뜻이다");
        }

        // ── 도우미 ─────────────────────────────────────────────

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

        private static T FromJson<T>(string json)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<T>(json);
#else
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
#endif
        }
    }
}
