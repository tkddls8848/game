using System.Collections.Generic;
using System.IO;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 거리감을 검사한다.
    ///
    /// <b>가장 중요한 것부터</b>: 거리는 음량·정위·음색만 바꾸고 <i>알아듣는지</i>는 바꾸지 않는다.
    /// <see cref="SliceSolvability"/>가 "한 방에 고정되면 전부는 못 듣는다"를 방 기준으로 증명해
    /// 뒀으므로, 거리 때문에 같은 방 대사가 안 들리게 되면 그 증명이 조용히 무효가 된다 —
    /// 게임은 멀쩡히 돌고 "풀 수 있다"는 보장만 사라진다. 그래서 바닥값을 여기서 못박는다.
    /// </summary>
    public class VoiceMixTests
    {
        private const string RoomsPath = "Assets/Resources/GameData/rooms.json";
        private const string TracksPath = "Assets/Resources/GameData/cases/case_02/tracks.json";

        private RoomLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _layout = RoomLayout.FromTable(FromJson<RoomTable>(ReadProjectFile(RoomsPath)));
        }

        // ── 지켜야 하는 선 ─────────────────────────────────────

        [Test]
        public void SameRoom_NeverFallsBelowTheFloor()
        {
            // 방 구석에 서 있어도, 심지어 판정 거리를 넘겨도 같은 방 소리는 들려야 한다.
            foreach (float distance in new[] { 0f, 3f, 8f, 15f, 40f, 1000f })
            {
                VoiceMixLevel mix = VoiceMix.For(0f, 0f, distance, 0f, Audibility.Full);
                Assert.GreaterOrEqual(mix.Gain, VoiceMix.SameRoomFloor,
                    distance + " 유닛에서 같은 방 소리가 바닥 밑으로 내려갔다 — 풀 수 있다는 보장이 깨진다");
            }
        }

        [Test]
        public void None_IsAlwaysSilent()
        {
            VoiceMixLevel mix = VoiceMix.For(0f, 0f, 0.1f, 0f, Audibility.None);
            Assert.IsTrue(mix.IsSilent, "들리지 않는다고 판정된 소리는 코앞이라도 울리지 않는다");
        }

        // ── 거리 ───────────────────────────────────────────────

        [Test]
        public void CloseUp_IsFullVolume()
        {
            VoiceMixLevel mix = VoiceMix.For(0f, 0f, 1f, 0f, Audibility.Full);
            Assert.AreEqual(1f, mix.Gain, 0.001f, "대화 거리 안에서는 감쇠가 없다");
        }

        [Test]
        public void FartherIsQuieter()
        {
            float previous = 2f;
            foreach (float distance in new[] { 2f, 4f, 7f, 11f, 16f })
            {
                float gain = VoiceMix.For(0f, 0f, distance, 0f, Audibility.Full).Gain;
                Assert.Less(gain, previous, distance + " 유닛이 더 가까운 곳보다 크게 들린다");
                previous = gain;
            }
        }

        [Test]
        public void ThroughWall_IsQuieterThanSameRoomAtTheSameDistance()
        {
            float same = VoiceMix.For(0f, 0f, 4f, 0f, Audibility.Full).Gain;
            float wall = VoiceMix.For(0f, 0f, 4f, 0f, Audibility.Muffled).Gain;
            Assert.Less(wall, same, "같은 거리라면 벽이 있는 쪽이 작아야 한다");
        }

        [Test]
        public void ThroughWall_LoseHighFrequencies_AndMoreWithDistance()
        {
            VoiceMixLevel near = VoiceMix.For(0f, 0f, 1f, 0f, Audibility.Muffled);
            VoiceMixLevel far = VoiceMix.For(0f, 0f, 12f, 0f, Audibility.Muffled);

            Assert.Less(near.LowPassHz, VoiceMix.OpenLowPassHz, "벽 너머는 고음이 깎여야 한다");
            Assert.Less(far.LowPassHz, near.LowPassHz, "멀어지면 더 깎여 웅얼거림에 가까워진다");
            Assert.GreaterOrEqual(far.LowPassHz, VoiceMix.MuffledFarHz);
        }

        [Test]
        public void SameRoom_KeepsItsHighFrequencies()
        {
            VoiceMixLevel mix = VoiceMix.For(0f, 0f, 6f, 0f, Audibility.Full);
            Assert.AreEqual(VoiceMix.OpenLowPassHz, mix.LowPassHz,
                "같은 방이면 멀어도 음색은 그대로다 — 작게 들릴 뿐이다");
        }

        // ── 정위 ───────────────────────────────────────────────

        [Test]
        public void SourceOnTheRight_PansRight()
        {
            Assert.Greater(VoiceMix.For(0f, 0f, 4f, 0f, Audibility.Full).Pan, 0f);
            Assert.Less(VoiceMix.For(0f, 0f, -4f, 0f, Audibility.Full).Pan, 0f);
        }

        [Test]
        public void DirectlyAheadOrBehind_IsCentred()
        {
            // 2D 위에서 내려다보는 게임이라 위아래는 정위로 나타낼 수 없다.
            Assert.AreEqual(0f, VoiceMix.For(0f, 0f, 0f, 6f, Audibility.Full).Pan, 0.001f);
        }

        [Test]
        public void Pan_NeverEmptiesOneEar()
        {
            VoiceMixLevel far = VoiceMix.For(0f, 0f, 100f, 0f, Audibility.Full);
            Assert.LessOrEqual(far.Pan, VoiceMix.MaxPan, "한쪽 귀가 완전히 비면 부자연스럽다");
        }

        [Test]
        public void Decibels_ClampInsteadOfGoingToNegativeInfinity()
        {
            Assert.AreEqual(-80f, VoiceMix.ToDecibels(0f), 0.001f);
            Assert.AreEqual(0f, VoiceMix.ToDecibels(1f), 0.01f);
            Assert.Less(VoiceMix.ToDecibels(0.5f), 0f);
        }

        // ── 누가 어디 있는가 ───────────────────────────────────

        [Test]
        public void VoiceIsPlacedWhereItsNpcStands()
        {
            MovementTracks tracks = LoadTracks();
            ScriptDefinition script = ScriptWithSpeaker("v1", "npc_a");
            var positions = new SpeakerPositions(script, tracks, _layout);

            // npc_a는 회차 시작에 식당에 있다(tracks.json).
            Assert.AreEqual("npc_a", positions.NpcOf("v1"));
            Assert.IsTrue(positions.TryGetVoicePoint("v1", "room_dining", 1000, out float x, out float y));

            Assert.IsTrue(_layout.TryGetRoomCenter("room_dining", out float cx, out float cy));
            Assert.AreEqual(cx, x, 0.01f);
            Assert.AreEqual(cy, y, 0.01f);
        }

        [Test]
        public void WhenTrackDisagreesWithTheScript_TheScriptWins()
        {
            // 발화에 적힌 방이 가청 판정의 근거다. 트랙을 믿고 위치를 잡으면
            // "옆 방에서 들린다고 판정된 소리가 이 방 한가운데서 난다"는 모순이 생긴다.
            MovementTracks tracks = LoadTracks();
            ScriptDefinition script = ScriptWithSpeaker("v1", "npc_a");
            var positions = new SpeakerPositions(script, tracks, _layout);

            // npc_a는 1초에 식당에 있는데, 발화는 서재에서 났다고 적혀 있다.
            Assert.IsTrue(positions.TryGetVoicePoint("v1", "room_victim", 1000, out float x, out float y));

            Assert.IsTrue(_layout.TryGetRoomCenter("room_victim", out float vx, out float vy));
            Assert.AreEqual(vx, x, 0.01f, "발화에 적힌 방을 따라야 한다");
            Assert.AreEqual(vy, y, 0.01f);
        }

        [Test]
        public void NoTracks_StillPlacesTheVoiceInItsRoom()
        {
            var positions = new SpeakerPositions(ScriptWithSpeaker("v1", "npc_a"), null, _layout);
            Assert.IsTrue(positions.TryGetVoicePoint("v1", "room_lobby", 0, out float x, out float y),
                "트랙이 없어도 방 중심으로 물러설 수 있어야 한다");
            Assert.IsTrue(_layout.TryGetRoomCenter("room_lobby", out float cx, out float cy));
            Assert.AreEqual(cx, x, 0.01f);
        }

        [Test]
        public void WalkingPast_MovesTheSound()
        {
            // 복도를 지나가는 사람의 소리는 실제로 스쳐 지나가야 한다.
            MovementTracks tracks = LoadTracks();
            var positions = new SpeakerPositions(ScriptWithSpeaker("v1", "npc_b"), tracks, _layout);

            // npc_b는 182초에 식당을 떠나 206초에 창고에 든다 — 그 사이가 이동 중이다.
            Assert.IsTrue(positions.TryGetPoint("npc_b", string.Empty, 188000, out float x1, out float y1));
            Assert.IsTrue(positions.TryGetPoint("npc_b", string.Empty, 200000, out float x2, out float y2));

            float moved = (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
            Assert.Greater(moved, 0.01f, "이동 중에는 위치가 실제로 변해야 한다");
        }

        [Test]
        public void Mix_AppliesDistanceToEveryPerceivedLine()
        {
            var positions = new SpeakerPositions(ScriptWithSpeaker("v1", "npc_a"), null, _layout);
            Assert.IsTrue(_layout.TryGetRoomCenter("room_dining", out float cx, out float cy));

            var perceived = new List<PerceivedUtterance>
            {
                new PerceivedUtterance { Level = Audibility.Full, UtteranceId = "u1",
                                         Room = "room_dining", VoiceId = "v1", Text = "가까이" }
            };

            List<MixedUtterance> near = positions.Mix(perceived, 0, cx, cy);
            List<MixedUtterance> far = positions.Mix(perceived, 0, cx + 12f, cy);

            Assert.AreEqual(1, near.Count);
            Assert.Greater(near[0].Mix.Gain, far[0].Mix.Gain,
                "청취점이 멀어지면 같은 대사도 작게 들려야 한다");
        }

        [Test]
        public void Mix_NeverDropsALineItCannotPlace()
        {
            // 자리를 못 찾아도 소리를 잃어서는 안 된다 — 들리는 판정이 났으면 들려야 한다.
            var positions = new SpeakerPositions(null, null, _layout);
            var perceived = new List<PerceivedUtterance>
            {
                new PerceivedUtterance { Level = Audibility.Full, UtteranceId = "u1",
                                         Room = "없는_방", VoiceId = "v9", Text = "어딘가" }
            };

            List<MixedUtterance> mixed = positions.Mix(perceived, 0, 0f, 0f);
            Assert.AreEqual(1, mixed.Count);
            Assert.Greater(mixed[0].Mix.Gain, 0f);
        }

        // ── 도우미 ─────────────────────────────────────────────

        private MovementTracks LoadTracks()
        {
            return MovementTracks.FromTable(FromJson<MovementTrackTable>(ReadProjectFile(TracksPath)));
        }

        private static ScriptDefinition ScriptWithSpeaker(string voiceId, string npcId)
        {
            return new ScriptDefinition
            {
                caseId = "voicemix_test",
                durationMs = 600000,
                speakers = new[] { new SpeakerDefinition { voiceId = voiceId, npcId = npcId } },
                utterances = new Utterance[0]
            }.Normalized();
        }

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
