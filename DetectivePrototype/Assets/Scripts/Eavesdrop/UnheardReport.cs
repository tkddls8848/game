using System.Collections.Generic;
using Detective.Core;
using Detective.Data;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 발화 하나를 지금까지 가장 잘 들은 등급을 묻는 창구.
    /// <see cref="ListeningSession.HeardLevel"/>·<c>HeardHistory.Level</c>·<c>RestoredProgress.HeardLevel</c>이
    /// 전부 이 모양이라, 지금 듣는 판이든 불러온 판이든 같은 문으로 들어온다.
    /// </summary>
    public delegate Audibility HeardLookup(string utteranceId);

    /// <summary>
    /// 아직 내용을 못 들은 발화 하나. <b>내용은 들어 있지 않다</b> —
    /// text도 voiceId도 싣지 않는 것이 이 자료형의 전부라 해도 좋다.
    /// 무슨 말이었는지를 여기서 알려 주면 되돌아가 들을 이유가 사라진다.
    ///
    /// 시각(<see cref="StartMs"/>)은 내용이 아니라 <b>어디로 되감을지</b>다. 타임라인에 금을 긋는 데 쓴다.
    /// </summary>
    public sealed class UnheardUtterance
    {
        /// <summary>엔진 쪽 식별용. 플레이어에게 보여 줄 값이 아니다.</summary>
        public string UtteranceId;

        /// <summary>여기로 가면 내용이 들린다.</summary>
        public string Room;

        public int StartMs;
        public int EndMs;

        /// <summary>
        /// 지금까지 이 발화에 닿은 가장 높은 등급.
        /// <see cref="Audibility.Muffled"/>면 <b>있다는 것은 안다</b>(벽 너머로 스쳤다),
        /// <see cref="Audibility.None"/>이면 <b>있는 줄도 모른다</b>.
        /// 화면이 무엇까지 드러낼지는 이 값으로 가른다 — 스치지도 않은 것을 목록에 띄우면 답을 알려 주는 셈이다.
        /// </summary>
        public Audibility BestSoFar;

        /// <summary>벽 너머로 스쳤지만 내용을 못 들었다. "저기서 무슨 말이 오갔다"까지는 아는 상태.</summary>
        public bool IsMissed { get { return BestSoFar == Audibility.Muffled; } }

        /// <summary>한 번도 닿지 않았다.</summary>
        public bool IsUntouched { get { return BestSoFar == Audibility.None; } }
    }

    /// <summary>방 하나에 남아 있는 못 들은 발화들. 시각 순으로 정렬돼 있다.</summary>
    public sealed class RoomUnheard
    {
        private readonly List<UnheardUtterance> _utterances = new List<UnheardUtterance>();

        internal RoomUnheard(string roomId)
        {
            RoomId = roomId;
        }

        public string RoomId { get; private set; }

        /// <summary>못 들은 발화들(시작 시각 순, 같으면 id 순).</summary>
        public IList<UnheardUtterance> Utterances { get { return _utterances.AsReadOnly(); } }

        public int TotalCount { get { return _utterances.Count; } }

        /// <summary>벽 너머로 스쳐서 <b>있다는 것은 아는</b> 발화 수. 화면에 띄워도 답이 새지 않는 쪽이다.</summary>
        public int MissedCount { get; private set; }

        /// <summary>한 번도 닿지 않은 발화 수.</summary>
        public int UntouchedCount { get { return _utterances.Count - MissedCount; } }

        /// <summary>가장 이른 못 들은 발화의 시작 시각. 없으면 -1.</summary>
        public int EarliestStartMs { get { return _utterances.Count > 0 ? _utterances[0].StartMs : -1; } }

        internal void Add(UnheardUtterance utterance)
        {
            _utterances.Add(utterance);
            if (utterance.IsMissed) MissedCount = MissedCount + 1;
        }

        internal void Sort()
        {
            _utterances.Sort(delegate(UnheardUtterance a, UnheardUtterance b)
            {
                int byStart = a.StartMs.CompareTo(b.StartMs);
                return byStart != 0 ? byStart : string.CompareOrdinal(a.UtteranceId, b.UtteranceId);
            });
        }
    }

    /// <summary>
    /// 못 들은 것을 세어 주는 보고서(Phase U-6, 순수 C#).
    ///
    /// 놓친 발화를 영영 모르면 답답함이 재미가 아니라 벽이 된다(DEVELOPMENT_PLAN_UNHEARD.md §7).
    /// 그래서 <b>"저 방에 아직 못 들은 것이 있다"</b>까지만 말한다. 무슨 말이었는지는 끝까지 말하지 않는다 —
    /// 내용을 흘리는 순간 되돌려 들을 이유가 사라지고, 이 게임에 남는 것이 없다.
    ///
    /// "이 방에서 내용을 들을 수 있는가"는 <see cref="AudibilityModel"/>에게 묻는다.
    /// 같은 방이면 들린다는 규칙을 여기서 다시 쓰지 않는 것은, 규칙이 한 군데에만 있어야
    /// 나중에 가청 판정이 바뀔 때 보고서가 따라오기 때문이다.
    /// </summary>
    public sealed class UnheardReport
    {
        private readonly List<RoomUnheard> _rooms = new List<RoomUnheard>();
        private readonly Dictionary<string, RoomUnheard> _byRoom = new Dictionary<string, RoomUnheard>();
        private readonly List<string> _roomsToVisit = new List<string>();
        private readonly AudibilityModel _audibility;

        private UnheardReport(AudibilityModel audibility)
        {
            _audibility = audibility;
        }

        /// <summary>대본에 있는 발화 수(id가 비었거나 들을 방이 없는 발화는 빠진다).</summary>
        public int TotalCount { get; private set; }

        /// <summary>내용까지 들은 발화 수.</summary>
        public int FullyHeardCount { get; private set; }

        /// <summary>아직 내용을 못 들은 발화 수.</summary>
        public int UnheardCount { get { return TotalCount - FullyHeardCount; } }

        /// <summary>벽 너머로 스쳤지만 내용을 못 들은 발화 수(전체).</summary>
        public int MissedCount { get; private set; }

        /// <summary>한 번도 닿지 않은 발화 수(전체).</summary>
        public int UntouchedCount { get { return UnheardCount - MissedCount; } }

        /// <summary>진행도 천분율(0~1000). 부동소수를 들이지 않으려고 천분율 정수로 둔다. 셀 것이 없으면 1000.</summary>
        public int ProgressPermille
        {
            get { return TotalCount > 0 ? FullyHeardCount * 1000 / TotalCount : 1000; }
        }

        public bool IsComplete { get { return UnheardCount == 0; } }

        /// <summary>못 들은 것이 남은 방들(방 id 순). 다 들은 방은 아예 빠진다.</summary>
        public IList<RoomUnheard> Rooms { get { return _rooms.AsReadOnly(); } }

        /// <summary>
        /// 웅얼거림으로 스치기만 한 것이 남은 방들(방 id 순) — <b>가야 할 방</b>이다.
        /// 한 번도 닿지 않은 발화만 남은 방은 여기 들어가지 않는다. 플레이어가 아직 그 존재를 모르기 때문이다.
        /// </summary>
        public IList<string> RoomsToVisit { get { return _roomsToVisit.AsReadOnly(); } }

        public RoomUnheard ForRoom(string roomId)
        {
            RoomUnheard room;
            if (!string.IsNullOrEmpty(roomId) && _byRoom.TryGetValue(roomId, out room)) return room;
            return null;
        }

        /// <summary>그 방에 아직 못 들은 것이 남았는가.</summary>
        public bool HasUnheardIn(string roomId)
        {
            RoomUnheard room = ForRoom(roomId);
            return room != null && room.TotalCount > 0;
        }

        /// <summary>그 방에 남은, 스쳐서 존재를 아는 발화 수. 화면에 배지로 띄워도 되는 값이다.</summary>
        public int MissedCountIn(string roomId)
        {
            RoomUnheard room = ForRoom(roomId);
            return room != null ? room.MissedCount : 0;
        }

        /// <summary>
        /// 여기 선 채로 <b>웅얼거림이 들려오는</b> 방들 가운데 못 들은 것이 남은 방(방 id 순).
        /// 지도 전체를 펼쳐 보이는 대신 "벽 너머 저쪽"만 가리키는 힌트다 — 한 걸음이면 닿는다.
        /// 지금 선 방 자신은 빠진다(거기 남은 것은 여기서 이미 들리는 중이다).
        /// </summary>
        public List<string> NeighboursToVisit(string listenerRoom)
        {
            var result = new List<string>();
            if (_audibility == null || string.IsNullOrEmpty(listenerRoom)) return result;

            for (int i = 0; i < _rooms.Count; i++)
            {
                string roomId = _rooms[i].RoomId;
                if (roomId == listenerRoom) continue;
                if (_audibility.Judge(listenerRoom, roomId) != Audibility.Muffled) continue;
                result.Add(roomId);
            }
            return result;
        }

        /// <summary>회차를 처음부터 되감지 않고 바로 뛸 수 있는, 가장 이른 못 들은 발화의 시각. 없으면 -1.</summary>
        public int EarliestUnheardMs
        {
            get
            {
                int earliest = -1;
                for (int i = 0; i < _rooms.Count; i++)
                {
                    int start = _rooms[i].EarliestStartMs;
                    if (start < 0) continue;
                    if (earliest < 0 || start < earliest) earliest = start;
                }
                return earliest;
            }
        }

        /// <summary>
        /// 지금 듣고 있는 세션에서 그대로 뽑는다.
        /// 불러온 판이면 <c>HeardHistory.Level</c>을 넘기는 쪽(<see cref="Build(ScriptTimeline,AudibilityModel,HeardLookup)"/>)을 쓴다 —
        /// 세션만 보면 저장에 적혀 있던 기록이 빠진다.
        /// </summary>
        public static UnheardReport Build(ListeningSession session, AudibilityModel audibility)
        {
            if (session == null) return Build(null, audibility, null);
            return Build(session.Timeline, audibility, session.HeardLevel);
        }

        /// <summary>
        /// 대본 전체를 훑어 "어느 방에 무엇이 남았는가"를 센다.
        /// 발화가 울리는 방이 평면도에 없으면 갈 수 있는 방이 없다는 뜻이므로 아예 세지 않는다 —
        /// 닿을 수 없는 것을 진행도의 분모에 넣으면 100%가 영원히 나오지 않는다.
        /// </summary>
        public static UnheardReport Build(ScriptTimeline timeline, AudibilityModel audibility, HeardLookup heard)
        {
            var report = new UnheardReport(audibility);
            if (timeline == null) return report;

            IList<Utterance> utterances = timeline.Utterances;
            for (int i = 0; i < utterances.Count; i++)
            {
                Utterance utterance = utterances[i];
                if (utterance == null || string.IsNullOrEmpty(utterance.id)) continue;

                string room = FullyAudibleRoom(utterance, audibility);
                if (room == null) continue;

                report.TotalCount = report.TotalCount + 1;

                Audibility level = heard != null ? heard(utterance.id) : Audibility.None;
                if (level == Audibility.Full)
                {
                    report.FullyHeardCount = report.FullyHeardCount + 1;
                    continue;
                }
                if (level == Audibility.Muffled) report.MissedCount = report.MissedCount + 1;

                report.Bucket(room).Add(new UnheardUtterance
                {
                    UtteranceId = utterance.id,
                    Room = room,
                    StartMs = utterance.startMs,
                    EndMs = utterance.EndMs,
                    BestSoFar = level
                });
            }

            report.Finish();
            return report;
        }

        /// <summary>
        /// 이 발화의 내용을 들을 수 있는 방. 없으면 null.
        /// 평면도가 없으면(테스트·검사 용도) 대본에 적힌 방을 그대로 믿는다.
        /// </summary>
        private static string FullyAudibleRoom(Utterance utterance, AudibilityModel audibility)
        {
            string room = utterance.room;
            if (string.IsNullOrEmpty(room)) return null;
            if (audibility == null) return room;

            RoomLayout layout = audibility.Layout;
            if (layout == null) return room;

            RoomDefinition definition;
            if (!layout.TryGetRoom(room, out definition)) return null;

            return audibility.Judge(room, room) == Audibility.Full ? room : null;
        }

        private RoomUnheard Bucket(string roomId)
        {
            RoomUnheard bucket;
            if (_byRoom.TryGetValue(roomId, out bucket)) return bucket;

            bucket = new RoomUnheard(roomId);
            _byRoom[roomId] = bucket;
            _rooms.Add(bucket);
            return bucket;
        }

        /// <summary>대본 순서에 흔들리지 않도록 방은 id 순, 발화는 시각 순으로 고정한다.</summary>
        private void Finish()
        {
            _rooms.Sort(delegate(RoomUnheard a, RoomUnheard b) { return string.CompareOrdinal(a.RoomId, b.RoomId); });
            for (int i = 0; i < _rooms.Count; i++)
            {
                _rooms[i].Sort();
                if (_rooms[i].MissedCount > 0) _roomsToVisit.Add(_rooms[i].RoomId);
            }
        }
    }
}
