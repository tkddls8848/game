using System.Collections.Generic;
using System.Text;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 소나 연출의 순수 계산. 웅얼거림 가림 문자, 파문 위상, 회차 시계 표기.
    /// UnityEngine에 의존하지 않아 EditMode 테스트로 고정한다.
    /// </summary>
    public static class SonarText
    {
        /// <summary>내용을 가리는 블록 문자.</summary>
        public const char Block = '▒'; // ▒

        /// <summary>
        /// 벽 너머 웅얼거림. 글자는 전부 블록으로 가리고 띄어쓰기만 남긴다 —
        /// "누군가 이만큼 말하고 있다"는 리듬만 전하고 내용은 한 글자도 새지 않는다.
        /// </summary>
        public static string Garble(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                sb.Append(c == ' ' ? ' ' : Block);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 파문 하나의 진행도(0=중심, 1=사라짐). 링들은 주기를 등분해 순서대로 출발한다.
        /// 아직 출발 전이면 false. 정수 ms만 받아 재생 위치에 그대로 묶인다 — 정지하면 파문도 멈춘다.
        /// </summary>
        public static bool RipplePhase(int elapsedMs, int periodMs, int ringIndex, int ringCount, out float progress)
        {
            progress = 0f;
            if (periodMs <= 0 || ringCount <= 0 || ringIndex < 0 || ringIndex >= ringCount) return false;
            int delay = periodMs * ringIndex / ringCount;
            int local = elapsedMs - delay;
            if (local < 0) return false;
            progress = (local % periodMs) / (float)periodMs;
            return true;
        }

        /// <summary>목소리는 아직 이름이 아니다(U-3에서 배정). "v2" → "목소리 2". 숫자가 아니면 ID 그대로.</summary>
        public static string VoiceName(string voiceId)
        {
            if (string.IsNullOrEmpty(voiceId)) return "목소리";
            int n;
            return int.TryParse(voiceId.TrimStart('v', 'V'), out n) ? "목소리 " + n : voiceId;
        }

        /// <summary>대본에 나오는 목소리 ID를 등장 순서대로(중복 없이). 타임라인 막대의 줄 순서.</summary>
        public static List<string> Voices(ScriptTimeline timeline)
        {
            var voices = new List<string>();
            if (timeline == null) return voices;
            IList<Utterance> all = timeline.Utterances;
            for (int i = 0; i < all.Count; i++)
            {
                string v = all[i].voiceId;
                if (!string.IsNullOrEmpty(v) && !voices.Contains(v)) voices.Add(v);
            }
            return voices;
        }

        /// <summary>내용까지 들은 발화를 대본 순서로. 청취 화면의 "들은 말" 목록.</summary>
        public static List<Utterance> FullyHeard(ListeningSession session)
        {
            var result = new List<Utterance>();
            if (session == null || session.Timeline == null) return result;
            IList<Utterance> all = session.Timeline.Utterances;
            for (int i = 0; i < all.Count; i++)
            {
                if (session.HeardLevel(all[i].id) == Audibility.Full) result.Add(all[i]);
            }
            return result;
        }

        /// <summary>
        /// 재생 위치까지 이미 끝난 발화 중 내용을 못 들은 것의 수 — "이 회차에서 놓친 발화 있음"(U-6) 표시.
        /// 웅얼거림으로 스친 것도 놓친 것으로 센다. 내용을 모르면 못 들은 것이다.
        /// </summary>
        public static int MissedSoFar(ListeningSession session)
        {
            if (session == null || session.Timeline == null) return 0;
            int missed = 0;
            IList<Utterance> all = session.Timeline.Utterances;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].EndMs <= session.PositionMs && session.HeardLevel(all[i].id) != Audibility.Full) missed++;
            }
            return missed;
        }

        /// <summary>회차 시계. 0:34.0 처럼 분:초.십분의 일 초. 회차는 GameTime 축이 아니라 대본의 자기 축이다.</summary>
        public static string Clock(int ms)
        {
            if (ms < 0) ms = 0;
            int tenths = (ms / 100) % 10;
            int totalSeconds = ms / 1000;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return minutes + ":" + seconds.ToString("00") + "." + tenths;
        }

        /// <summary>배속 표기. 100 → ×1, 50 → ×0.5, 200 → ×2.</summary>
        public static string Speed(int percent)
        {
            if (percent % 100 == 0) return "×" + (percent / 100);
            return "×" + (percent / 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
