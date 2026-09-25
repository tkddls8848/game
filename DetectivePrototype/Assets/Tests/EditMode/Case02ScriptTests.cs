using System.Collections.Generic;
using System.IO;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using Detective.NPC;
using NUnit.Framework;

namespace Detective.Tests
{
    /// <summary>
    /// 본편 대본(case_02)을 검사기에 물린다.
    ///
    /// 대본은 코드가 아니라 데이터라서 컴파일이 지켜 주지 않는다. 발화를 하나 옮기거나
    /// 한 줄 고치다가 "한자리에서 다 들리는" 상태가 되면 이 게임의 전제가 조용히 무너진다.
    /// 그래서 대본을 고칠 때마다 DEVELOPMENT_PLAN_UNHEARD.md §5의 검사가 돌게 못박는다.
    ///
    /// 실제 파일을 읽는다. Unity 밖(헤드리스)에서는 Newtonsoft.Json으로 대신 읽는다.
    /// </summary>
    public class Case02ScriptTests
    {
        private const string RoomsPath = "Assets/Resources/GameData/rooms.json";
        private const string ScriptPath = "Assets/Resources/GameData/cases/case_02/script.json";
        private const string NpcsFolder = "Assets/Resources/GameData/npcs";

        private ScriptDefinition _script;
        private RoomLayout _layout;
        private NpcRoster _roster;

        [SetUp]
        public void SetUp()
        {
            _layout = RoomLayout.FromTable(FromJson<RoomTable>(ReadProjectFile(RoomsPath)));
            _script = FromJson<ScriptDefinition>(ReadProjectFile(ScriptPath)).Normalized();

            var npcs = new List<NpcDefinition>();
            foreach (string path in SortedNpcFiles())
                npcs.Add(FromJson<NpcDefinition>(File.ReadAllText(path)).Normalized());
            _roster = new NpcRoster(npcs);
        }

        // ── 참조 무결성 ─────────────────────────────────────────

        [Test]
        public void Script_PassesValidator()
        {
            List<string> errors = ScriptValidator.Validate(_script, _layout, _roster);
            Assert.IsEmpty(errors, string.Join(" / ", errors.ToArray()));
        }

        [Test]
        public void Script_HasReleaseScaleAndUsesEveryRoom()
        {
            Assert.That(_script.utterances.Length, Is.InRange(150, 300),
                "본편 대본은 150~300 발화다. 슬라이스 크기로 줄면 되돌려 들을 거리가 사라진다");
            Assert.AreEqual(5, _script.speakers.Length, "목소리 5개");

            var rooms = new HashSet<string>();
            var voices = new HashSet<string>();
            for (int i = 0; i < _script.utterances.Length; i++)
            {
                Utterance u = _script.utterances[i];
                rooms.Add(u.room);
                voices.Add(u.voiceId);
            }
            Assert.AreEqual(_layout.Rooms.Count, rooms.Count,
                "방 하나라도 조용하면 그 방에 갈 이유가 없어진다");
            Assert.AreEqual(5, voices.Count, "선언한 목소리가 전부 실제로 말해야 한다");
        }

        [Test]
        public void EveryVoiceSpeaksEnoughToBeIdentified()
        {
            // 익명 해제는 플레이어가 목소리를 알아듣는 데서 시작한다.
            // 한두 마디만 하는 목소리는 이름을 붙일 근거가 모자란다.
            var count = new Dictionary<string, int>();
            for (int i = 0; i < _script.utterances.Length; i++)
            {
                string v = _script.utterances[i].voiceId;
                count[v] = count.ContainsKey(v) ? count[v] + 1 : 1;
            }
            foreach (KeyValuePair<string, int> pair in count)
                Assert.Greater(pair.Value, 20, pair.Key + " 의 발화가 너무 적다");
        }

        // ── §5 추리 가능성 ──────────────────────────────────────

        [Test]
        public void Script_IsSolvable_ButNotFromOneSpotAndNotInOnePass()
        {
            SliceSolvabilityReport report = SliceSolvability.Check(_script, _layout);
            Assert.IsEmpty(report.Problems, string.Join(" / ", report.Problems.ToArray()));

            Assert.IsTrue(report.EveryFactAudibleSomewhere,
                "필요한 사실 중 어디서도 온전히 들리지 않는 것이 있다 — 풀 수 없는 사건이다");
            Assert.IsTrue(report.NoSingleRoomSuffices,
                "한 방에 서 있는 것만으로 다 들린다 — 돌아다닐 이유가 없어진다");
            Assert.IsEmpty(report.RoomsHearingEverything, "전부 들리는 방이 있으면 안 된다");
            Assert.IsFalse(report.SinglePassPossible,
                "한 회차로 전부 모을 수 있다 — 되돌려 듣는 것이 이 게임의 전부인데 그 이유가 사라진다");
            Assert.IsTrue(report.IsSolvable);
        }

        [Test]
        public void StayingInAnyOneRoom_LeavesUtterancesUnheard()
        {
            var timeline = new ScriptTimeline(_script);
            var model = new AudibilityModel(_layout);

            for (int i = 0; i < _layout.Rooms.Count; i++)
            {
                string room = _layout.Rooms[i].id;
                HashSet<string> heard = SliceSolvability.HeardUtteranceIds(timeline, model, ListeningPlan.Fixed(room));
                Assert.Less(heard.Count, _script.utterances.Length,
                    room + " 에 서 있으면 전부 들린다");
            }
        }

        [Test]
        public void ClipSlotsAreEmpty_UntilVoiceIsRecorded()
        {
            // 음성은 아직 없다(DEVELOPMENT_PLAN_RELEASE.md §1.1). 빈 자리인 것을 확인해,
            // 나중에 채울 때 무엇이 남았는지 이 테스트가 알려 준다.
            for (int i = 0; i < _script.utterances.Length; i++)
                Assert.AreEqual(string.Empty, _script.utterances[i].clip, _script.utterances[i].id);
        }

        // ── 도우미 ──────────────────────────────────────────────

        private static string[] SortedNpcFiles()
        {
            string folder = ResolveProjectPath(NpcsFolder);
            string[] files = Directory.GetFiles(folder, "*.json");
            System.Array.Sort(files, System.StringComparer.Ordinal);
            return files;
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(ResolveProjectPath(relativePath));
        }

        private static string ResolveProjectPath(string relativePath)
        {
            string[] starts = { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory };
            for (int s = 0; s < starts.Length; s++)
            {
                for (DirectoryInfo dir = new DirectoryInfo(starts[s]); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, relativePath);
                    if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
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
