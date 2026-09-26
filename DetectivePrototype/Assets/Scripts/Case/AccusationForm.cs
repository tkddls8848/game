using System.Collections.Generic;
using System.Globalization;
using Detective.Core;
using Detective.Data;
using Detective.Investigation;
using Detective.NPC;

namespace Detective.Case
{
    /// <summary>
    /// 고발 화면의 입력 상태(순수 C#). 항목마다 선택지 목록과 현재 선택 번호를 들고 있다.
    /// 결정적 증거는 플레이어가 실제로 얻은 단서 중에서만 고를 수 있다.
    /// </summary>
    public sealed class AccusationForm
    {
        /// <summary>
        /// 고발 항목 이름. 정적 배열로 두면 언어를 바꿔도 첫 값이 굳으므로 속성으로 만든다.
        /// </summary>
        public static string[] FieldLabels
        {
            get
            {
                return new[]
                {
                    Localization.Text("accuse.field.culprit", "범인"),
                    Localization.Text("accuse.field.motive", "동기"),
                    Localization.Text("accuse.field.time", "범행 시각"),
                    Localization.Text("accuse.field.room", "범행 장소"),
                    Localization.Text("accuse.field.method", "범행 수법"),
                    Localization.Text("accuse.field.evidence", "결정적 증거")
                };
            }
        }

        private readonly List<ChoiceDefinition>[] _options = new List<ChoiceDefinition>[CaseGradeResult.FieldCount];
        private readonly int[] _selected = new int[CaseGradeResult.FieldCount];

        public AccusationForm(InvestigationState state)
        {
            CaseDatabase database = state.Database;
            CaseDefinition definition = database.Case;

            var culprits = new List<ChoiceDefinition>();
            List<NpcDefinition> suspects = database.Npcs.Suspects;
            for (int i = 0; i < suspects.Count; i++) culprits.Add(Choice(suspects[i].id, suspects[i].displayName));
            _options[(int)AccusationField.Culprit] = culprits;

            _options[(int)AccusationField.Motive] = new List<ChoiceDefinition>(definition.motives);

            // 범행 시각은 10분 칸 단위로 고른다. 선택지 id는 그 칸이 시작하는 ms.
            var times = new List<ChoiceDefinition>();
            for (int t = GameTime.FirstTick; t <= GameTime.LastTick; t++)
            {
                int ms = GameTime.TickToMs(t);
                times.Add(Choice(ms.ToString(CultureInfo.InvariantCulture), GameTime.ToLabel(ms)));
            }
            _options[(int)AccusationField.Time] = times;

            var rooms = new List<ChoiceDefinition>();
            IList<RoomDefinition> layoutRooms = database.Layout.Rooms;
            for (int i = 0; i < layoutRooms.Count; i++) rooms.Add(Choice(layoutRooms[i].id, layoutRooms[i].displayName));
            _options[(int)AccusationField.Place] = rooms;

            _options[(int)AccusationField.Method] = new List<ChoiceDefinition>(definition.methods);

            var evidence = new List<ChoiceDefinition>();
            IList<string> collected = state.Evidence.InOrder;
            for (int i = 0; i < collected.Count; i++) evidence.Add(Choice(collected[i], database.Evidence.NameOf(collected[i])));
            if (evidence.Count == 0) evidence.Add(Choice(string.Empty, Localization.Text("accuse.noevidence", "(확보한 단서 없음)")));
            _options[(int)AccusationField.Evidence] = evidence;
        }

        public int OptionCount(AccusationField field) { return _options[(int)field].Count; }

        public int SelectedIndex(AccusationField field) { return _selected[(int)field]; }

        public ChoiceDefinition Selected(AccusationField field)
        {
            List<ChoiceDefinition> options = _options[(int)field];
            return options.Count == 0 ? Choice(string.Empty, "-") : options[_selected[(int)field]];
        }

        /// <summary>선택지를 앞뒤로 돌린다(끝에서 처음으로 넘어간다).</summary>
        public void Cycle(AccusationField field, int delta)
        {
            int count = OptionCount(field);
            if (count == 0) return;
            int index = (_selected[(int)field] + delta) % count;
            if (index < 0) index += count;
            _selected[(int)field] = index;
        }

        public CaseAnswer ToSubmission()
        {
            // CaseAnswer는 JSON 호환을 위해 아직 틱으로 적는다(경계).
            int ms;
            if (!int.TryParse(Selected(AccusationField.Time).id, NumberStyles.None, CultureInfo.InvariantCulture, out ms)) ms = GameTime.NoTime;
            int tick = GameTime.TickOf(ms);

            return new CaseAnswer
            {
                culprit = Selected(AccusationField.Culprit).id,
                motive = Selected(AccusationField.Motive).id,
                tick = tick,
                room = Selected(AccusationField.Place).id,
                method = Selected(AccusationField.Method).id,
                evidence = Selected(AccusationField.Evidence).id
            };
        }

        private static ChoiceDefinition Choice(string id, string label)
        {
            return new ChoiceDefinition { id = id, label = label };
        }
    }
}
