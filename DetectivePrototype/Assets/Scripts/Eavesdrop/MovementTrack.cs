using System;
using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.NPC;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 한 인물이 한 방에 머무른 구간. <c>[startMs, endMs)</c> 반열린 구간이다 —
    /// 끝나는 순간에는 이미 그 방에 없다(발화 구간과 같은 규칙).
    /// JsonUtility가 다룰 수 있도록 public 필드만 쓴다.
    /// </summary>
    [Serializable]
    public class TrackSegment
    {
        public int startMs;
        public int endMs;

        /// <summary>rooms.json의 방 id.</summary>
        public string room;

        public int DurationMs { get { return endMs - startMs; } }

        public bool Contains(int ms) { return ms >= startMs && ms < endMs; }

        /// <summary>[startMs, endMs)를 통째로 감싸는가. 발화 내내 그 방에 있었는지 볼 때 쓴다.</summary>
        public bool Covers(int fromMs, int toMs)
        {
            return Contains(fromMs) && endMs >= toMs;
        }
    }

    /// <summary>
    /// 한 인물의 연속 이동 트랙. 틱당 방 하나였던 <c>schedule[7]</c>을 대체한다.
    ///
    /// 시각은 <b>회차 기준 정수 밀리초</b>다 — 대본(<see cref="ScriptDefinition.durationMs"/>)과 같은 축이고
    /// <c>GameTime</c>의 18:00 기준 축이 아니다. 엿듣기에서 흐르는 시간은 회차 시간이다.
    ///
    /// 구간 사이(적어 두지 않은 틈)는 <b>이동 중</b>이다. 이때 방은 빈 문자열이다 —
    /// 복도로 치지 않는 것은 의도다. 복도에서 실제로 무슨 일이 있었다면(거기서 말을 했다면)
    /// 복도 구간을 직접 적어야 하고, 그러면 그 시각 복도는 "지나가는 중"이 아니라 "있었던 곳"이 된다.
    /// </summary>
    [Serializable]
    public class MovementTrack
    {
        public string npcId;
        public TrackSegment[] segments = new TrackSegment[0];

        public MovementTrack Normalized()
        {
            if (segments == null) segments = new TrackSegment[0];
            return this;
        }

        /// <summary>구간 순서에 기대지 않는다 — 적힌 순서가 뒤섞여 있어도 같은 답이 나온다.</summary>
        public TrackSegment SegmentAt(int ms)
        {
            if (segments == null) return null;
            for (int i = 0; i < segments.Length; i++)
            {
                TrackSegment segment = segments[i];
                if (segment != null && segment.Contains(ms)) return segment;
            }
            return null;
        }

        /// <summary>그 시각에 있던 방. 이동 중이거나 기록 밖이면 빈 문자열.</summary>
        public string RoomAt(int ms)
        {
            TrackSegment segment = SegmentAt(ms);
            return segment != null && segment.room != null ? segment.room : string.Empty;
        }

        /// <summary>[startMs, endMs) 내내 한 방에 있었으면 그 방, 중간에 옮겼거나 기록 밖이면 null.</summary>
        public string StayedRoomDuring(int startMs, int endMs)
        {
            if (segments == null) return null;
            for (int i = 0; i < segments.Length; i++)
            {
                TrackSegment segment = segments[i];
                if (segment != null && segment.Covers(startMs, endMs)) return segment.room ?? string.Empty;
            }
            return null;
        }

        /// <summary>기록이 시작하는 방. 구간이 없으면 빈 문자열.</summary>
        public string FirstRoom { get { return EdgeRoom(true); } }

        /// <summary>기록이 끝나는 방. 구간이 없으면 빈 문자열.</summary>
        public string LastRoom { get { return EdgeRoom(false); } }

        /// <summary>그 시각의 자리. 방 안인지 이동 중인지, 이동 중이면 어디서 어디로 얼마나 왔는지.</summary>
        public TrackPosition SampleAt(int ms)
        {
            var position = new TrackPosition();
            TrackSegment here = SegmentAt(ms);
            if (here != null)
            {
                string room = here.room ?? string.Empty;
                position.Room = room;
                position.FromRoom = room;
                position.ToRoom = room;
                return position;
            }

            TrackSegment before = null;
            TrackSegment after = null;
            if (segments != null)
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    TrackSegment segment = segments[i];
                    if (segment == null) continue;
                    if (segment.endMs <= ms && (before == null || segment.endMs > before.endMs)) before = segment;
                    if (segment.startMs > ms && (after == null || segment.startMs < after.startMs)) after = segment;
                }
            }

            position.Room = string.Empty;
            position.FromRoom = before != null ? (before.room ?? string.Empty) : string.Empty;
            position.ToRoom = after != null ? (after.room ?? string.Empty) : string.Empty;

            // 앞뒤가 다 있어야 "이동 중"이다. 기록 시작 전·끝난 뒤는 그냥 자리에 없는 것이다.
            if (before == null || after == null) return position;

            position.InTransit = true;
            int span = after.startMs - before.endMs;
            position.ProgressPermille = span > 0 ? (ms - before.endMs) * 1000 / span : 0;
            return position;
        }

        private string EdgeRoom(bool first)
        {
            TrackSegment best = null;
            if (segments != null)
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    TrackSegment segment = segments[i];
                    if (segment == null) continue;
                    if (best == null) best = segment;
                    else if (first ? segment.startMs < best.startMs : segment.endMs > best.endMs) best = segment;
                }
            }
            return best != null ? (best.room ?? string.Empty) : string.Empty;
        }
    }

    /// <summary>이동 트랙 파일 최상위 객체. JsonUtility는 배열을 최상위로 읽지 못하므로 한 겹 싸 둔다.</summary>
    [Serializable]
    public class MovementTrackTable
    {
        public MovementTrack[] tracks = new MovementTrack[0];

        public MovementTrackTable Normalized()
        {
            if (tracks == null) tracks = new MovementTrack[0];
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i] != null) tracks[i].Normalized();
            }
            return this;
        }
    }

    /// <summary>
    /// 한 시각의 자리. 방 안이면 <see cref="Room"/>이 채워지고,
    /// 두 구간 사이를 지나는 중이면 <see cref="InTransit"/>이 서고 <see cref="Room"/>은 빈 문자열이다.
    /// 토큰을 그리는 쪽은 이동 중일 때 <see cref="FromRoom"/>→<see cref="ToRoom"/>을
    /// <see cref="ProgressPermille"/>만큼 보간하면 된다(천분율 정수 — 시각 계산에 부동소수를 들이지 않는다).
    /// </summary>
    public struct TrackPosition
    {
        public string Room;
        public string FromRoom;
        public string ToRoom;
        public bool InTransit;

        /// <summary>이동 중일 때 0~1000. 그 밖에는 0.</summary>
        public int ProgressPermille;

        /// <summary>어느 방 안에 있는가(이동 중·기록 밖이면 false).</summary>
        public bool HasPlace { get { return !string.IsNullOrEmpty(Room); } }
    }

    /// <summary>
    /// 인물별 이동 트랙 묶음(순수 C#). "그때 누가 어디 있었나"를 묻는 쪽이 쓰는 입구다.
    /// <c>NpcSchedule</c>이 틱 배열에 대해 하던 일을 연속 구간에 대해 한다.
    /// </summary>
    public sealed class MovementTracks
    {
        private readonly List<MovementTrack> _all = new List<MovementTrack>();
        private readonly Dictionary<string, MovementTrack> _byNpc = new Dictionary<string, MovementTrack>();

        public MovementTracks(IEnumerable<MovementTrack> tracks)
        {
            if (tracks == null) return;
            foreach (MovementTrack track in tracks)
            {
                if (track == null) continue;
                track.Normalized();
                _all.Add(track);
                if (!string.IsNullOrEmpty(track.npcId) && !_byNpc.ContainsKey(track.npcId)) _byNpc[track.npcId] = track;
            }

            // 파일 순서와 무관하게 늘 같은 순서가 되도록 id로 정렬한다(NpcRoster와 같은 규칙).
            _all.Sort(delegate(MovementTrack a, MovementTrack b) { return string.CompareOrdinal(a.npcId, b.npcId); });
        }

        public static MovementTracks FromTable(MovementTrackTable table)
        {
            if (table == null) return new MovementTracks(null);
            table.Normalized();
            return new MovementTracks(table.tracks);
        }

        public IList<MovementTrack> All { get { return _all.AsReadOnly(); } }

        public bool TryGet(string npcId, out MovementTrack track)
        {
            track = null;
            if (string.IsNullOrEmpty(npcId)) return false;
            return _byNpc.TryGetValue(npcId, out track);
        }

        /// <summary>그 시각 그 인물이 있던 방. 이동 중·기록 밖·트랙 없음이면 빈 문자열.</summary>
        public string PositionAt(string npcId, int ms)
        {
            MovementTrack track;
            return TryGet(npcId, out track) ? track.RoomAt(ms) : string.Empty;
        }

        public TrackPosition SampleAt(string npcId, int ms)
        {
            MovementTrack track;
            if (TryGet(npcId, out track)) return track.SampleAt(ms);

            var empty = new TrackPosition();
            empty.Room = string.Empty;
            empty.FromRoom = string.Empty;
            empty.ToRoom = string.Empty;
            return empty;
        }

        /// <summary>두 구간 사이를 지나는 중인가. "방에 없다"와 "기록에 없다"를 가른다.</summary>
        public bool IsInTransit(string npcId, int ms)
        {
            return SampleAt(npcId, ms).InTransit;
        }

        /// <summary>[startMs, endMs) 내내 한 방에 있었으면 그 방, 아니면 null.</summary>
        public string StayedRoomDuring(string npcId, int startMs, int endMs)
        {
            MovementTrack track;
            return TryGet(npcId, out track) ? track.StayedRoomDuring(startMs, endMs) : null;
        }

        /// <summary>그 시각 그 방에 있던 인물 id들(id 순).</summary>
        public List<string> OccupantsAt(int ms, string roomId)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(roomId)) return result;

            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].RoomAt(ms) == roomId) result.Add(_all[i].npcId);
            }
            return result;
        }
    }

    /// <summary>
    /// 이동 트랙 참조 무결성 검사(순수 C#). <c>ScriptValidator</c>와 같은 자리에 있다 —
    /// "트랙이 말이 되는가"만 본다. 사건이 풀리는지는 보지 않는다.
    /// </summary>
    public static class MovementTrackValidator
    {
        /// <summary>
        /// 문제 목록. 비어 있으면 통과. layout·roster는 null이면 그 항목 검사를 건너뛰고,
        /// durationMs가 0 이하면 회차 길이 검사를 건너뛴다.
        /// </summary>
        public static List<string> Validate(MovementTrackTable table, RoomLayout layout, NpcRoster roster, int durationMs)
        {
            var errors = new List<string>();
            if (table == null)
            {
                errors.Add("이동 트랙을 읽지 못했다.");
                return errors;
            }
            table.Normalized();

            var npcIds = new HashSet<string>();
            for (int i = 0; i < table.tracks.Length; i++)
            {
                MovementTrack track = table.tracks[i];
                string label = "tracks[" + i + "]";
                if (track == null) { errors.Add(label + "가 비어 있다."); continue; }

                if (string.IsNullOrEmpty(track.npcId)) errors.Add(label + ": npcId가 비어 있다.");
                else
                {
                    label = label + "('" + track.npcId + "')";
                    if (!npcIds.Add(track.npcId)) errors.Add(label + ": npcId가 중복된다.");
                    if (roster != null)
                    {
                        NpcDefinition npc;
                        if (!roster.TryGet(track.npcId, out npc)) errors.Add(label + ": '" + track.npcId + "'는 없는 인물이다.");
                    }
                }

                ValidateSegments(track, label, layout, durationMs, errors);
            }
            return errors;
        }

        private static void ValidateSegments(MovementTrack track, string trackLabel, RoomLayout layout, int durationMs, List<string> errors)
        {
            if (track.segments.Length == 0)
            {
                errors.Add(trackLabel + ": 구간이 하나도 없다.");
                return;
            }

            for (int s = 0; s < track.segments.Length; s++)
            {
                TrackSegment segment = track.segments[s];
                string label = trackLabel + " segments[" + s + "]";
                if (segment == null) { errors.Add(label + "가 비어 있다."); continue; }

                if (segment.startMs < 0) errors.Add(label + ": startMs가 음수다(" + segment.startMs + ").");
                if (segment.endMs <= segment.startMs)
                    errors.Add(label + ": endMs(" + segment.endMs + ")가 startMs(" + segment.startMs + ") 이하다. 구간은 [startMs, endMs)다.");
                if (durationMs > 0 && segment.endMs > durationMs)
                    errors.Add(label + ": " + segment.endMs + "ms에 끝나 회차 길이 " + durationMs + "ms를 넘는다.");

                if (string.IsNullOrEmpty(segment.room)) errors.Add(label + ": room이 비어 있다.");
                else if (layout != null)
                {
                    RoomDefinition room;
                    if (!layout.TryGetRoom(segment.room, out room)) errors.Add(label + ": room '" + segment.room + "'이 없는 방이다.");
                }

                for (int other = 0; other < s; other++)
                {
                    TrackSegment previous = track.segments[other];
                    if (previous == null) continue;
                    if (previous.startMs < segment.endMs && segment.startMs < previous.endMs)
                        errors.Add(label + ": segments[" + other + "]와 시각이 겹친다. 한 사람이 두 방에 있을 수는 없다.");
                }
            }
        }

        /// <summary>
        /// 대본과 대조한다. 어떤 발화든 그것을 말한 인물이 그 시각 그 방에 <b>내내</b> 있어야 한다.
        /// 여기가 어긋나면 플레이어가 들은 목소리와 평면도 위의 토큰이 서로 다른 이야기를 하게 되고,
        /// 익명을 푸는 단서(누가 그 방에 있었나)가 거짓말이 된다.
        ///
        /// 트랙이 아예 없는 인물은 건너뛴다 — 트랙을 다 적기 전에도 대본을 검사할 수 있어야 한다.
        /// </summary>
        public static List<string> ValidateAgainstScript(MovementTracks tracks, ScriptDefinition script)
        {
            var errors = new List<string>();
            if (script == null) { errors.Add("대본이 없다."); return errors; }
            if (tracks == null) { errors.Add("이동 트랙이 없다."); return errors; }
            script.Normalized();

            var npcByVoice = new Dictionary<string, string>();
            for (int i = 0; i < script.speakers.Length; i++)
            {
                SpeakerDefinition speaker = script.speakers[i];
                if (speaker == null || string.IsNullOrEmpty(speaker.voiceId)) continue;
                npcByVoice[speaker.voiceId] = speaker.npcId;
            }

            for (int i = 0; i < script.utterances.Length; i++)
            {
                Utterance utterance = script.utterances[i];
                if (utterance == null || string.IsNullOrEmpty(utterance.voiceId)) continue;

                string npcId;
                if (!npcByVoice.TryGetValue(utterance.voiceId, out npcId) || string.IsNullOrEmpty(npcId)) continue;

                MovementTrack track;
                if (!tracks.TryGet(npcId, out track)) continue;

                string stayed = track.StayedRoomDuring(utterance.startMs, utterance.EndMs);
                if (stayed == utterance.room) continue;

                string label = "발화 '" + utterance.id + "'(" + utterance.startMs + "~" + utterance.EndMs + "ms)";
                if (stayed == null)
                    errors.Add(label + ": 말한 '" + npcId + "'이(가) 그동안 " + utterance.room + "에 계속 있지 않았다(이동 중이거나 기록 밖이다).");
                else
                    errors.Add(label + ": " + utterance.room + "에서 울리는데 말한 '" + npcId + "'은(는) " + stayed + "에 있었다.");
            }
            return errors;
        }
    }
}
