using System.Collections.Generic;
using Detective.Core;
using Detective.Data;

namespace Detective.Eavesdrop
{
    /// <summary>재조정 결과. 문제가 하나라도 있으면 그 음성을 그대로 실으면 안 된다.</summary>
    public sealed class ClipTimingReport
    {
        /// <summary>회차 끝을 넘겨 버린 발화.</summary>
        public readonly List<string> RunsPastEnd = new List<string>();

        /// <summary>같은 목소리가 자기 자신과 겹치게 된 발화 쌍.</summary>
        public readonly List<string> SelfOverlaps = new List<string>();

        /// <summary>길이가 바뀌면서 §5 보장이 깨진 내용.</summary>
        public readonly List<string> SolvabilityProblems = new List<string>();

        /// <summary>실제 길이를 재지 못한(음성이 아직 없는) 발화.</summary>
        public readonly List<string> Unmeasured = new List<string>();

        /// <summary>가장 크게 늘어난/줄어든 폭(ms). 사람이 감으로 확인할 때 쓴다.</summary>
        public int LargestGrowthMs;
        public int LargestShrinkMs;

        public bool IsSafe
        {
            get
            {
                return RunsPastEnd.Count == 0
                    && SelfOverlaps.Count == 0
                    && SolvabilityProblems.Count == 0;
            }
        }
    }

    /// <summary>
    /// 음성 파일의 자리와, 음성이 들어왔을 때 대본 타이밍이 무너지지 않는지 보는 검사.
    ///
    /// 이 게임의 전제("한 회차로는 전부 들을 수 없다")는 <b>작성된 타이밍</b>에 대해 증명돼 있다.
    /// TTS가 뱉는 실제 길이는 작가가 적은 durationMs와 다르므로, 음성을 얹는 순간
    /// 겹침이 밀리면서 그 전제가 조용히 깨질 수 있다. 깨지면 되돌려 들을 이유가 사라지는데
    /// 게임은 멀쩡히 돌아가므로 아무도 눈치채지 못한다. 그래서 기계로 잡는다.
    ///
    /// startMs는 건드리지 않는다 — 누가 언제 말을 꺼내는지는 작가가 설계한 것이다.
    /// 바뀌는 것은 durationMs뿐이다.
    /// </summary>
    public static class VoiceClipPlan
    {
        /// <summary>음성 파일이 놓이는 Resources 폴더.</summary>
        public const string RootFolder = "Audio/Voice";

        /// <summary>발화 하나의 음성 경로(Resources 기준, 확장자 없음).</summary>
        public static string ClipPath(string caseId, string utteranceId)
        {
            if (string.IsNullOrEmpty(caseId) || string.IsNullOrEmpty(utteranceId)) return string.Empty;
            return RootFolder + "/" + caseId + "/" + utteranceId;
        }

        /// <summary>아직 음성이 붙지 않은 발화 id. 진행도를 세는 데 쓴다.</summary>
        public static List<string> UtterancesWithoutClip(ScriptDefinition script)
        {
            var result = new List<string>();
            if (script == null) return result;
            script.Normalized();
            for (int i = 0; i < script.utterances.Length; i++)
            {
                if (string.IsNullOrEmpty(script.utterances[i].clip)) result.Add(script.utterances[i].id);
            }
            return result;
        }

        /// <summary>
        /// 실제로 잰 음성 길이를 대본에 반영한 사본을 만든다. 원본은 건드리지 않는다.
        /// measuredMs에 없는 발화는 작성된 길이를 그대로 쓴다.
        /// </summary>
        public static ScriptDefinition WithMeasuredDurations(ScriptDefinition script, IDictionary<string, int> measuredMs)
        {
            if (script == null) return null;
            script.Normalized();

            var copy = new ScriptDefinition
            {
                caseId = script.caseId,
                durationMs = script.durationMs,
                speakers = script.speakers,
                facts = script.facts,
                conclusions = script.conclusions,
                utterances = new Utterance[script.utterances.Length]
            };

            for (int i = 0; i < script.utterances.Length; i++)
            {
                Utterance u = script.utterances[i];
                int duration = u.durationMs;
                int measured;
                if (measuredMs != null && measuredMs.TryGetValue(u.id, out measured) && measured > 0) duration = measured;

                copy.utterances[i] = new Utterance
                {
                    id = u.id,
                    startMs = u.startMs,
                    durationMs = duration,
                    voiceId = u.voiceId,
                    room = u.room,
                    text = u.text,
                    clip = u.clip
                };
            }
            return copy.Normalized();
        }

