using System;
using System.Collections.Generic;
using Detective.Data;
using Detective.Eavesdrop;
using Detective.Investigation;

namespace Detective.Core
{
    /// <summary>
    /// 저장 파일 하나를 지금 빌드가 어떻게 받아들였는가.
    /// 읽을 수 없는 이유를 하나로 뭉뚱그리지 않는 것은, 화면에 보여 줄 말이 저마다 다르기 때문이다
    /// ("저장이 없다" / "예전 판으로 만든 저장이다" / "더 새 판으로 만든 저장이다").
    /// </summary>
    public enum SaveCompatibility
    {
        /// <summary>저장 자체가 없다(null).</summary>
        Missing = 0,

        /// <summary>version이 적혀 있지 않다(0 이하). 형식을 알 수 없으므로 읽지 않는다.</summary>
        Unversioned = 1,

        /// <summary>승격 경로가 없는 옛 형식이다.</summary>
        TooOld = 2,

        /// <summary>더 새 빌드가 쓴 저장이다. 모르는 필드를 지워 덮어쓰지 않도록 읽지 않는다.</summary>
        FromFuture = 3,

        /// <summary>옛 형식이지만 지금 형식으로 승격했다.</summary>
        Upgraded = 4,

        /// <summary>지금 형식 그대로다.</summary>
        Current = 5
    }

    /// <summary>
    /// 들은 발화 한 줄. <c>Dictionary</c>를 쓸 수 없으니(JsonUtility 제약) 배열 한 줄로 편다.
    /// <c>level</c>은 <see cref="Audibility"/>의 정수값이다 — enum을 그대로 쓰면 값이 바뀌었을 때
    /// 옛 저장이 조용히 다른 뜻이 되므로, 읽을 때 범위를 검사하고 들인다.
    /// </summary>
    [Serializable]
    public class HeardEntry
    {
        public string utteranceId = string.Empty;

        /// <summary>0=None · 1=Muffled · 2=Full. 범위를 벗어난 값은 복원할 때 걸러진다.</summary>
        public int level;

        public Audibility Level { get { return SaveGame.ToAudibility(level); } }

        public bool IsValidLevel { get { return level >= (int)Audibility.None && level <= (int)Audibility.Full; } }
    }

    /// <summary>
    /// 한 판의 저장 상태(순수 C#). JsonUtility가 읽고 쓸 수 있도록 <b>public 필드 · 배열 · 문자열 id</b>만 쓴다
    /// (Dictionary·다형성·프로퍼티 금지). 시각은 전부 정수 밀리초다.
    ///
    /// 이 클래스는 <b>파일을 읽거나 쓰지 않는다</b>. 파일 입출력은 Unity 쪽(Application.persistentDataPath)이 맡고,
    /// 여기는 "상태 → SaveGame"과 "SaveGame → 상태"만 순수 함수로 다룬다(<see cref="SaveGameCodec"/>).
    /// 그래야 저장·복원 규칙이 헤드리스로 검증된다(CLAUDE.md §18-1).
    ///
    /// <b>version을 반드시 적는다.</b> JsonUtility는 빠진 int를 0으로 채우므로 0은 "적히지 않았다"는 뜻이고,
    /// 그런 저장은 승격하지 않고 거부한다. 형식이 바뀌면 <see cref="CurrentVersion"/>을 올리고
    /// <see cref="SaveGameCodec"/>에 한 단계 승격을 더한다.
    /// </summary>
    [Serializable]
    public class SaveGame
    {
        /// <summary>지금 빌드가 쓰는 형식 번호.</summary>
        public const int CurrentVersion = 1;

        /// <summary>승격해서라도 읽어 주는 가장 오래된 형식. 이보다 낮으면 <see cref="SaveCompatibility.TooOld"/>.</summary>
        public const int OldestUpgradableVersion = 1;

        /// <summary>0이면 적히지 않은 것이다 — 읽지 않는다.</summary>
        public int version;

        /// <summary>어느 사건의 저장인가. 다른 사건을 열어 둔 채로 덮어쓰지 않도록 대조한다.</summary>
        public string caseId = string.Empty;

        /// <summary>저장 시각(Unix epoch 밀리초, UTC). 슬롯 목록에 "언제 저장했는가"를 보여 줄 때 쓴다.</summary>
        public long savedAtUnixMs;

        /// <summary>회차 재생 위치(ms). 대본 축이지 GameTime의 18:00 축이 아니다.</summary>
        public int positionMs;

