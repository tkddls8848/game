using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Dialogue;
using Detective.NPC;

namespace Detective.Investigation
{
    /// <summary>행적 기록의 출처. 뒤로 갈수록 믿을 만하다.</summary>
    public enum RecordKind
    {
        /// <summary>본인이 그렇게 말했다(거짓일 수 있다).</summary>
        Testimony = 1,

        /// <summary>다른 사람이 봤다고 말했다.</summary>
        Sighting = 2,

        /// <summary>물증이 확인해 준다.</summary>
        Evidence = 3
    }

    /// <summary>"NpcId가 Ms에 RoomId에 있었다"는 기록 한 건과 그 출처.</summary>
    public struct TimelineRecord
    {
        public readonly string NpcId;
        public readonly int Ms;
        public readonly string RoomId;
        public readonly RecordKind Kind;

        /// <summary>Testimony/Sighting이면 말한 사람 id, Evidence면 단서 id.</summary>
        public readonly string SourceId;

        public TimelineRecord(string npcId, int ms, string roomId, RecordKind kind, string sourceId)
        {
            NpcId = npcId;
            Ms = ms;
            RoomId = roomId;
            Kind = kind;
            SourceId = sourceId;
        }

        /// <summary>Ms가 속한 10분 칸. 수사 노트 표·관찰 눈금용 파생값.</summary>
        public int Tick { get { return GameTime.TickOf(Ms); } }
    }

    /// <summary>
    /// 플레이어가 지금까지 모은 정보로 복원한 타임라인(순수 C#).
    /// 실제 스케줄은 절대 직접 보지 않는다 — 들은 대사와 얻은 단서만으로 만든다.
    /// 서로 어긋나는 기록도 그대로 둔다. 모순을 찾는 건 플레이어의 몫이다(§11).
    /// </summary>
    public sealed class TimelineBoard : ITimelinePlacementSource
    {
        private readonly CaseDatabase _database;
        private readonly List<TimelineRecord> _records = new List<TimelineRecord>();

        private TimelineBoard(CaseDatabase database)
        {
            _database = database;
        }

        public IList<TimelineRecord> Records { get { return _records.AsReadOnly(); } }

        public static TimelineBoard Build(InvestigationState state)
        {
            var board = new TimelineBoard(state.Database);
            CaseDatabase database = state.Database;

            IList<NpcDefinition> npcs = database.Npcs.All;
            for (int n = 0; n < npcs.Count; n++)
            {
                NpcDefinition npc = npcs[n];
                List<ConversationLine> lines = ConversationBuilder.AllLines(database, npc.id);
                for (int i = 0; i < lines.Count; i++)
                {
                    ConversationLine line = lines[i];
                    if (!state.HasHeard(line.Key)) continue;

                    if (line.RevealsClaims)
                    {
                        // claims 데이터가 10분 칸이라 칸마다 한 건씩 적는다.
                        for (int t = GameTime.FirstTick; t <= GameTime.LastTick; t++)
                        {
                            int ms = GameTime.TickToMs(t);
                            board.Add(npc.id, ms, NpcSchedule.ClaimedRoomAt(npc, ms), RecordKind.Testimony, npc.id);
                        }
                    }
                    if (line.ClaimMs != GameTime.NoTime)
                    {
                        board.Add(npc.id, line.ClaimMs, line.ClaimRoom, RecordKind.Testimony, npc.id);
                    }
                    if (line.IsSighting)
                    {
                        Sighting s = line.Sighting;
                        board.Add(s.TargetId, s.Ms, s.RoomId, RecordKind.Sighting, s.ObserverId);
                    }
                }
            }

            IList<string> collected = state.Evidence.InOrder;
            for (int i = 0; i < collected.Count; i++)
            {
                EvidenceDefinition evidence;
                if (!database.Evidence.TryGet(collected[i], out evidence) || !evidence.RevealsWhereabouts) continue;
                board.Add(evidence.revealNpc, evidence.RevealMs, evidence.revealRoom, RecordKind.Evidence, evidence.id);
            }

            return board;
        }

        private void Add(string npcId, int ms, string roomId, RecordKind kind, string sourceId)
        {
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(roomId) || !GameTime.IsValid(ms)) return;

            for (int i = 0; i < _records.Count; i++)
            {
                TimelineRecord r = _records[i];
                if (r.NpcId == npcId && r.Ms == ms && r.RoomId == roomId && r.Kind == kind && r.SourceId == sourceId) return;
            }
            _records.Add(new TimelineRecord(npcId, ms, roomId, kind, sourceId));
        }

        /// <summary>한 사람의 한 10분 칸(틱)에 대한 기록 전부(들어온 순서). 수사 노트 표의 한 칸이다.</summary>
        public List<TimelineRecord> RecordsInTick(string npcId, int tick)
        {
            var result = new List<TimelineRecord>();
            if (!GameTime.IsValidTick(tick)) return result;

            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].NpcId == npcId && _records[i].Tick == tick) result.Add(_records[i]);
            }
            return result;
        }

        /// <summary>그 시각(ms)이 속한 10분 칸의 기록 전부. 기록은 아직 10분 칸 단위로만 들어온다.</summary>
        public List<TimelineRecord> RecordsAt(string npcId, int ms)
        {
            return RecordsInTick(npcId, GameTime.TickOf(ms));
        }

        /// <summary>
        /// 관찰 화면에 세울 위치: 가장 믿을 만한 출처(물증 &gt; 목격 &gt; 증언), 같으면 나중에 알게 된 것.
        /// </summary>
        public NpcPlacement PlacementAt(NpcDefinition npc, int ms)
        {
            if (npc == null) return NpcPlacement.Unknown;

            List<TimelineRecord> records = RecordsAt(npc.id, ms);
            if (records.Count == 0) return NpcPlacement.Unknown;

            TimelineRecord best = records[0];
            for (int i = 1; i < records.Count; i++)
            {
                if ((int)records[i].Kind >= (int)best.Kind) best = records[i];
            }
            return new NpcPlacement(best.RoomId, SourceLabel(best, true));
        }

        /// <summary>출처 표시. 짧게: "본인" / "마르코" / "물증", 길게: "본인 증언" / "마르코 목격" / "물증: 전화 기록부".</summary>
        public string SourceLabel(TimelineRecord record, bool verbose)
        {
            switch (record.Kind)
            {
                case RecordKind.Testimony:
                    return verbose ? "본인 증언" : "본인";
                case RecordKind.Sighting:
                    string name = ShortName(_database.Npcs.DisplayNameOf(record.SourceId));
                    return verbose ? name + " 목격" : name;
                default:
                    return verbose ? "물증: " + _database.Evidence.NameOf(record.SourceId) : "물증";
            }
        }

        /// <summary>"마르코 벨리니" → "마르코". 좁은 칸에 넣기 위한 이름.</summary>
        public static string ShortName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return string.Empty;
            int space = displayName.IndexOf(' ');
            return space > 0 ? displayName.Substring(0, space) : displayName;
        }
    }
}
