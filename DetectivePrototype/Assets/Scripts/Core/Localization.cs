using System;
using System.Collections.Generic;

namespace Detective.Core
{
    /// <summary>
    /// 문자열 표 하나. id → 글자. JsonUtility가 읽을 수 있게 배열로 둔다(딕셔너리 불가).
    /// </summary>
    [Serializable]
    public class LocaleEntry
    {
        public string id;
        public string text;
    }

    /// <summary>한 언어의 문자열 묶음 파일 형식.</summary>
    [Serializable]
    public class LocaleFile
    {
        /// <summary>"ko" · "en" 같은 언어 코드.</summary>
        public string locale;

        public LocaleEntry[] entries = new LocaleEntry[0];

        public LocaleFile Normalized()
        {
            if (entries == null) entries = new LocaleEntry[0];
            if (locale == null) locale = string.Empty;
            return this;
        }
    }

    /// <summary>
    /// 지금 쓰는 언어와 그 문자열 표(순수 C#).
    ///
    /// <b>한국어는 표가 없어도 돈다.</b> 코드에서 부르는 방식이 이렇다:
    /// <code>Localization.Text("event.door.muffled", "어디선가 문이 여닫힌다")</code>
    /// 둘째 인자가 한국어 원문이고, 그것이 곧 기본값이다. 그래서
    ///   * 표 파일이 없거나 깨져도 화면이 비지 않는다 — 한국어가 나온다.
    ///   * 번역이 빠진 항목만 한국어로 남는다(전체가 깨지지 않는다).
    ///   * 코드를 읽는 사람이 원문을 그 자리에서 본다. 키만 있으면 무슨 말인지 알 수 없다.
    ///
    /// 큰 판 갈아치우기(big-bang migration)를 하지 않는 이유가 이것이다. 한 번에 모든
    /// 문자열을 표로 옮기면 그 사이에 화면이 비거나 키가 새는 구간이 반드시 생긴다.
    ///
    /// 콘텐츠(대본·사건 문구·방 이름)는 코드가 아니라 데이터에 있으므로 <b>덮어쓰기 파일</b>로
    /// 번역한다 — 한국어 원본 JSON은 그대로 두고, 같은 id에 영어 글자만 얹는다.
    /// 그래야 원본이 하나로 유지되고 타이밍·구조가 갈라지지 않는다.
    /// </summary>
    public static class Localization
    {
        public const string Korean = "ko";
        public const string English = "en";

        /// <summary>번역이 없을 때 쓰는 언어. 이 저장소의 원문 언어다.</summary>
        public const string SourceLocale = Korean;

        private static string _current = SourceLocale;
        private static readonly Dictionary<string, string> _table = new Dictionary<string, string>();

        /// <summary>빠진 키. 개발 중에 무엇을 아직 번역하지 않았는지 보는 데 쓴다.</summary>
        private static readonly HashSet<string> _missing = new HashSet<string>();

        /// <summary>지금 언어. 바꾸면 표를 비우므로 다시 <see cref="Load"/>해야 한다.</summary>
        public static string Current
        {
            get { return _current; }
        }

        public static bool IsSourceLocale
        {
            get { return _current == SourceLocale; }
        }

        /// <summary>
        /// 이 언어로 갈아탄다. 표를 비운다 — 이전 언어의 글자가 섞여 남으면
        /// 화면에 두 언어가 뒤엉킨다.
        /// </summary>
        public static void SetLocale(string locale)
        {
            _current = string.IsNullOrEmpty(locale) ? SourceLocale : locale;
            _table.Clear();
            _missing.Clear();
        }

        /// <summary>문자열 표를 얹는다. 여러 파일을 겹쳐 쌓을 수 있다(UI + 콘텐츠).</summary>
        public static void Load(LocaleFile file)
        {
            if (file == null) return;
            file.Normalized();
            for (int i = 0; i < file.entries.Length; i++)
            {
                LocaleEntry entry = file.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                if (entry.text == null) continue;
                _table[entry.id] = entry.text;
            }
        }

        /// <summary>표를 통째로 비운다(테스트·언어 재설정).</summary>
        public static void Clear()
        {
            _table.Clear();
            _missing.Clear();
        }

        /// <summary>
        /// 이 키의 글자. 번역이 없으면 <paramref name="sourceText"/>(한국어 원문)를 돌려준다.
        /// 원문 언어일 때는 표를 아예 보지 않는다 — 원문이 늘 이긴다.
        /// </summary>
        public static string Text(string key, string sourceText)
        {
            if (IsSourceLocale || string.IsNullOrEmpty(key)) return sourceText;

            string translated;
            if (_table.TryGetValue(key, out translated) && translated.Length > 0) return translated;

            _missing.Add(key);
            return sourceText;
        }

        /// <summary>
        /// 데이터에서 온 콘텐츠의 번역. 원문이 데이터에 있으므로 키가 곧 그 데이터의 id다
        /// (발화 id, 방 id 등). 번역이 없으면 원문을 그대로 쓴다.
        /// </summary>
        public static string Content(string id, string sourceText)
        {
            return Text(id, sourceText);
        }

        /// <summary>지금까지 찾지 못한 키들. 정렬해서 돌려준다.</summary>
        public static List<string> MissingKeys()
        {
            var keys = new List<string>(_missing);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        public static int LoadedCount { get { return _table.Count; } }

        /// <summary>
        /// 한국어 조사를 붙인다. <b>영어에서는 붙이지 않는다</b> — 조사가 없는 언어에
        /// "Claraeul"을 만들면 안 된다. 자동 생성 대사가 이 경로를 탄다.
        /// </summary>
        public static string WithParticle(string word, ParticleKind kind)
        {
            if (!IsSourceLocale) return word;
            switch (kind)
            {
                case ParticleKind.EulReul: return KoreanText.EulReul(word);
                case ParticleKind.IGa: return KoreanText.IGa(word);
                case ParticleKind.WaGwa: return KoreanText.WaGwa(word);
                case ParticleKind.EunNeun: return KoreanText.EunNeun(word);
                default: return word;
            }
        }
    }

    /// <summary>한국어 조사 종류.</summary>
    public enum ParticleKind
    {
        None = 0,
        EulReul = 1,
        IGa = 2,
        WaGwa = 3,
        EunNeun = 4
    }
}