        /// <summary>청취점 — 그때 귀가 서 있던 방 id. 어느 방에도 없었으면 빈 문자열.</summary>
        public string listenerRoom = string.Empty;

        /// <summary>들은 발화 기록. <see cref="Audibility.None"/>인 줄은 적지 않는다(아무것도 뜻하지 않는다).</summary>
        public HeardEntry[] heard = new HeardEntry[0];

        /// <summary>목소리 배정 보드(<see cref="VoiceAssignment.ToPairs"/>). 붙어 있는 칸만 적힌다.</summary>
        public SpeakerDefinition[] voices = new SpeakerDefinition[0];

        /// <summary>수집한 단서 id(획득 순).</summary>
        public string[] evidence = new string[0];

        /// <summary>JSON에서 빠진 배열·문자열을 빈 값으로 채운다. 자기 자신을 돌려준다.</summary>
        public SaveGame Normalized()
        {
            if (caseId == null) caseId = string.Empty;
            if (listenerRoom == null) listenerRoom = string.Empty;
            if (heard == null) heard = new HeardEntry[0];
            if (voices == null) voices = new SpeakerDefinition[0];
            if (evidence == null) evidence = new string[0];

            for (int i = 0; i < heard.Length; i++)
            {
                if (heard[i] != null && heard[i].utteranceId == null) heard[i].utteranceId = string.Empty;
            }
            return this;
        }

        /// <summary>이 저장이 그 사건의 것인가. 사건 id가 비어 있으면 대조를 포기하고 받아들인다.</summary>
        public bool MatchesCase(string otherCaseId)
        {
            if (string.IsNullOrEmpty(caseId) || string.IsNullOrEmpty(otherCaseId)) return true;
            return caseId == otherCaseId;
        }

        /// <summary>저장 시각(UTC). 저장이 시각을 안 적었으면 <see cref="DateTime.MinValue"/>.</summary>
        public DateTime SavedAtUtc
        {
            get
            {
                if (savedAtUnixMs <= 0) return DateTime.MinValue;
                return Epoch.AddMilliseconds(savedAtUnixMs);
            }
        }

