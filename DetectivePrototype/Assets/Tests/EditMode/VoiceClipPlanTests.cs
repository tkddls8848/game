using System.Collections.Generic;
using System.IO;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 음성을 얹을 때 대본 타이밍이 무너지지 않는지 보는 안전장치를 검사한다.
    ///
    /// 이 게임의 전제("한 회차로는 전부 들을 수 없다")는 작성된 durationMs에 대해 증명돼 있다.
    /// TTS가 뱉는 실제 길이는 그 값과 다르므로, 음성을 넣는 순간 겹침이 밀리면서 전제가
    /// 깨질 수 있다. 깨져도 게임은 멀쩡히 돌아가고 로그도 남지 않는다 — 되돌려 들을 이유만
    /// 조용히 사라진다. 그래서 장치가 실제로 잡아내는지를 여기서 못박는다.
    ///
    /// 검사는 실제 case_02 대본에 대고 한다. 만들어 낸 예제로는 "진짜 대본에서도 잡히는가"를
    /// 보증하지 못한다.
    /// </summary>
    public class VoiceClipPlanTests
    {
        private const string RoomsPath = "Assets/Resources/GameData/rooms.json";
        private const string ScriptPath = "Assets/Resources/GameData/cases/case_02/script.json";

        private ScriptDefinition _script;
        private RoomLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _layout = RoomLayout.FromTable(FromJson<RoomTable>(ReadProjectFile(RoomsPath)));
            _script = FromJson<ScriptDefinition>(ReadProjectFile(ScriptPath)).Normalized();
        }

        private Dictionary<string, int> AuthoredDurations()
        {
            var map = new Dictionary<string, int>();
            for (int i = 0; i < _script.utterances.Length; i++)
                map[_script.utterances[i].id] = _script.utterances[i].durationMs;
            return map;
        }

        // ── 자리 규칙 ───────────────────────────────────────────

        [Test]
        public void ClipPath_IsPerCaseAndPerUtterance()
        {
            Assert.AreEqual("Audio/Voice/case_02/u01", VoiceClipPlan.ClipPath("case_02", "u01"));
            Assert.AreEqual(string.Empty, VoiceClipPlan.ClipPath("case_02", null));
        }

        [Test]
        public void EveryUtteranceStillWaitsForItsClip()
        {
            // 음성은 아직 없다. 이 수가 줄어드는 것이 음성 작업의 진행도다.
            List<string> missing = VoiceClipPlan.UtterancesWithoutClip(_script);
            Assert.AreEqual(_script.utterances.Length, missing.Count);
        }

        // ── 재조정 ─────────────────────────────────────────────

        [Test]
        public void WithMeasuredDurations_LeavesTheOriginalAlone()
        {
            int before = _script.utterances[0].durationMs;
            var measured = new Dictionary<string, int> { { _script.utterances[0].id, before + 5000 } };

            ScriptDefinition adjusted = VoiceClipPlan.WithMeasuredDurations(_script, measured);

            Assert.AreEqual(before, _script.utterances[0].durationMs, "원본이 바뀌면 안 된다");
            Assert.AreEqual(before + 5000, adjusted.utterances[0].durationMs);
            Assert.AreEqual(_script.utterances[0].startMs, adjusted.utterances[0].startMs,
                "startMs는 작가가 설계한 값이라 건드리지 않는다");
        }

        [Test]
        public void AuthoredDurations_AreSafe()
        {
            // 작성된 길이 그대로면 당연히 통과해야 한다. 이게 깨지면 기준선이 무너진 것이다.
            ClipTimingReport report = VoiceClipPlan.Check(_script, AuthoredDurations(), _layout);

            Assert.IsEmpty(report.RunsPastEnd, string.Join(" / ", report.RunsPastEnd.ToArray()));
            Assert.IsEmpty(report.SelfOverlaps, string.Join(" / ", report.SelfOverlaps.ToArray()));
            Assert.IsEmpty(report.SolvabilityProblems, string.Join(" / ", report.SolvabilityProblems.ToArray()));
            Assert.IsEmpty(report.Unmeasured);
            Assert.IsTrue(report.IsSafe);
        }

        [Test]
        public void MissingMeasurements_AreReported_NotSilentlyAssumed()
        {
            ClipTimingReport report = VoiceClipPlan.Check(_script, new Dictionary<string, int>(), _layout);
            Assert.AreEqual(_script.utterances.Length, report.Unmeasured.Count,
                "길이를 재지 못한 발화는 조용히 넘어가지 말고 드러나야 한다");
        }

        // ── 실제로 잡아내는가 ───────────────────────────────────

        [Test]
        public void AudioRunningPastTheEnd_IsCaught()
        {
            Dictionary<string, int> measured = AuthoredDurations();
            Utterance last = _script.utterances[0];
            for (int i = 0; i < _script.utterances.Length; i++)
                if (_script.utterances[i].startMs > last.startMs) last = _script.utterances[i];

            measured[last.id] = _script.durationMs; // 회차 끝을 훌쩍 넘기게

            ClipTimingReport report = VoiceClipPlan.Check(_script, measured, _layout);
            Assert.IsNotEmpty(report.RunsPastEnd);
            Assert.IsFalse(report.IsSafe);
        }

        [Test]
        public void OneVoiceTalkingOverItself_IsCaught()
        {
            // 같은 목소리의 이웃한 두 발화를 찾아, 앞엣것을 뒤엣것까지 덮도록 늘린다.
            Dictionary<string, int> measured = AuthoredDurations();
            var byVoice = new Dictionary<string, List<Utterance>>();
            for (int i = 0; i < _script.utterances.Length; i++)
            {
                Utterance u = _script.utterances[i];
                List<Utterance> list;
                if (!byVoice.TryGetValue(u.voiceId, out list)) byVoice[u.voiceId] = list = new List<Utterance>();
                list.Add(u);
            }
            bool stretched = false;
            foreach (KeyValuePair<string, List<Utterance>> pair in byVoice)
            {
                List<Utterance> list = pair.Value;
                list.Sort((a, b) => a.startMs.CompareTo(b.startMs));
                for (int i = 1; i < list.Count && !stretched; i++)
                {
                    measured[list[i - 1].id] = list[i].startMs - list[i - 1].startMs + 1000;
                    stretched = true;
                }
                if (stretched) break;
            }
            Assert.IsTrue(stretched, "같은 목소리의 발화가 둘 이상 있어야 한다");

            ClipTimingReport report = VoiceClipPlan.Check(_script, measured, _layout);
            Assert.IsNotEmpty(report.SelfOverlaps);
            Assert.IsFalse(report.IsSafe);
        }

        [Test]
        public void GrowthAndShrink_AreMeasured()
        {
            Dictionary<string, int> measured = AuthoredDurations();
            string first = _script.utterances[0].id;
            measured[first] = _script.utterances[0].durationMs + 700;

            ClipTimingReport report = VoiceClipPlan.Check(_script, measured, _layout);
            Assert.AreEqual(700, report.LargestGrowthMs, "얼마나 밀렸는지 사람이 볼 수 있어야 한다");
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
