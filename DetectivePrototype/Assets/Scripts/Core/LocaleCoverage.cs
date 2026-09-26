using System;
using System.Collections.Generic;

namespace Detective.Core
{
    /// <summary>번역 진행 상황. 무엇이 아직 한국어로 남아 있는지 기계가 센다.</summary>
    public sealed class LocaleCoverageReport
    {
        /// <summary>번역이 있어야 하는데 없는 id.</summary>
        public readonly List<string> Missing = new List<string>();

        /// <summary>표에는 있는데 아무도 쓰지 않는 id. 원본이 바뀌면 여기로 떨어진다.</summary>
        public readonly List<string> Orphaned = new List<string>();

        /// <summary>번역이 원문과 글자 그대로 같은 id. 붙여넣기만 하고 안 고친 자리다.</summary>
        public readonly List<string> Untranslated = new List<string>();

        public int RequiredCount;
        public int TranslatedCount;

        public float Ratio
        {
            get { return RequiredCount <= 0 ? 0f : TranslatedCount / (float)RequiredCount; }
        }

        public bool IsComplete
        {
            get { return RequiredCount > 0 && Missing.Count == 0 && Untranslated.Count == 0; }
        }
    }

    /// <summary>
    /// 번역이 얼마나 됐는지 센다(순수 C#).
    ///
    /// 왜 필요한가: 로컬라이제이션은 "다 했다"고 말하기 쉬운 작업이다. 화면을 몇 개 열어 보고
    /// 영어가 나오면 끝난 것처럼 보인다. 그런데 대사 267줄 중 30줄만 번역돼 있어도 첫 화면은
    /// 완벽하게 영어로 보인다 — 원문 대체(fallback) 때문에 <b>깨지지 않고 조용히 한국어로 남는다</b>.
    ///
    /// 그래서 진행률을 기계가 세게 한다. 출시 판정에 쓸 수 있는 숫자여야 한다.
    ///
    /// <c>Untranslated</c>를 따로 세는 이유: 번역 파일을 만들 때 한국어를 그대로 복사해 두고
    /// 나중에 고치는 일이 흔하다. 그 자리는 "번역 있음"으로 세어지지만 실제로는 한국어다.
    /// </summary>
    public static class LocaleCoverage
    {
        /// <summary>
        /// <paramref name="required"/>는 번역이 있어야 하는 id → 한국어 원문.
        /// <paramref name="translations"/>는 번역 파일의 내용.
        /// </summary>
        public static LocaleCoverageReport Check(IDictionary<string, string> required, LocaleFile translations)
        {
            var report = new LocaleCoverageReport();
            if (required == null) return report;

            var table = new Dictionary<string, string>();
            if (translations != null)
            {
                translations.Normalized();
                for (int i = 0; i < translations.entries.Length; i++)
                {
                    LocaleEntry entry = translations.entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                    table[entry.id] = entry.text ?? string.Empty;
                }
            }

            report.RequiredCount = required.Count;

            foreach (KeyValuePair<string, string> pair in required)
            {
                string translated;
                if (!table.TryGetValue(pair.Key, out translated) || translated.Length == 0)
                {
                    report.Missing.Add(pair.Key);
                    continue;
                }
                if (translated == pair.Value)
                {
                    // 원문을 복사해 둔 자리. 세어 주면 진행률이 거짓이 된다.
                    report.Untranslated.Add(pair.Key);
                    continue;
                }
                report.TranslatedCount++;
            }

            foreach (KeyValuePair<string, string> pair in table)
            {
                if (!required.ContainsKey(pair.Key)) report.Orphaned.Add(pair.Key);
            }

            report.Missing.Sort(StringComparer.Ordinal);
            report.Orphaned.Sort(StringComparer.Ordinal);
            report.Untranslated.Sort(StringComparer.Ordinal);
            return report;
        }

        /// <summary>
        /// 대본에서 번역이 필요한 항목을 모은다 — 발화 글자와 사실 설명.
        /// id는 대본의 id를 그대로 쓴다(별도 키를 만들면 대본이 바뀔 때 어긋난다).
        /// </summary>
        public static Dictionary<string, string> RequiredFromScript(Eavesdrop.ScriptDefinition script)
        {
            var required = new Dictionary<string, string>();
            if (script == null) return required;
            script.Normalized();

            for (int i = 0; i < script.utterances.Length; i++)
            {
                Eavesdrop.Utterance u = script.utterances[i];
                if (u == null || string.IsNullOrEmpty(u.id)) continue;
                if (!string.IsNullOrEmpty(u.text)) required[u.id] = u.text;
            }
            for (int i = 0; i < script.facts.Length; i++)
            {
                Eavesdrop.ScriptFact f = script.facts[i];
                if (f == null || string.IsNullOrEmpty(f.id)) continue;
                if (!string.IsNullOrEmpty(f.description)) required[f.id] = f.description;
            }
            for (int i = 0; i < script.conclusions.Length; i++)
            {
                Eavesdrop.ScriptConclusion c = script.conclusions[i];
                if (c == null || string.IsNullOrEmpty(c.id)) continue;
                if (!string.IsNullOrEmpty(c.text)) required[c.id] = c.text;
            }
            return required;
        }

        /// <summary>
        /// 이벤트에서 번역이 필요한 항목 — 같은 방에서 들었을 때 보이는 묘사.
        /// 벽 너머 묘사는 <c>EventKind.MuffledDescription</c>이 만들고 UI 표에서 번역되므로 여기 없다.
        /// </summary>
        public static Dictionary<string, string> RequiredFromEvents(Eavesdrop.EventTable table)
        {
            var required = new Dictionary<string, string>();
            if (table == null) return required;
            table.Normalized();
            for (int i = 0; i < table.events.Length; i++)
            {
                Eavesdrop.ScriptEvent e = table.events[i];
                if (e == null) continue;
                Add(required, e.id, e.text);
                if (e.muffledText.Length > 0) Add(required, e.id + ".muffled", e.muffledText);
            }
            return required;
        }

        /// <summary>사건 문구에서 번역이 필요한 항목. 제목·도입·해결·실패·선택지 라벨.</summary>
        public static Dictionary<string, string> RequiredFromCase(Data.CaseDefinition definition)
        {
            var required = new Dictionary<string, string>();
            if (definition == null) return required;
            definition.Normalized();

            string prefix = (definition.id ?? "case") + ".";
            Add(required, prefix + "title", definition.title);
            Add(required, prefix + "intro", definition.intro);
            Add(required, prefix + "solved", definition.solvedText);
            Add(required, prefix + "failed", definition.failedText);

            for (int i = 0; i < definition.motives.Length; i++)
            {
                Data.ChoiceDefinition choice = definition.motives[i];
                if (choice != null) Add(required, choice.id, choice.label);
            }
            for (int i = 0; i < definition.methods.Length; i++)
            {
                Data.ChoiceDefinition choice = definition.methods[i];
                if (choice != null) Add(required, choice.id, choice.label);
            }
            return required;
        }

        /// <summary>방 이름. id를 키로 쓴다.</summary>
        public static Dictionary<string, string> RequiredFromRooms(Data.RoomTable table)
        {
            var required = new Dictionary<string, string>();
            if (table == null) return required;
            table.Normalized();
            for (int i = 0; i < table.rooms.Length; i++)
            {
                Data.RoomDefinition room = table.rooms[i];
                if (room == null) continue;
                Add(required, room.id, room.displayName);
            }
            return required;
        }

        private static void Add(Dictionary<string, string> into, string id, string text)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(text)) return;
            into[id] = text;
        }
    }
}