        /// <summary>지금을 Unix epoch 밀리초로. 저장을 만드는 쪽이 이 값을 넣어 준다 — 순수 함수 안에서 시계를 읽지 않는다.</summary>
        public static long NowUnixMs()
        {
            return (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;
        }

        /// <summary>범위를 벗어난 정수는 <see cref="Audibility.None"/>으로 떨어뜨린다.</summary>
        public static Audibility ToAudibility(int level)
        {
            if (level == (int)Audibility.Full) return Audibility.Full;
            if (level == (int)Audibility.Muffled) return Audibility.Muffled;
            return Audibility.None;
        }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// 저장에서 되살린 상태(순수 C#). <see cref="SaveGame"/>이 <i>파일에 적힌 모양</i>이라면 이쪽은 <i>게임이 쓰는 모양</i>이다.
    ///
    /// 여기까지가 순수 함수의 결과물이고, 살아 있는 객체에 밀어 넣는 것은 <see cref="SaveGameCodec.ApplyTo"/>가 따로 한다.
    /// 둘을 갈라 두면 "저장이 읽히는가"를 살아 있는 객체 없이 검사할 수 있다.
    /// </summary>
    public sealed class RestoredProgress
    {
        private readonly Dictionary<string, Audibility> _heard = new Dictionary<string, Audibility>();
        private readonly List<HeardEntry> _heardOrdered = new List<HeardEntry>();
        private readonly List<string> _evidence = new List<string>();
        private readonly List<string> _problems = new List<string>();

        internal RestoredProgress(SaveCompatibility compatibility)
        {
            Compatibility = compatibility;
            CaseId = string.Empty;
            ListenerRoom = string.Empty;
            VoicePairs = new SpeakerDefinition[0];
        }

        /// <summary>이 저장을 어떻게 받아들였는가.</summary>
        public SaveCompatibility Compatibility { get; private set; }

        /// <summary>실제로 이어서 놀 수 있는가. 거부한 저장은 나머지 필드가 전부 비어 있다.</summary>
        public bool IsUsable
        {
            get { return Compatibility == SaveCompatibility.Current || Compatibility == SaveCompatibility.Upgraded; }
        }

        /// <summary>저장에 적혀 있던 형식 번호(승격 전 값).</summary>
        public int Version { get; internal set; }

        public string CaseId { get; internal set; }
        public long SavedAtUnixMs { get; internal set; }

        /// <summary>회차 길이 안으로 자른 재생 위치.</summary>
        public int PositionMs { get; internal set; }

        /// <summary>평면도에 실재하는 방으로 확인된 청취점. 확인되지 않으면 빈 문자열.</summary>
        public string ListenerRoom { get; internal set; }

        public SpeakerDefinition[] VoicePairs { get; internal set; }

        /// <summary>들인 기록(저장에 적힌 순서).</summary>
        public IList<HeardEntry> Heard { get { return _heardOrdered.AsReadOnly(); } }

        public IList<string> Evidence { get { return _evidence.AsReadOnly(); } }

        /// <summary>버린 줄과 고친 값. 비어 있으면 저장이 그대로 들어왔다는 뜻이다.</summary>
        public IList<string> Problems { get { return _problems.AsReadOnly(); } }

        /// <summary>이 발화를 저장 시점까지 가장 잘 들은 등급. <see cref="HeardLookup"/>에 그대로 넘길 수 있다.</summary>
        public Audibility HeardLevel(string utteranceId)
        {
            Audibility level;
            if (!string.IsNullOrEmpty(utteranceId) && _heard.TryGetValue(utteranceId, out level)) return level;
            return Audibility.None;
        }

        /// <summary>내용까지 들었던 발화 수.</summary>
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

        internal void AddProblem(string problem) { _problems.Add(problem); }

        internal void AddEvidence(string evidenceId) { _evidence.Add(evidenceId); }

        /// <summary>같은 발화가 두 번 적혀 있으면 더 잘 들은 쪽을 남긴다.</summary>
        internal bool AddHeard(string utteranceId, Audibility level)
        {
            Audibility existing;
            if (_heard.TryGetValue(utteranceId, out existing))
            {
                if (level <= existing) return false;
                _heard[utteranceId] = level;
                for (int i = 0; i < _heardOrdered.Count; i++)
                {
                    if (_heardOrdered[i].utteranceId != utteranceId) continue;
                    _heardOrdered[i].level = (int)level;
                    break;
                }
                return false;
            }

            _heard[utteranceId] = level;
            _heardOrdered.Add(new HeardEntry { utteranceId = utteranceId, level = (int)level });
            return true;
        }
    }

    /// <summary>
    /// 저장에서 되살린 기록과 그 뒤로 실제 들은 것을 <b>합쳐서</b> 보는 창(순수 C#).
    ///
    /// <see cref="ListeningSession"/>은 들은 기록을 자기 안에 쥐고 있고 밖에서 집어넣을 문이 없다
    /// (지나치지 않은 것을 들었다고 쳐 주지 않으려는 설계다). 그래서 불러오기는 세션의 기록을 고치는 대신
    /// 되살린 기록을 <b>바닥</b>에 깔고, 그 위에 지금 세션이 새로 들은 것을 얹어 더 잘 들은 쪽을 답으로 준다.
    ///
    /// 이 창을 <see cref="UnheardReport"/>와 다음 저장(<see cref="SaveGameCodec.Capture(string,ListeningSession,HeardHistory,VoiceAssignment,EvidenceLog,long)"/>)이
    /// 함께 읽으므로, 불러온 판에서도 "아직 못 들은 것"과 "다시 저장한 것"이 어긋나지 않는다.
    /// </summary>
    public sealed class HeardHistory
    {
        private readonly ListeningSession _session;
        private readonly Dictionary<string, Audibility> _baseline = new Dictionary<string, Audibility>();

        /// <summary>새 판. 바닥에 깔 기록이 없다.</summary>
        public HeardHistory(ListeningSession session) : this(session, null) { }

        public HeardHistory(ListeningSession session, RestoredProgress restored)
        {
            _session = session;
            Adopt(restored);
        }

        /// <summary>되살린 기록을 바닥에 깐다. 이미 깔린 것보다 낮은 등급은 덮어쓰지 않는다.</summary>
        public void Adopt(RestoredProgress restored)
        {
            if (restored == null) return;
            IList<HeardEntry> entries = restored.Heard;
            for (int i = 0; i < entries.Count; i++)
            {
                HeardEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.utteranceId)) continue;
                Remember(entry.utteranceId, entry.Level);
            }
        }

        /// <summary>바닥 기록 한 줄. 세션이 이미 더 잘 들었으면 그쪽이 이긴다(합쳐 볼 때).</summary>
        public void Remember(string utteranceId, Audibility level)
        {
            if (string.IsNullOrEmpty(utteranceId) || level == Audibility.None) return;

            Audibility existing;
            if (_baseline.TryGetValue(utteranceId, out existing) && existing >= level) return;
            _baseline[utteranceId] = level;
        }

