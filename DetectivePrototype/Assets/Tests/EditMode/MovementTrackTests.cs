using System.Collections.Generic;
using Detective.Core;
using Detective.Eavesdrop;
using Detective.NPC;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// Phase U-3 — 연속 이동 트랙(DEVELOPMENT_PLAN_UNHEARD.md §3).
    /// 틱당 방 하나였던 <c>schedule[7]</c>이 구간 배열로 바뀐다. 구간은 <c>[startMs, endMs)</c> 반열린 구간이고,
    /// 구간 사이는 **이동 중**(방 없음)이다.
    ///
    /// 방은 TestCaseFactory의 a(0~10) · b(10~20) · study(20~30) 한 줄 배치를 쓰고,
    /// 인물 id도 TestCaseFactory와 같은 culprit · witness · victim을 쓴다.
    /// 회차 길이는 10초로 둔다.
    /// </summary>
    public class MovementTrackTests
    {
        private const string RoomA = "a";
        private const string RoomB = "b";
        private const string RoomStudy = "study";

        private const string Culprit = "culprit";
        private const string Witness = "witness";
        private const string Victim = "victim";

        private const int RunMs = 10000;

        private RoomLayout _layout;
        private NpcRoster _roster;

        [SetUp]
        public void SetUp()
        {
            _layout = RoomLayout.FromTable(TestCaseFactory.Rooms());
            _roster = new NpcRoster(TestCaseFactory.Npcs());
        }

        /// <summary>
        /// culprit: a(0~3초) → [이동] → b(4~7초) → study(7~10초, 곧바로 이어진다)
        /// witness: a에 회차 내내
        /// victim : study(2~5초)만 — 앞뒤로는 기록이 없다
        /// </summary>
        private static MovementTrackTable Table()
        {
            return new MovementTrackTable
            {
                tracks = new[]
                {
                    new MovementTrack
                    {
                        npcId = Culprit,
                        segments = new[]
                        {
                            Segment(0, 3000, RoomA),
                            Segment(4000, 7000, RoomB),
                            Segment(7000, 10000, RoomStudy)
                        }
                    },
                    new MovementTrack
                    {
                        npcId = Witness,
                        segments = new[] { Segment(0, 10000, RoomA) }
                    },
                    new MovementTrack
                    {
                        npcId = Victim,
                        segments = new[] { Segment(2000, 5000, RoomStudy) }
                    }
                }
            }.Normalized();
        }

        private static TrackSegment Segment(int startMs, int endMs, string room)
        {
            return new TrackSegment { startMs = startMs, endMs = endMs, room = room };
        }

        private static MovementTracks Tracks()
        {
            return MovementTracks.FromTable(Table());
        }

        // ── 조회 ─────────────────────────────────────────────────

        [Test]
        public void PositionAt_InsideASegment_GivesTheRoom()
        {
            MovementTracks tracks = Tracks();

            Assert.AreEqual(RoomA, tracks.PositionAt(Culprit, 1000));
            Assert.AreEqual(RoomB, tracks.PositionAt(Culprit, 5000));
            Assert.AreEqual(RoomStudy, tracks.PositionAt(Culprit, 9000));
        }

        [Test]
        public void SegmentIsHalfOpen_SoTheEndBelongsToWhatComesNext()
        {
            MovementTracks tracks = Tracks();

            Assert.AreEqual(RoomA, tracks.PositionAt(Culprit, 2999));
            Assert.AreEqual(string.Empty, tracks.PositionAt(Culprit, 3000), "구간이 끝나는 순간에는 이미 그 방에 없다");
            Assert.AreEqual(RoomB, tracks.PositionAt(Culprit, 4000), "시작하는 순간에는 이미 그 방에 있다");

            // 7000ms에서 b와 study가 맞붙어 있다 — 틈이 없으면 이동 중도 없다.
            Assert.AreEqual(RoomStudy, tracks.PositionAt(Culprit, 7000));
            Assert.IsFalse(tracks.IsInTransit(Culprit, 7000));
        }

        [Test]
        public void BetweenSegments_ThereIsNoRoom()
        {
            MovementTracks tracks = Tracks();

            Assert.AreEqual(string.Empty, tracks.PositionAt(Culprit, 3500), "이동 중에는 어느 방에도 없다");
            Assert.IsTrue(tracks.IsInTransit(Culprit, 3500));
        }

        [Test]
        public void OutsideTheRecord_IsNotTheSameAsMoving()
        {
            MovementTracks tracks = Tracks();

            Assert.AreEqual(string.Empty, tracks.PositionAt(Victim, 1000));
            Assert.IsFalse(tracks.IsInTransit(Victim, 1000), "기록이 시작하기 전은 이동 중이 아니라 그냥 없는 것이다");

            Assert.AreEqual(string.Empty, tracks.PositionAt(Victim, 6000));
            Assert.IsFalse(tracks.IsInTransit(Victim, 6000));
        }

        [Test]
        public void SampleAt_InTransit_NamesBothEndsAndHowFar()
        {
            TrackPosition position = Tracks().SampleAt(Culprit, 3500);

            Assert.IsTrue(position.InTransit);
            Assert.IsFalse(position.HasPlace);
            Assert.AreEqual(RoomA, position.FromRoom);
            Assert.AreEqual(RoomB, position.ToRoom);
            Assert.AreEqual(500, position.ProgressPermille, "3~4초 사이의 절반이면 천분율로 500이다");
        }

        [Test]
        public void SampleAt_InsideARoom_PointsNowhereElse()
        {
            TrackPosition position = Tracks().SampleAt(Culprit, 1000);

            Assert.IsFalse(position.InTransit);
            Assert.IsTrue(position.HasPlace);
            Assert.AreEqual(RoomA, position.Room);
            Assert.AreEqual(RoomA, position.FromRoom);
            Assert.AreEqual(RoomA, position.ToRoom);
            Assert.AreEqual(0, position.ProgressPermille);
        }

        [Test]
        public void UnknownNpc_HasNoPlaceAnywhere()
        {
            MovementTracks tracks = Tracks();

            Assert.AreEqual(string.Empty, tracks.PositionAt("ghost", 1000));
            Assert.IsFalse(tracks.IsInTransit("ghost", 1000));
            Assert.IsFalse(tracks.SampleAt("ghost", 1000).HasPlace);
            Assert.IsNull(tracks.StayedRoomDuring("ghost", 0, 100));
        }

        [Test]
        public void OccupantsAt_ListsEveryoneInTheRoom()
        {
            MovementTracks tracks = Tracks();

            CollectionAssert.AreEqual(new[] { Culprit, Witness }, tracks.OccupantsAt(1000, RoomA));
            CollectionAssert.AreEqual(new[] { Witness }, tracks.OccupantsAt(5000, RoomA));
            CollectionAssert.AreEqual(new[] { Victim }, tracks.OccupantsAt(3000, RoomStudy));
            CollectionAssert.IsEmpty(tracks.OccupantsAt(9000, RoomB));
        }

        [Test]
        public void StayedRoomDuring_FailsWhenTheyLeaveMidWay()
        {
            MovementTracks tracks = Tracks();

            Assert.AreEqual(RoomA, tracks.StayedRoomDuring(Culprit, 1000, 2000));
            Assert.AreEqual(RoomA, tracks.StayedRoomDuring(Culprit, 0, 3000), "구간에 꼭 맞는 창은 머무른 것이다");
            Assert.IsNull(tracks.StayedRoomDuring(Culprit, 2500, 4500), "말하는 도중에 방을 떠났으면 머무른 것이 아니다");
            Assert.IsNull(tracks.StayedRoomDuring(Culprit, 6000, 8000), "방을 옮겼으면 이어져 있어도 머무른 것이 아니다");
        }

        [Test]
        public void FirstAndLastRoom_AreTheEdgesOfTheRecord()
        {
            MovementTrack track;
            Assert.IsTrue(Tracks().TryGet(Culprit, out track));

            Assert.AreEqual(RoomA, track.FirstRoom);
            Assert.AreEqual(RoomStudy, track.LastRoom);
        }

        [Test]
        public void SegmentsWrittenOutOfOrder_ReadTheSame()
        {
            var shuffled = new MovementTrack
            {
                npcId = Culprit,
                segments = new[] { Segment(7000, 10000, RoomStudy), Segment(0, 3000, RoomA), Segment(4000, 7000, RoomB) }
            }.Normalized();

            Assert.AreEqual(RoomA, shuffled.RoomAt(1000));
            Assert.AreEqual(RoomB, shuffled.RoomAt(5000));
            Assert.AreEqual(RoomStudy, shuffled.RoomAt(9000));
            Assert.AreEqual(RoomA, shuffled.FirstRoom);
            Assert.AreEqual(RoomStudy, shuffled.LastRoom);
            Assert.IsTrue(shuffled.SampleAt(3500).InTransit);
        }

        [Test]
        public void EmptyTable_IsHarmless()
        {
            MovementTracks none = MovementTracks.FromTable(null);

            Assert.AreEqual(0, none.All.Count);
            Assert.AreEqual(string.Empty, none.PositionAt(Culprit, 0));
            CollectionAssert.IsEmpty(none.OccupantsAt(0, RoomA));
        }

        // ── 참조 무결성 ─────────────────────────────────────────

        [Test]
        public void Validate_AcceptsAGoodTable()
        {
            List<string> errors = MovementTrackValidator.Validate(Table(), _layout, _roster, RunMs);
            CollectionAssert.IsEmpty(errors, string.Join(" / ", errors.ToArray()));
        }

        [Test]
        public void Validate_CatchesOverlappingSegments()
        {
            MovementTrackTable table = Table();
            table.tracks[0].segments[1].startMs = 2000; // a(0~3000)와 겹친다

            List<string> errors = MovementTrackValidator.Validate(table, _layout, _roster, RunMs);

            Assert.AreEqual(1, errors.Count, string.Join(" / ", errors.ToArray()));
            StringAssert.Contains("겹친다", errors[0]);
        }

        [Test]
        public void Validate_CatchesBackwardsSegment()
        {
            MovementTrackTable table = Table();
            table.tracks[1].segments[0].endMs = table.tracks[1].segments[0].startMs;

            List<string> errors = MovementTrackValidator.Validate(table, _layout, _roster, RunMs);

            Assert.AreEqual(1, errors.Count, string.Join(" / ", errors.ToArray()));
            StringAssert.Contains("endMs", errors[0]);
        }

        [Test]
        public void Validate_CatchesUnknownRoomAndUnknownNpc()
        {
            MovementTrackTable table = Table();
            table.tracks[1].npcId = "ghost";
            table.tracks[2].segments[0].room = "attic";

            List<string> errors = MovementTrackValidator.Validate(table, _layout, _roster, RunMs);

            Assert.AreEqual(2, errors.Count, string.Join(" / ", errors.ToArray()));
            StringAssert.Contains("ghost", errors[0]);
            StringAssert.Contains("attic", errors[1]);
        }

        [Test]
        public void Validate_CatchesDuplicateNpcAndEmptySegments()
        {
            var table = new MovementTrackTable
            {
                tracks = new[]
                {
                    new MovementTrack { npcId = Witness, segments = new[] { Segment(0, 1000, RoomA) } },
                    new MovementTrack { npcId = Witness, segments = new TrackSegment[0] }
                }
            };

            List<string> errors = MovementTrackValidator.Validate(table, _layout, _roster, RunMs);

            Assert.AreEqual(2, errors.Count, string.Join(" / ", errors.ToArray()));
            StringAssert.Contains("중복", errors[0]);
            StringAssert.Contains("구간이 하나도 없다", errors[1]);
        }

        [Test]
        public void Validate_CatchesSegmentPastTheEndOfTheRun()
        {
            MovementTrackTable table = Table();
            table.tracks[1].segments[0].endMs = RunMs + 1;

            List<string> errors = MovementTrackValidator.Validate(table, _layout, _roster, RunMs);

            Assert.AreEqual(1, errors.Count, string.Join(" / ", errors.ToArray()));
            StringAssert.Contains("회차 길이", errors[0]);

            // 회차 길이를 모르면 그 검사는 건너뛴다 — 트랙만 따로 볼 수 있어야 한다.
            CollectionAssert.IsEmpty(MovementTrackValidator.Validate(table, _layout, _roster, 0));
        }

        [Test]
        public void Validate_WithoutLayoutOrRoster_SkipsThoseChecks()
        {
            MovementTrackTable table = Table();
            table.tracks[1].npcId = "ghost";
            table.tracks[2].segments[0].room = "attic";

            CollectionAssert.IsEmpty(MovementTrackValidator.Validate(table, null, null, RunMs));
        }

        [Test]
        public void Validate_WithoutATable_SaysSo()
        {
            List<string> errors = MovementTrackValidator.Validate(null, _layout, _roster, RunMs);
            Assert.AreEqual(1, errors.Count);
        }

        // ── 대본과의 대조 ───────────────────────────────────────

        /// <summary>목소리 v1 = culprit, v2 = witness. 발화 하나하나가 말한 사람의 자리와 맞아야 한다.</summary>
        private static ScriptDefinition ScriptWith(params Utterance[] utterances)
        {
            return new ScriptDefinition
            {
                caseId = "movement_track_test",
                durationMs = RunMs,
                speakers = new[]
                {
                    new SpeakerDefinition { voiceId = "v1", npcId = Culprit },
                    new SpeakerDefinition { voiceId = "v2", npcId = Witness }
                },
                utterances = utterances
            }.Normalized();
        }

        private static Utterance Line(string id, int startMs, int durationMs, string voiceId, string room)
        {
            return new Utterance { id = id, startMs = startMs, durationMs = durationMs, voiceId = voiceId, room = room, text = "…", clip = "" };
        }

        [Test]
        public void ValidateAgainstScript_AcceptsSpeakersWhoAreActuallyThere()
        {
            ScriptDefinition script = ScriptWith(
                Line("u1", 1000, 500, "v1", RoomA),
                Line("u2", 5000, 500, "v1", RoomB),
                Line("u3", 9000, 500, "v2", RoomA));

            List<string> errors = MovementTrackValidator.ValidateAgainstScript(Tracks(), script);
            CollectionAssert.IsEmpty(errors, string.Join(" / ", errors.ToArray()));
        }

        [Test]
        public void ValidateAgainstScript_CatchesAVoiceComingFromTheWrongRoom()
        {
            ScriptDefinition script = ScriptWith(Line("u_bad", 5000, 500, "v1", RoomA));

            List<string> errors = MovementTrackValidator.ValidateAgainstScript(Tracks(), script);

            Assert.AreEqual(1, errors.Count, string.Join(" / ", errors.ToArray()));
            StringAssert.Contains("u_bad", errors[0]);
            StringAssert.Contains(RoomB + "에 있었다", errors[0], "어디에 있었는지까지 알려 줘야 고칠 수 있다");
        }

        [Test]
        public void ValidateAgainstScript_CatchesTalkingWhileWalking()
        {
            // 2900~3100ms: 말하는 도중에 a를 떠난다.
            ScriptDefinition script = ScriptWith(Line("u_walk", 2900, 200, "v1", RoomA));

            List<string> errors = MovementTrackValidator.ValidateAgainstScript(Tracks(), script);

            Assert.AreEqual(1, errors.Count, string.Join(" / ", errors.ToArray()));
            StringAssert.Contains("u_walk", errors[0]);
            StringAssert.Contains("이동 중", errors[0]);
        }

        [Test]
        public void ValidateAgainstScript_SkipsSpeakersWithoutATrack()
        {
            var tracks = new MovementTracks(new List<MovementTrack>
            {
                new MovementTrack { npcId = Culprit, segments = new[] { Segment(0, 3000, RoomA) } }
            });

            // v2(witness)는 트랙이 없다 — 트랙을 다 적기 전에도 대본을 검사할 수 있어야 한다.
            ScriptDefinition script = ScriptWith(
                Line("u1", 1000, 500, "v1", RoomA),
                Line("u2", 1000, 500, "v2", RoomStudy));

            CollectionAssert.IsEmpty(MovementTrackValidator.ValidateAgainstScript(tracks, script));
        }
    }
}