        /// <summary>
        /// 실제 음성 길이로 바꿔도 대본이 성립하는지 본다.
        /// IsSafe가 false면 그 음성을 싣지 말고, 대본의 startMs를 다시 짜거나 음성을 다시 뽑아야 한다.
        /// </summary>
        public static ClipTimingReport Check(ScriptDefinition script, IDictionary<string, int> measuredMs, RoomLayout layout)
        {
            var report = new ClipTimingReport();
            if (script == null) { report.SolvabilityProblems.Add("대본이 없다"); return report; }
            script.Normalized();

            for (int i = 0; i < script.utterances.Length; i++)
            {
                string id = script.utterances[i].id;
                if (measuredMs == null || !measuredMs.ContainsKey(id)) report.Unmeasured.Add(id);
            }

            ScriptDefinition adjusted = WithMeasuredDurations(script, measuredMs);

            // 1) 회차 밖으로 삐져나가는가
            for (int i = 0; i < adjusted.utterances.Length; i++)
            {
                Utterance u = adjusted.utterances[i];
                if (u.startMs + u.durationMs > adjusted.durationMs)
                    report.RunsPastEnd.Add(u.id + " (" + (u.startMs + u.durationMs) + "ms > 회차 " + adjusted.durationMs + "ms)");
            }

            // 2) 같은 목소리가 자기 자신과 겹치는가 — 한 사람이 동시에 두 말을 할 수는 없다
            var byVoice = new Dictionary<string, List<Utterance>>();
            for (int i = 0; i < adjusted.utterances.Length; i++)
            {
                Utterance u = adjusted.utterances[i];
                List<Utterance> list;
                if (!byVoice.TryGetValue(u.voiceId, out list)) byVoice[u.voiceId] = list = new List<Utterance>();
                list.Add(u);
            }
            foreach (KeyValuePair<string, List<Utterance>> pair in byVoice)
            {
                List<Utterance> list = pair.Value;
                list.Sort((a, b) => a.startMs.CompareTo(b.startMs));
                for (int i = 1; i < list.Count; i++)
                {
                    if (list[i - 1].startMs + list[i - 1].durationMs > list[i].startMs)
                        report.SelfOverlaps.Add(pair.Key + ": " + list[i - 1].id + " 가 " + list[i].id + " 를 덮는다");
                }
            }

            // 3) 길이가 바뀌어도 §5가 성립하는가 — 여기가 조용히 깨지는 곳이다
            if (layout != null)
            {
                SliceSolvabilityReport solvability = SliceSolvability.Check(adjusted, layout);
                for (int i = 0; i < solvability.Problems.Count; i++) report.SolvabilityProblems.Add(solvability.Problems[i]);
                if (!solvability.EveryFactAudibleSomewhere)
                    report.SolvabilityProblems.Add("길이가 바뀌면서 어디서도 온전히 들리지 않는 사실이 생겼다");
                if (!solvability.NoSingleRoomSuffices)
                    report.SolvabilityProblems.Add("길이가 바뀌면서 한 방에서 전부 들리게 됐다");
                if (solvability.SinglePassPossible)
                    report.SolvabilityProblems.Add("길이가 바뀌면서 한 회차로 전부 모을 수 있게 됐다 — 되돌려 들을 이유가 사라진다");
            }

            // 얼마나 밀렸는지
            for (int i = 0; i < script.utterances.Length; i++)
            {
                int authored = script.utterances[i].durationMs;
                int actual = adjusted.utterances[i].durationMs;
                int delta = actual - authored;
                if (delta > report.LargestGrowthMs) report.LargestGrowthMs = delta;
                if (delta < report.LargestShrinkMs) report.LargestShrinkMs = delta;
            }
            return report;
        }
    }
}