        /// <summary>바닥에 깔린 줄 수(세션이 새로 들은 것은 세지 않는다).</summary>
        public int BaselineCount { get { return _baseline.Count; } }

        /// <summary>바닥과 지금 세션 중 더 잘 들은 등급. <see cref="HeardLookup"/>에 그대로 넘길 수 있다.</summary>
        public Audibility Level(string utteranceId)
        {
            if (string.IsNullOrEmpty(utteranceId)) return Audibility.None;

            Audibility floor;
            if (!_baseline.TryGetValue(utteranceId, out floor)) floor = Audibility.None;

            Audibility live = _session != null ? _session.HeardLevel(utteranceId) : Audibility.None;
            return live > floor ? live : floor;
        }

        /// <summary>바닥 기록만 버린다. 세션이 쥔 기록은 그대로다.</summary>
        public void ForgetBaseline() { _baseline.Clear(); }
    }

    /// <summary>
    /// 상태 ↔ 저장을 옮기는 순수 함수 모음. 파일도 시계도 건드리지 않는다 —
    /// 저장 시각은 받아서 넣고, 읽고 쓰는 것은 Unity 쪽이 한다.
    /// </summary>
    public static class SaveGameCodec
    {
        /// <summary>
        /// 지금 상태를 저장 모양으로 옮긴다. 들은 기록은 <b>대본에 있는 발화만</b> 훑는다 —
        /// 대본에 없는 id가 저장에 섞여 들어가면 다음 판에서 조용히 버려지느니 애초에 적지 않는 편이 낫다.
        /// </summary>
        public static SaveGame Capture(
            string caseId,
            ScriptTimeline timeline,
            HeardLookup heard,
            int positionMs,
            string listenerRoom,
            VoiceAssignment voices,
            EvidenceLog evidence,
            long savedAtUnixMs)
        {
            var save = new SaveGame();
            save.version = SaveGame.CurrentVersion;
            save.caseId = caseId ?? string.Empty;
            save.savedAtUnixMs = savedAtUnixMs;
            save.listenerRoom = listenerRoom ?? string.Empty;

            int duration = timeline != null ? timeline.DurationMs : 0;
            save.positionMs = Clamp(positionMs, 0, duration);

            var entries = new List<HeardEntry>();
            if (timeline != null && heard != null)
            {
                IList<Utterance> utterances = timeline.Utterances;
                for (int i = 0; i < utterances.Count; i++)
                {
                    Utterance utterance = utterances[i];
                    if (utterance == null || string.IsNullOrEmpty(utterance.id)) continue;

                    Audibility level = heard(utterance.id);
                    if (level == Audibility.None) continue;
                    entries.Add(new HeardEntry { utteranceId = utterance.id, level = (int)level });
                }
            }
            save.heard = entries.ToArray();

            save.voices = voices != null ? voices.ToPairs() : new SpeakerDefinition[0];

            var collected = new List<string>();
            if (evidence != null)
            {
                IList<string> inOrder = evidence.InOrder;
                for (int i = 0; i < inOrder.Count; i++) collected.Add(inOrder[i]);
            }
            save.evidence = collected.ToArray();

            return save;
        }

        /// <summary>세션 하나에서 곧바로 저장을 뜬다(불러오기 없이 시작한 판).</summary>
        public static SaveGame Capture(string caseId, ListeningSession session, VoiceAssignment voices, EvidenceLog evidence, long savedAtUnixMs)
        {
            if (session == null) return Capture(caseId, null, null, 0, string.Empty, voices, evidence, savedAtUnixMs);
            return Capture(caseId, session.Timeline, session.HeardLevel, session.PositionMs, session.ListenerRoom, voices, evidence, savedAtUnixMs);
        }

        /// <summary>불러온 판에서 다시 저장한다. 되살린 기록과 그 뒤에 들은 것을 합쳐 적는다.</summary>
        public static SaveGame Capture(string caseId, ListeningSession session, HeardHistory history, VoiceAssignment voices, EvidenceLog evidence, long savedAtUnixMs)
        {
            if (history == null) return Capture(caseId, session, voices, evidence, savedAtUnixMs);

            ScriptTimeline timeline = session != null ? session.Timeline : null;
            int positionMs = session != null ? session.PositionMs : 0;
            string room = session != null ? session.ListenerRoom : string.Empty;
            return Capture(caseId, timeline, history.Level, positionMs, room, voices, evidence, savedAtUnixMs);
        }

