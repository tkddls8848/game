using System.Collections.Generic;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 대본을 시간 축으로 조회한다. GameTime에 의존하지 않고 자기 정수 밀리초 축을 쓴다.
    /// 발화 구간은 [startMs, startMs + durationMs) — 끝나는 순간에는 이미 울리지 않는다.
    /// </summary>
    public sealed class ScriptTimeline
    {
        private readonly List<Utterance> _ordered;
        private readonly Dictionary<string, Utterance> _byId;
        private readonly int _durationMs;

        public ScriptTimeline(ScriptDefinition script)
        {
            if (script == null) script = new ScriptDefinition();
            script.Normalized();

            _durationMs = script.durationMs;
            _ordered = new List<Utterance>();
            _byId = new Dictionary<string, Utterance>();

            for (int i = 0; i < script.utterances.Length; i++)
            {
                Utterance u = script.utterances[i];
                if (u == null) continue;
                _ordered.Add(u);
                if (!string.IsNullOrEmpty(u.id)) _byId[u.id] = u;
            }

            // 시작 시각 순. 같으면 ID 순으로 고정해 결과가 대본 순서에 흔들리지 않게 한다.
            _ordered.Sort(delegate(Utterance a, Utterance b)
            {
                int byStart = a.startMs.CompareTo(b.startMs);
                return byStart != 0 ? byStart : string.CompareOrdinal(a.id, b.id);
            });
        }

        public int DurationMs { get { return _durationMs; } }
        public IList<Utterance> Utterances { get { return _ordered.AsReadOnly(); } }

        public bool TryGet(string utteranceId, out Utterance utterance)
        {
            utterance = null;
            if (string.IsNullOrEmpty(utteranceId)) return false;
            return _byId.TryGetValue(utteranceId, out utterance);
        }

        /// <summary>timeMs 순간에 울리고 있는 발화 목록(시작 시각 순).</summary>
        public List<Utterance> ActiveAt(int timeMs)
        {
            var result = new List<Utterance>();
            for (int i = 0; i < _ordered.Count; i++)
            {
                Utterance u = _ordered[i];
                if (u.startMs > timeMs) break;
                if (timeMs < u.EndMs) result.Add(u);
            }
            return result;
        }

        /// <summary>두 발화의 재생 구간이 1ms라도 겹치는가.</summary>
        public static bool Overlaps(Utterance a, Utterance b)
        {
            return a.startMs < b.EndMs && b.startMs < a.EndMs;
        }
    }
}
