using System;
using System.Collections.Generic;

namespace Detective.Eavesdrop
{
    /// <summary>이벤트 묶음. 대본과 따로 적거나(authored) 이동에서 만들어(derived) 합친다.</summary>
    [Serializable]
    public class EventTable
    {
        public ScriptEvent[] events = new ScriptEvent[0];

        public EventTable Normalized()
        {
            if (events == null) events = new ScriptEvent[0];
            for (int i = 0; i < events.Length; i++)
                if (events[i] != null) events[i].Normalized();
            return this;
        }
    }

    /// <summary>
    /// 시각으로 이벤트를 뽑는 표. <see cref="ScriptTimeline"/>과 같은 구실을 이벤트에 한다.
    ///
    /// 회차를 앞뒤로 마구 긁는 게임이라 "지금 울리고 있는 것"을 임의 지점에서 빨리 답해야 한다.
    /// 그래서 시작 시각으로 정렬해 두고 훑는다. 이벤트 수가 수백 개라 이 정도로 충분하다.
    /// </summary>
    public sealed class EventTimeline
    {
        private readonly List<ScriptEvent> _ordered = new List<ScriptEvent>();
        private readonly Dictionary<string, ScriptEvent> _byId = new Dictionary<string, ScriptEvent>();

        public EventTimeline(IEnumerable<ScriptEvent> events)
        {
            if (events != null)
            {
                foreach (ScriptEvent e in events)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    e.Normalized();
                    if (_byId.ContainsKey(e.id)) continue;   // 같은 id는 처음 것만 남긴다
                    _byId[e.id] = e;
                    _ordered.Add(e);
                }
                _ordered.Sort(CompareByStart);
            }
        }

        private static int CompareByStart(ScriptEvent a, ScriptEvent b)
        {
            int byStart = a.startMs.CompareTo(b.startMs);
            return byStart != 0 ? byStart : string.CompareOrdinal(a.id, b.id);
        }

        public int Count { get { return _ordered.Count; } }
        public IList<ScriptEvent> All { get { return _ordered.AsReadOnly(); } }

        public bool TryGet(string eventId, out ScriptEvent found)
        {
            if (string.IsNullOrEmpty(eventId)) { found = null; return false; }
            return _byId.TryGetValue(eventId, out found);
        }

        /// <summary>이 순간 울리고 있는 이벤트들. 구간은 [startMs, EndMs).</summary>
        public List<ScriptEvent> ActiveAt(int timeMs)
        {
            var active = new List<ScriptEvent>();
            for (int i = 0; i < _ordered.Count; i++)
            {
                ScriptEvent e = _ordered[i];
                if (e.startMs > timeMs) break;                 // 정렬돼 있으니 더 볼 필요가 없다
                if (timeMs < e.EndMs) active.Add(e);
            }
            return active;
        }

        /// <summary>
        /// 구간 안의 이벤트들. 영상 플레이어의 눈금(마커)을 찍을 때 쓴다 —
        /// 진행 바에 "여기서 뭔가 났다"를 표시하려면 범위로 물어봐야 한다.
        /// </summary>
        public List<ScriptEvent> Between(int fromMs, int toMs)
        {
            var found = new List<ScriptEvent>();
            for (int i = 0; i < _ordered.Count; i++)
            {
                ScriptEvent e = _ordered[i];
                if (e.startMs > toMs) break;
                if (e.EndMs > fromMs) found.Add(e);
            }
            return found;
        }
    }
}