        /// <summary>
        /// 저장을 게임이 쓰는 모양으로 되돌린다. 읽을 수 없는 저장은 <see cref="RestoredProgress.IsUsable"/>가 false이고
        /// 나머지가 비어 있다 — 반쯤 되살린 상태로 게임을 시작시키지 않는다.
        ///
        /// 대본·평면도와 대조해 <b>지금 데이터에 없는 것</b>(사라진 발화 id, 없는 방)은 문제 목록에 적고 버린다.
        /// 데이터가 바뀐 뒤에도 옛 저장으로 게임이 깨지지는 않아야 한다.
        /// </summary>
        public static RestoredProgress Restore(SaveGame save, ScriptTimeline timeline, AudibilityModel audibility)
        {
            SaveCompatibility compatibility = CheckCompatibility(save);
            var progress = new RestoredProgress(compatibility);
            if (save != null) progress.Version = save.version;
            if (!progress.IsUsable) return progress;

            save.Normalized();
            progress.CaseId = save.caseId;
            progress.SavedAtUnixMs = save.savedAtUnixMs;

            int duration = timeline != null ? timeline.DurationMs : 0;
            int clamped = Clamp(save.positionMs, 0, duration);
            if (clamped != save.positionMs)
                progress.AddProblem("재생 위치 " + save.positionMs + "ms가 회차 길이 " + duration + "ms 밖이라 " + clamped + "ms로 당겼다.");
            progress.PositionMs = clamped;

            progress.ListenerRoom = RestoreListenerRoom(save.listenerRoom, audibility, progress);
            RestoreHeard(save, timeline, progress);
            progress.VoicePairs = RestoreVoices(save, progress);
            RestoreEvidence(save, progress);

            return progress;
        }

        /// <summary>
        /// 되살린 상태를 살아 있는 객체에 밀어 넣는다. 돌려주는 값은 밀어 넣으면서 생긴 문제 목록이다
        /// (<see cref="RestoredProgress.Problems"/>와 합쳐서 보여 주면 된다).
        ///
        /// <b>들은 기록은 여기서 넣지 않는다</b> — 세션은 "귀가 그 자리에 있던 순간"에만 기록하고,
        /// 밖에서 집어넣으면 그 규칙이 무너진다. 되살린 기록은 <see cref="HeardHistory"/>로 세션 위에 겹쳐 본다.
        /// </summary>
        public static List<string> ApplyTo(RestoredProgress progress, ListeningSession session, VoiceAssignment voices, EvidenceLog evidence)
        {
            var problems = new List<string>();
            if (progress == null || !progress.IsUsable)
            {
                problems.Add("되살릴 수 없는 저장이다.");
                return problems;
            }

            if (session != null)
            {
                session.MoveTo(progress.ListenerRoom);
                session.SeekTo(progress.PositionMs);
            }

            if (voices != null)
            {
                List<string> voiceProblems = voices.Restore(progress.VoicePairs);
                for (int i = 0; i < voiceProblems.Count; i++) problems.Add("목소리 " + voiceProblems[i]);
            }

            if (evidence != null)
            {
                IList<string> collected = progress.Evidence;
                for (int i = 0; i < collected.Count; i++) evidence.Collect(collected[i]);
            }

            return problems;
        }

        /// <summary>이 저장을 읽을 수 있는가. 읽을 수 있으면서 형식이 낮으면 <see cref="TryUpgrade"/>가 올려 준다.</summary>
        public static SaveCompatibility CheckCompatibility(SaveGame save)
        {
            if (save == null) return SaveCompatibility.Missing;
            if (save.version <= 0) return SaveCompatibility.Unversioned;
            if (save.version > SaveGame.CurrentVersion) return SaveCompatibility.FromFuture;
            if (save.version == SaveGame.CurrentVersion) return SaveCompatibility.Current;
            if (save.version < SaveGame.OldestUpgradableVersion) return SaveCompatibility.TooOld;
            return TryUpgrade(save) ? SaveCompatibility.Upgraded : SaveCompatibility.TooOld;
        }

