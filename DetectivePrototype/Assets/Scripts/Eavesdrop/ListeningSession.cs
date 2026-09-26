using System.Collections.Generic;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 한 회차를 듣는 동안의 상태(순수 C#). 대본·가청 판정·재생 장치를 한데 묶고,
    /// "지금 이 귀에 무엇이 들리는가"와 "지금까지 무엇을 들었는가"를 들고 있는다.
    ///
    /// 재생 위치는 PlaybackTransport가, 어디까지 들리는가는 AudibilityModel이 안다.
    /// 이 클래스가 더하는 것은 **청취점**(지금 서 있는 방)과 **들은 기록**이다.
    ///
    /// 들은 기록의 규칙: 귀가 그 자리에 있던 순간에 울리고 있었으면 들은 것이다.
    /// 건너뛴(Seek) 구간은 듣지 않은 것으로 남는다 — 되돌려 듣게 만드는 것이 이 게임의 동력이므로,
    /// 지나치지 않은 것을 들었다고 쳐 주면 안 된다.
    /// </summary>
    public sealed class ListeningSession
    {
        private readonly ScriptTimeline _timeline;
        private readonly AudibilityModel _audibility;
        private readonly PlaybackTransport _transport;

        /// <summary>말이 아닌 소리들. 없을 수도 있다(이벤트 없는 회차).</summary>
        private readonly EventTimeline _events;

        /// <summary>발화 id → 지금까지 가장 잘 들은 등급.</summary>
        private readonly Dictionary<string, Audibility> _heard = new Dictionary<string, Audibility>();

        private readonly List<PerceivedUtterance> _current = new List<PerceivedUtterance>();
        private readonly List<PerceivedEvent> _currentEvents = new List<PerceivedEvent>();
        private string _listenerRoom = string.Empty;

        public ListeningSession(ScriptTimeline timeline, AudibilityModel audibility)
            : this(timeline, audibility, null)
        {
        }

        /// <summary>이벤트(문소리·발소리·깨지는 소리)까지 함께 듣는 회차.</summary>
        public ListeningSession(ScriptTimeline timeline, AudibilityModel audibility, EventTimeline events)
        {
            _timeline = timeline;
            _audibility = audibility;
            _events = events;
            _transport = new PlaybackTransport(timeline != null ? timeline.DurationMs : 0);
            Refresh();
        }

        public ScriptTimeline Timeline { get { return _timeline; } }
        public PlaybackTransport Transport { get { return _transport; } }
        public int PositionMs { get { return _transport.PositionMs; } }

        /// <summary>지금 서 있는 방. 어느 방에도 속하지 않으면 빈 문자열이고, 그때는 아무것도 들리지 않는다.</summary>
        public string ListenerRoom { get { return _listenerRoom; } }

        /// <summary>이 순간 이 귀에 닿는 발화들. Muffled면 내용이 비어 있다.</summary>
        public IList<PerceivedUtterance> Current { get { return _current.AsReadOnly(); } }

        /// <summary>이 순간 이 귀에 닿는 말이 아닌 소리들. 들은 수에는 끼지 않는다(정황이다).</summary>
        public IList<PerceivedEvent> CurrentEvents { get { return _currentEvents.AsReadOnly(); } }

        /// <summary>이벤트 표. 영상 진행 바에 눈금을 찍을 때 쓴다. 없으면 null.</summary>
        public EventTimeline Events { get { return _events; } }

        /// <summary>청취점을 옮긴다. 옮긴 즉시 들리는 것이 달라진다.</summary>
        public void MoveTo(string roomId)
        {
            string next = roomId ?? string.Empty;
            if (next == _listenerRoom) return;
            _listenerRoom = next;
            Refresh();
        }

        /// <summary>
        /// 재생을 진행시키고 들은 것을 기록한다. 돌려주는 값은 흐른 대본 시간(ms, 뒤로 흐르면 음수).
        ///
        /// <b>뒤로 감는 동안은 들은 것으로 치지 않는다.</b> 거꾸로 흐르는 말은 알아들을 수 없고,
        /// 이미 있는 규칙(건너뛴 구간은 듣지 않은 것)과도 같은 이유다 — 지나치지 않은 것을
        /// 들었다고 쳐 주면 되돌려 들을 이유가 사라진다.
        /// </summary>
        public int Advance(int realDeltaMs)
        {
            int moved = _transport.Advance(realDeltaMs);
            Refresh(moved >= 0 && _transport.Direction == PlayDirection.Forward);
            return moved;
        }

        /// <summary>
        /// 회차의 다른 지점으로 건너뛴다. 건너뛴 구간은 듣지 않은 것으로 남는다.
        /// (Transport를 직접 만지면 Refresh를 빠뜨리기 쉬워 여기로 감싼다.)
        /// </summary>
        public void SeekTo(int ms)
        {
            _transport.SeekTo(ms);
            Refresh();
        }

        /// <summary>회차를 처음으로 되돌린다. 들은 기록은 남는다 — 두 번째 청취의 의미가 거기에 있다.</summary>
        public void Restart()
        {
            _transport.Restart();
            Refresh();
        }

        /// <summary>위치나 방이 바뀐 뒤 지금 들리는 것을 다시 계산하고 기록에 반영한다.</summary>
        public void Refresh()
        {
            Refresh(true);
        }

        /// <summary>
        /// <paramref name="record"/>가 false면 지금 들리는 것만 갱신하고 <b>기록에는 남기지 않는다</b>.
        /// 뒤로 감는 중에 쓴다 — 화면에는 무엇이 울리고 있는지 보여 주되 들은 것으로는 치지 않는다.
        /// </summary>
        public void Refresh(bool record)
        {
            _current.Clear();
            _currentEvents.Clear();
            if (_timeline == null || _audibility == null) return;

            if (_events != null)
            {
                List<PerceivedEvent> heardEvents =
                    _audibility.PerceiveEvents(_events, _transport.PositionMs, _listenerRoom);
                for (int i = 0; i < heardEvents.Count; i++) _currentEvents.Add(heardEvents[i]);
            }

            List<PerceivedUtterance> perceived = _audibility.Perceive(_timeline, _transport.PositionMs, _listenerRoom);
            for (int i = 0; i < perceived.Count; i++)
            {
                PerceivedUtterance p = perceived[i];
                _current.Add(p);
                if (!record) continue;

                Audibility best;
                if (!_heard.TryGetValue(p.UtteranceId, out best) || p.Level > best)
                    _heard[p.UtteranceId] = p.Level;
            }
        }

        /// <summary>이 발화를 지금까지 가장 잘 들은 등급. 한 번도 닿지 않았으면 None.</summary>
        public Audibility HeardLevel(string utteranceId)
        {
            Audibility level;
            return _heard.TryGetValue(utteranceId, out level) ? level : Audibility.None;
        }

        /// <summary>내용까지 들은 발화 수.</summary>
        public int FullyHeardCount
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<string, Audibility> pair in _heard)
                    if (pair.Value == Audibility.Full) n++;
                return n;
            }
        }

        /// <summary>
        /// 웅얼거림으로만 스쳤고 아직 내용을 못 들은 발화 id들.
        /// "저기서 무슨 말이 오갔는데 여기선 안 들렸다" — 되돌아갈 이유를 만든다.
        /// </summary>
        public List<string> MuffledOnly()
        {
            var result = new List<string>();
            foreach (KeyValuePair<string, Audibility> pair in _heard)
                if (pair.Value == Audibility.Muffled) result.Add(pair.Key);
            result.Sort(System.StringComparer.Ordinal);
            return result;
        }

        /// <summary>기록만 지운다(회차를 다시 듣되 무엇을 들었는지는 잊는 경우). 재생 위치는 건드리지 않는다.</summary>
        public void ForgetHeard()
        {
            _heard.Clear();
            Refresh();
        }
    }
}
