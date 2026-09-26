using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 회차를 영상처럼 다루게 한 뒤의 규칙을 못박는다.
    ///
    /// 뒤로 재생에서 조용히 깨질 수 있는 지점이 둘 있다.
    ///   1. 되감는 동안 스쳐 간 말을 "들었다"고 쳐 주면 되돌려 들을 이유가 사라진다.
    ///      게임은 멀쩡히 돌고 퍼즐만 없어지므로 아무도 눈치채지 못한다.
    ///   2. 처음(0)에 닿았을 때 끝(Ended)과 같이 취급하면, 뒤로 감아 0에 닿은 순간
    ///      "다 봤다"가 되어 다음 재생이 엉뚱하게 처음으로 점프한다.
    /// </summary>
    public class ReversePlaybackTests
    {
        private const int Duration = 60000;
        private const string RoomA = "a";

        /// <summary>a에서 한 번 말하는 짧은 회차. 되감기 판정에는 이 한 줄로 충분하다.</summary>
        private static ListeningSession NewSession()
        {
            RoomLayout layout = RoomLayout.FromTable(TestCaseFactory.Rooms());
            var script = new ScriptDefinition
            {
                caseId = "reverse_test",
                durationMs = 20000,
                speakers = new[] { new SpeakerDefinition { voiceId = "v1", npcId = "npc_a" } },
                utterances = new[]
                {
                    new Utterance { id = "u1", startMs = 3000, durationMs = 3000,
                                    voiceId = "v1", room = RoomA, text = "되감기 시험용 대사", clip = "" }
                }
            }.Normalized();
            return new ListeningSession(new ScriptTimeline(script), new AudibilityModel(layout));
        }

        private static PlaybackTransport At(int ms)
        {
            var transport = new PlaybackTransport(Duration);
            transport.SeekTo(ms);
            return transport;
        }

        // ── 방향 ───────────────────────────────────────────────

        [Test]
        public void Forward_IsTheDefault()
        {
            Assert.AreEqual(PlayDirection.Forward, new PlaybackTransport(Duration).Direction);
        }

        [Test]
        public void Backward_MovesBackward_AndReturnsNegative()
        {
            PlaybackTransport transport = At(30000);
            transport.Play(PlayDirection.Backward);

            int moved = transport.Advance(1000);

            Assert.AreEqual(-1000, moved, "뒤로 흘렀으면 돌려주는 값도 음수여야 한다");
            Assert.AreEqual(29000, transport.PositionMs);
        }

        [Test]
        public void Backward_StopsAtStart_AsPaused_NotEnded()
        {
            PlaybackTransport transport = At(500);
            transport.Play(PlayDirection.Backward);

            transport.Advance(2000);

            Assert.AreEqual(0, transport.PositionMs);
            Assert.AreEqual(TransportState.Paused, transport.State,
                "시작점은 '다 봤다'가 아니다 — Ended가 되면 다음 재생이 엉뚱하게 튄다");
        }

        [Test]
        public void Forward_StillEndsAtTheEnd()
        {
            PlaybackTransport transport = At(Duration - 500);
            transport.Play();

            transport.Advance(2000);

            Assert.AreEqual(Duration, transport.PositionMs);
            Assert.AreEqual(TransportState.Ended, transport.State);
        }

        [Test]
        public void AtEnd_PlayingForward_Restarts_ButBackwardDoesNot()
        {
            PlaybackTransport forward = At(Duration);
            forward.Play(PlayDirection.Forward);
            Assert.AreEqual(0, forward.PositionMs, "끝에서 앞으로 재생은 처음부터 다시 흐른다");

            PlaybackTransport backward = At(Duration);
            backward.Play(PlayDirection.Backward);
            Assert.AreEqual(Duration, backward.PositionMs,
                "끝에서 뒤로 돌리는 것은 자연스러운 동작이다 — 되감지 말아야 한다");
            Assert.IsTrue(backward.IsRewinding);
        }

        [Test]
        public void SwitchingDirection_AtEnd_ClearsEndedState()
        {
            // 끝 바로 앞에서 앞으로 흘려 Ended에 닿게 한다.
            // (끝에서 Play()를 부르면 처음으로 되감기므로 그 길로는 Ended가 되지 않는다.)
            PlaybackTransport transport = At(Duration - 50);
            transport.Play(PlayDirection.Forward);
            transport.Advance(200);
            Assert.AreEqual(TransportState.Ended, transport.State, "출발 상태를 맞춘다");

            transport.Direction = PlayDirection.Backward;

            Assert.AreNotEqual(TransportState.Ended, transport.State,
                "뒤로는 아직 갈 곳이 있으므로 Ended에 묶여 있으면 안 된다");
        }

        // ── 배속과 드리프트 ────────────────────────────────────

        [Test]
        public void Backward_HalfSpeed_DoesNotDrift()
        {
            PlaybackTransport transport = At(30000);
            transport.SpeedPercent = 50;
            transport.Play(PlayDirection.Backward);

            for (int i = 0; i < 300; i++) transport.Advance(1);

            Assert.AreEqual(30000 - 150, transport.PositionMs,
                "0.5배속으로 1ms씩 300번이면 정확히 150ms다 — 나머지 누적이 깨지면 여기서 어긋난다");
        }

        [Test]
        public void SignedSpeed_ShowsDirection()
        {
            PlaybackTransport transport = At(10000);
            transport.SpeedPercent = 200;
            Assert.AreEqual(200, transport.SignedSpeedPercent);

            transport.Direction = PlayDirection.Backward;
            Assert.AreEqual(-200, transport.SignedSpeedPercent, "뒤로 2배속은 -200으로 읽힌다");
        }

        [Test]
        public void SpeedPresets_AreOrderedAndContainNormal()
        {
            int[] presets = PlaybackTransport.SpeedPresets;
            Assert.Contains(PlaybackTransport.NormalSpeedPercent, presets);
            for (int i = 1; i < presets.Length; i++)
                Assert.Less(presets[i - 1], presets[i], "단계는 오름차순이어야 UI가 순환할 수 있다");
        }

        // ── 들은 기록 (조용히 깨지는 지점) ─────────────────────

        [Test]
        public void Rewinding_DoesNotCountAsHeard()
        {
            ListeningSession session = NewSession();
            Utterance first = session.Timeline.Utterances[0];

            session.MoveTo(first.room);

            // 그 발화 뒤로 건너뛴 다음, 되감아 통과시킨다.
            session.SeekTo(first.EndMs + 2000);
            session.ForgetHeard();
            Assert.AreEqual(Audibility.None, session.HeardLevel(first.id), "출발선을 맞춘다");

            session.Transport.Play(PlayDirection.Backward);
            for (int i = 0; i < 400; i++) session.Advance(16);   // 발화를 거꾸로 통과

            Assert.AreEqual(Audibility.None, session.HeardLevel(first.id),
                "거꾸로 흐르는 말은 알아들을 수 없다 — 들은 것으로 치면 되돌려 들을 이유가 사라진다");
        }

        [Test]
        public void PlayingForward_StillCountsAsHeard()
        {
            // 위 테스트가 기록을 아예 막아 버리는 식으로 통과하지 않는다는 것을 못박는다.
            ListeningSession session = NewSession();
            Utterance first = session.Timeline.Utterances[0];

            session.MoveTo(first.room);
            session.SeekTo(first.startMs);
            session.ForgetHeard();

            session.Transport.Play(PlayDirection.Forward);
            session.Advance(16);

            Assert.AreEqual(Audibility.Full, session.HeardLevel(first.id));
        }

        [Test]
        public void Rewinding_StillShowsWhatIsSounding()
        {
            // 기록은 안 하지만 화면은 살아 있어야 한다 — 뭘 지나치는지 보여야 되감을 수 있다.
            ListeningSession session = NewSession();
            Utterance first = session.Timeline.Utterances[0];

            session.MoveTo(first.room);
            session.SeekTo(first.EndMs - 10);
            session.Transport.Play(PlayDirection.Backward);
            session.Advance(16);

            Assert.IsNotEmpty(session.Current, "되감는 중에도 지금 울리는 것은 보여야 한다");
        }
    }
}