        /// <summary>
        /// 옛 형식을 한 단계씩 올려 지금 형식으로 만든다. 끝까지 올라가면 true.
        /// 형식을 바꿀 때는 <see cref="SaveGame.CurrentVersion"/>을 올리고 <see cref="StepUp"/>에 한 단계를 더한다.
        /// </summary>
        public static bool TryUpgrade(SaveGame save)
        {
            if (save == null) return false;
            if (save.version <= 0) return false;

            save.Normalized();
            while (save.version < SaveGame.CurrentVersion)
            {
                if (!StepUp(save)) return false;
            }
            return save.version == SaveGame.CurrentVersion;
        }

        /// <summary>
        /// 한 단계 승격. 아직 올릴 단계가 없다(형식은 1이 처음이다).
        /// 형식 2가 생기면 여기에 <c>case 1:</c>을 더해 필드를 옮기고 <c>save.version = 2</c>로 올린다.
        /// </summary>
        private static bool StepUp(SaveGame save)
        {
            switch (save.version)
            {
                default:
                    return false;
            }
        }

        private static string RestoreListenerRoom(string roomId, AudibilityModel audibility, RestoredProgress progress)
        {
            if (string.IsNullOrEmpty(roomId)) return string.Empty;

            RoomLayout layout = audibility != null ? audibility.Layout : null;
            if (layout == null) return roomId;

            RoomDefinition room;
            if (layout.TryGetRoom(roomId, out room)) return roomId;

            progress.AddProblem("청취점 '" + roomId + "'은(는) 없는 방이라 비웠다.");
            return string.Empty;
        }

        private static void RestoreHeard(SaveGame save, ScriptTimeline timeline, RestoredProgress progress)
        {
            for (int i = 0; i < save.heard.Length; i++)
            {
                HeardEntry entry = save.heard[i];
                string label = "들은 기록[" + i + "]";

                if (entry == null) { progress.AddProblem(label + "가 비어 있다."); continue; }
                if (string.IsNullOrEmpty(entry.utteranceId)) { progress.AddProblem(label + ": 발화 id가 비어 있다."); continue; }

                label = label + "('" + entry.utteranceId + "')";
                if (!entry.IsValidLevel)
                {
                    progress.AddProblem(label + ": 모르는 등급 " + entry.level + "이라 버렸다.");
                    continue;
                }

                Audibility level = entry.Level;
                if (level == Audibility.None) continue;

                if (timeline != null)
                {
                    Utterance utterance;
                    if (!timeline.TryGet(entry.utteranceId, out utterance))
                    {
                        progress.AddProblem(label + ": 지금 대본에 없는 발화라 버렸다.");
                        continue;
                    }
                }

                if (!progress.AddHeard(entry.utteranceId, level))
                    progress.AddProblem(label + ": 같은 발화가 두 번 적혀 있어 더 잘 들은 쪽만 남겼다.");
            }
        }

        private static SpeakerDefinition[] RestoreVoices(SaveGame save, RestoredProgress progress)
        {
            var pairs = new List<SpeakerDefinition>();
            var seenVoices = new HashSet<string>();

            for (int i = 0; i < save.voices.Length; i++)
            {
                SpeakerDefinition pair = save.voices[i];
                string label = "목소리 배정[" + i + "]";

                if (pair == null) { progress.AddProblem(label + "가 비어 있다."); continue; }
                if (string.IsNullOrEmpty(pair.voiceId)) { progress.AddProblem(label + ": 목소리 id가 비어 있다."); continue; }
                if (string.IsNullOrEmpty(pair.npcId)) { progress.AddProblem(label + "('" + pair.voiceId + "'): 붙은 이름이 없어 버렸다."); continue; }
                if (!seenVoices.Add(pair.voiceId))
                {
                    progress.AddProblem(label + "('" + pair.voiceId + "'): 같은 목소리가 두 번 적혀 있어 뒤엣것을 버렸다.");
                    continue;
                }

                pairs.Add(new SpeakerDefinition { voiceId = pair.voiceId, npcId = pair.npcId });
            }
            return pairs.ToArray();
        }

        private static void RestoreEvidence(SaveGame save, RestoredProgress progress)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < save.evidence.Length; i++)
            {
                string evidenceId = save.evidence[i];
                if (string.IsNullOrEmpty(evidenceId))
                {
                    progress.AddProblem("단서[" + i + "]: id가 비어 있다.");
                    continue;
                }
                if (!seen.Add(evidenceId)) continue;
                progress.AddEvidence(evidenceId);
            }
        }

        private static int Clamp(int value, int low, int high)
        {
            if (high < low) high = low;
            if (value < low) return low;
            return value > high ? high : value;
        }
    }
}
