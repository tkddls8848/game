using System.Collections.Generic;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// Phase U-1 — 대본 검증기와 연속 재생 장치(DEVELOPMENT_PLAN_UNHEARD.md Phase U-1).
    /// 파일을 읽지 않고 코드로 대본을 세워 검사한다. 실제 슬라이스 대본은 EavesdropTests가 본다.
    /// </summary>
    public class EavesdropPlaybackTests
    {
        // ── 대본 만들기 도우미 ───────────────────────────────────

        private static Utterance U(string id, int startMs, int durationMs, string voiceId, string room, string text)
        {
            return new Utterance
            {
                id = id, startMs = startMs, durationMs = durationMs,
                voiceId = voiceId, room = room, text = text, clip = string.Empty
            };
        }

        /// <summary>참조가 멀쩡한 최소 대본. 각 테스트가 한 군데씩 망가뜨려 검증기를 시험한다.</summary>
        private static ScriptDefinition Healthy()
        {
            return new ScriptDefinition
            {
                caseId = "test_case",
                durationMs = 30000,
                speakers = new[]
                {
                    new SpeakerDefinition { voiceId = "v1", npcId = "npc_a" },
                    new SpeakerDefinition { voiceId = "v2", npcId = "npc_b" }
                },
                utterances = new[]
                {
                    U("u1", 1000, 2000, "v1", "room_dining", "첫 마디"),
                    U("u2", 4000, 2000, "v2", "room_hall", "둘째 마디")
                },
                facts = new[]
                {
                    new ScriptFact { id = "f1", description = "사실 하나", utteranceIds = new[] { "u1" } },
                    new ScriptFact { id = "f2", description = "사실 둘", utteranceIds = new[] { "u2" } }
                },
                conclusions = new[]
                {
                    new ScriptConclusion { id = "c1", text = "결론", requiresFacts = new[] { "f1", "f2" } }
                }
            }.Normalized();
        }

        private static bool Mentions(List<string> errors, string fragment)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i].Contains(fragment)) return true;
            }
            return false;
        }

        // ── 검증기 ───────────────────────────────────────────────

        [Test]
        public void Validator_HealthyScript_HasNoErrors()
        {
            // layout·roster 없이도 대본 안의 참조(목소리·사실·결론)는 검사된다.
            Assert.IsEmpty(ScriptValidator.Validate(Healthy(), null, null));
        }

        [Test]
        public void Validator_NullScript_IsReported()
        {
            Assert.AreEqual(1, ScriptValidator.Validate(null, null, null).Count);
        }

        [Test]
        public void Validator_MissingDuration_IsCaught()
        {
            // JsonUtility는 빠진 int를 0으로 채운다. 그 0이 그대로 통과하면 회차 길이가 사라진다.
            ScriptDefinition script = Healthy();
            script.durationMs = 0;
            Assert.IsTrue(Mentions(ScriptValidator.Validate(script, null, null), "durationMs"));
        }

        [Test]
        public void Validator_UtteranceRunningPastTheEnd_IsCaught()
        {
            ScriptDefinition script = Healthy();
            script.utterances[1].startMs = 29000;
            script.utterances[1].durationMs = 5000; // 34000ms — 회차는 30000ms다
            Assert.IsTrue(Mentions(ScriptValidator.Validate(script, null, null), "회차 길이"));
        }

        [Test]
        public void Validator_DuplicateUtteranceId_IsCaught()
        {
            ScriptDefinition script = Healthy();
            script.utterances[1].id = "u1";
            Assert.IsTrue(Mentions(ScriptValidator.Validate(script, null, null), "중복"));
        }

        [Test]
        public void Validator_UndeclaredVoice_IsCaught()
        {
            ScriptDefinition script = Healthy();
            script.utterances[0].voiceId = "v9";
            Assert.IsTrue(Mentions(ScriptValidator.Validate(script, null, null), "speakers에 없다"));
        }

        [Test]
        public void Validator_DanglingFactAndConclusionReferences_AreCaught()
        {
            ScriptDefinition script = Healthy();
            script.facts[0].utteranceIds = new[] { "nope" };
            script.conclusions[0].requiresFacts = new[] { "f1", "missing" };

            List<string> errors = ScriptValidator.Validate(script, null, null);
            Assert.IsTrue(Mentions(errors, "없는 발화"));
            Assert.IsTrue(Mentions(errors, "없는 사실"));
        }

        [Test]
        public void Validator_NonPositiveDuration_IsCaught()
        {
            ScriptDefinition script = Healthy();
            script.utterances[0].durationMs = 0;
            Assert.IsTrue(Mentions(ScriptValidator.Validate(script, null, null), "durationMs가 0 이하"));
        }

        // ── 재생 장치 ────────────────────────────────────────────

        [Test]
        public void Transport_StartsPausedAtZero()
        {
            var t = new PlaybackTransport(90000);
            Assert.AreEqual(TransportState.Paused, t.State);
            Assert.AreEqual(0, t.PositionMs);
            Assert.AreEqual(90000, t.DurationMs);
            Assert.AreEqual(0, t.Advance(1000), "멈춰 있으면 시간이 흐르지 않는다");
        }

        [Test]
        public void Transport_PlayAdvancesAndPauseHolds()
        {
            var t = new PlaybackTransport(90000);
            t.Play();
            t.Advance(500);
            t.Advance(500);
            Assert.AreEqual(1000, t.PositionMs);

            t.Pause();
            t.Advance(5000);
            Assert.AreEqual(1000, t.PositionMs, "정지 중에는 위치가 유지된다");
        }

        [Test]
        public void Transport_StopsAtTheEnd_AndDoesNotLoop()
        {
            var t = new PlaybackTransport(1000);
            t.Play();
            t.Advance(5000);

            Assert.AreEqual(1000, t.PositionMs);
            Assert.AreEqual(TransportState.Ended, t.State);
            Assert.IsTrue(t.AtEnd);
            Assert.AreEqual(0, t.Advance(1000), "끝난 뒤에는 저절로 처음으로 돌아가지 않는다");
        }

        [Test]
        public void Transport_PlayAtTheEnd_RewindsFirst()
        {
            var t = new PlaybackTransport(1000);
            t.Play();
            t.Advance(5000);

            t.Play();
            Assert.AreEqual(0, t.PositionMs);
            Assert.AreEqual(TransportState.Playing, t.State);
        }

        [Test]
        public void Transport_SeekIsClampedToTheRun()
        {
            var t = new PlaybackTransport(90000);
            t.SeekTo(-5000);
            Assert.AreEqual(0, t.PositionMs);

            t.SeekTo(999999);
            Assert.AreEqual(90000, t.PositionMs);

            t.SeekBy(-10000);
            Assert.AreEqual(80000, t.PositionMs);
        }

        [Test]
        public void Transport_RestartRewindsAndPauses()
        {
            var t = new PlaybackTransport(90000);
            t.Play();
            t.Advance(30000);
            t.Restart();

            Assert.AreEqual(0, t.PositionMs);
            Assert.AreEqual(TransportState.Paused, t.State);
        }

        [Test]
        public void Transport_HalfSpeed_DoesNotDriftOverManySmallSteps()
        {
            // 0.5배속으로 1ms를 300번. 실수로 곱하면 여기서 오차가 쌓인다.
            var t = new PlaybackTransport(90000);
            t.SpeedPercent = 50;
            t.Play();
            for (int i = 0; i < 300; i++) t.Advance(1);

            Assert.AreEqual(150, t.PositionMs);
        }

        [Test]
        public void Transport_DoubleSpeed_CoversTwiceTheGround()
        {
            var t = new PlaybackTransport(90000);
            t.SpeedPercent = 200;
            t.Play();
            t.Advance(1000);

            Assert.AreEqual(2000, t.PositionMs);
        }

        [Test]
        public void Transport_SpeedIsClamped()
        {
            var t = new PlaybackTransport(90000);
            t.SpeedPercent = 0;
            Assert.AreEqual(10, t.SpeedPercent);

            t.SpeedPercent = 100000;
            Assert.AreEqual(800, t.SpeedPercent);
        }

        [Test]
        public void Transport_ThirdSpeed_AccumulatesRemainderExactly()
        {
            // 1/3 배속은 나머지가 반드시 남는다. 1ms씩 3번이면 딱 1ms 흘러야 한다.
            var t = new PlaybackTransport(90000);
            t.SpeedPercent = 33;
            t.Play();
            t.Advance(1);
            Assert.AreEqual(0, t.PositionMs, "33/100ms는 아직 1ms가 안 된다");
            t.Advance(1);
            t.Advance(1);
            Assert.AreEqual(0, t.PositionMs, "99/100ms도 아직 1ms가 아니다");
            t.Advance(1);
            Assert.AreEqual(1, t.PositionMs, "132/100ms에서 1ms가 된다");
        }
    }
}
