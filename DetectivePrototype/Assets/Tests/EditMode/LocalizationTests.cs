using System.Collections.Generic;
using System.IO;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 로컬라이제이션을 검사한다.
    ///
    /// 여기서 조용히 깨지는 것이 하나 있다. 원문 대체(fallback)를 쓰기 때문에
    /// <b>번역이 30줄만 돼 있어도 화면은 완벽하게 영어로 보인다</b> — 첫 화면의 UI만 번역돼
    /// 있으면 그렇다. 나머지는 깨지지 않고 조용히 한국어로 남는다. 그래서 "다 했다"고
    /// 말하기 가장 쉬운 작업이고, 진행률을 기계가 세지 않으면 출시 판정에 쓸 수 없다.
    ///
    /// 또 한 가지: 번역 파일을 만들 때 한국어를 그대로 복사해 두는 일이 흔하다.
    /// 그 자리는 "번역 있음"으로 세어지지만 실제로는 한국어다. 따로 센다.
    /// </summary>
    public class LocalizationTests
    {
        private const string RoomsPath = "Assets/Resources/GameData/rooms.json";
        private const string CasePath = "Assets/Resources/GameData/cases/case_02.json";
        private const string ScriptPath = "Assets/Resources/GameData/cases/case_02/script.json";
        private const string UiEnPath = "Assets/Resources/GameData/locale/ui.en.json";
        private const string ContentEnPath = "Assets/Resources/GameData/locale/content.en.json";
        private const string ScriptEnPath = "Assets/Resources/GameData/locale/case_02.script.en.json";

        [TearDown]
        public void TearDown()
        {
            // 정적 상태이므로 테스트마다 되돌린다. 안 그러면 다른 테스트가 영어로 돈다.
            Localization.SetLocale(Localization.SourceLocale);
        }

        // ── 원문 대체 규칙 ─────────────────────────────────────

        [Test]
        public void KoreanIsAlwaysAvailable_EvenWithNoTable()
        {
            Localization.SetLocale(Localization.Korean);
            Assert.AreEqual("어디선가 문이 여닫힌다",
                Localization.Text("event.door", "어디선가 문이 여닫힌다"),
                "표가 없어도 한국어는 나와야 한다 — 화면이 비면 안 된다");
        }

        [Test]
        public void SourceLocale_IgnoresTheTableEntirely()
        {
            // 한국어일 때 영어 표가 남아 있어도 한국어가 이겨야 한다.
            Localization.SetLocale(Localization.English);
            Localization.Load(new LocaleFile
            {
                locale = "en",
                entries = new[] { new LocaleEntry { id = "event.door", text = "a door" } }
            });
            Assert.AreEqual("a door", Localization.Text("event.door", "문"));

            Localization.SetLocale(Localization.Korean);
            Assert.AreEqual("문", Localization.Text("event.door", "문"),
                "원문 언어에서는 표를 보지 않는다");
        }

        [Test]
        public void MissingTranslation_FallsBackToKorean_AndIsRecorded()
        {
            Localization.SetLocale(Localization.English);
            Assert.AreEqual("발소리가 지나간다",
                Localization.Text("event.footsteps", "발소리가 지나간다"),
                "번역이 없으면 깨지지 말고 한국어로 남아야 한다");

            Assert.Contains("event.footsteps", Localization.MissingKeys(),
                "빠진 키는 기록돼야 한다 — 그러지 않으면 무엇이 안 됐는지 알 수 없다");
        }

        [Test]
        public void SwitchingLocale_ClearsTheTable()
        {
            Localization.SetLocale(Localization.English);
            Localization.Load(new LocaleFile
            {
                locale = "en",
                entries = new[] { new LocaleEntry { id = "a", text = "A" } }
            });
            Assert.AreEqual(1, Localization.LoadedCount);

            Localization.SetLocale("ja");
            Assert.AreEqual(0, Localization.LoadedCount,
                "이전 언어의 글자가 남으면 화면에 두 언어가 뒤엉킨다");
        }

        // ── 조사 ───────────────────────────────────────────────

        [Test]
        public void KoreanParticles_AreAppliedOnlyInKorean()
        {
            Localization.SetLocale(Localization.Korean);
            Assert.AreEqual("클라라를", Localization.WithParticle("클라라", ParticleKind.EulReul));
            Assert.AreEqual("헬렌을", Localization.WithParticle("헬렌", ParticleKind.EulReul));

            Localization.SetLocale(Localization.English);
            Assert.AreEqual("Clara", Localization.WithParticle("Clara", ParticleKind.EulReul),
                "영어에 조사를 붙이면 'Claraeul'이 된다");
        }

        // ── 실제 데이터의 번역 진행률 ──────────────────────────

        [Test]
        public void UiStrings_AreFullyTranslated()
        {
            // UI는 항목이 적으므로 100%를 요구한다. 여기가 비면 영어판이 즉시 티가 난다.
            LocaleFile en = LoadLocale(UiEnPath);
            Assert.IsNotNull(en, "ui.en.json을 읽지 못했다");
            Assert.Greater(en.entries.Length, 0);

            var required = new Dictionary<string, string>
            {
                { "event.door", "어디선가 문이 여닫힌다" },
                { "event.footsteps", "발소리가 지나간다" },
                { "event.object", "무언가 놓이는 소리" },
                { "event.break", "무언가 깨지는 소리" },
                { "event.struggle", "무언가 부딪히는 소리" },
                { "event.house", "저택이 내는 소리" },
                { "event.other", "무슨 소리가 난다" },
                { "voice.unknown", "목소리" },
                { "voice.numbered", "목소리 {0}" },
            };

            LocaleCoverageReport report = LocaleCoverage.Check(required, en);
            Assert.IsEmpty(report.Missing, "UI 번역이 빠졌다: " + string.Join(", ", report.Missing.ToArray()));
            Assert.IsEmpty(report.Untranslated,
                "한국어를 그대로 복사해 둔 자리가 있다: " + string.Join(", ", report.Untranslated.ToArray()));
        }

        [Test]
        public void RoomNames_AreFullyTranslated()
        {
            // 방 이름은 여섯 개뿐이고, 하나라도 한국어로 남으면 영어판에서 바로 눈에 띈다.
            RoomTable rooms = FromJson<RoomTable>(ReadProjectFile(RoomsPath));
            Dictionary<string, string> required = LocaleCoverage.RequiredFromRooms(rooms);
            LocaleCoverageReport report = LocaleCoverage.Check(required, LoadLocale(ContentEnPath));

            Assert.AreEqual(6, required.Count);
            Assert.IsEmpty(report.Missing, "방 이름 번역이 빠졌다: " + string.Join(", ", report.Missing.ToArray()));
        }

        [Test]
        public void CaseFrame_IsFullyTranslated()
        {
            // 제목·도입·해결·실패·선택지. 플레이어가 가장 먼저·가장 마지막에 읽는 글이다.
            CaseDefinition definition = FromJson<CaseDefinition>(ReadProjectFile(CasePath));
            Dictionary<string, string> required = LocaleCoverage.RequiredFromCase(definition);
            LocaleCoverageReport report = LocaleCoverage.Check(required, LoadLocale(ContentEnPath));

            Assert.IsEmpty(report.Missing, "사건 문구 번역이 빠졌다: " + string.Join(", ", report.Missing.ToArray()));
            Assert.IsEmpty(report.Untranslated, string.Join(", ", report.Untranslated.ToArray()));
        }

        [Test]
        public void ClueBearingLines_AreTranslated()
        {
            // 단서를 나르는 발화는 게임을 풀 수 있게 하는 최소 집합이다.
            // 이것이 한국어로 남으면 영어판은 플레이 불가다 — 다른 무엇보다 먼저 요구한다.
            ScriptDefinition script = FromJson<ScriptDefinition>(ReadProjectFile(ScriptPath)).Normalized();
            LocaleFile en = LoadLocale(ScriptEnPath);

            var clueLines = new Dictionary<string, string>();
            var byId = new Dictionary<string, Utterance>();
            for (int i = 0; i < script.utterances.Length; i++) byId[script.utterances[i].id] = script.utterances[i];

            for (int f = 0; f < script.facts.Length; f++)
            {
                ScriptFact fact = script.facts[f];
                for (int u = 0; u < fact.utteranceIds.Length; u++)
                {
                    Utterance utterance;
                    if (byId.TryGetValue(fact.utteranceIds[u], out utterance))
                        clueLines[utterance.id] = utterance.text;
                }
            }

            Assert.Greater(clueLines.Count, 0, "단서를 나르는 발화를 찾지 못했다");
            LocaleCoverageReport report = LocaleCoverage.Check(clueLines, en);
            Assert.IsEmpty(report.Missing,
                "단서 발화 번역이 빠졌다 — 영어판이 풀리지 않는다: " + string.Join(", ", report.Missing.ToArray()));
            Assert.IsEmpty(report.Untranslated, string.Join(", ", report.Untranslated.ToArray()));
        }

        [Test]
        public void ScriptTranslation_ProgressIsMeasured_NotAssumed()
        {
            // 대본 전체 진행률. 아직 낮은 것이 정상이다 — 이 테스트는 낮음을 실패로 보지 않고
            // **숫자가 나온다는 것**을 못박는다. 출시 판정은 이 숫자로 한다.
            ScriptDefinition script = FromJson<ScriptDefinition>(ReadProjectFile(ScriptPath)).Normalized();
            Dictionary<string, string> required = LocaleCoverage.RequiredFromScript(script);
            LocaleCoverageReport report = LocaleCoverage.Check(required, LoadLocale(ScriptEnPath));

            Assert.Greater(report.RequiredCount, 250, "대본 항목 수가 갑자기 줄었다면 무언가 빠진 것이다");
            TestContext.WriteLine("대본 번역 진행률: " + report.TranslatedCount + "/" + report.RequiredCount
                                  + " (" + (int)(report.Ratio * 100) + "%)");

            // 번역 파일에 대본에 없는 id가 있으면 대본이 바뀐 것이다 — 조용히 낡는 자리다.
            Assert.IsEmpty(report.Orphaned,
                "번역 파일에 대본에 없는 id가 있다(대본이 바뀌었다): " + string.Join(", ", report.Orphaned.ToArray()));
        }

        // ── 도우미 ─────────────────────────────────────────────

        private static LocaleFile LoadLocale(string relativePath)
        {
            string json = ReadProjectFile(relativePath);
            LocaleFile file = FromJson<LocaleFile>(json);
            return file != null ? file.Normalized() : null;
        }

        private static string ReadProjectFile(string relativePath)
        {
            string[] starts = { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory };
            for (int s = 0; s < starts.Length; s++)
            {
                for (DirectoryInfo dir = new DirectoryInfo(starts[s]); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, relativePath);
                    if (File.Exists(candidate)) return File.ReadAllText(candidate);
                }
            }
            Assert.Fail(relativePath + "을(를) 찾지 못했다");
            return null;
        }

        private static T FromJson<T>(string json)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<T>(json);
#else
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
#endif
        }
    }
}
