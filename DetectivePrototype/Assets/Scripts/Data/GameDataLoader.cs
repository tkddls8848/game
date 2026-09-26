using System.Collections.Generic;
using Detective.Core;
using Detective.NPC;
using UnityEngine;

namespace Detective.Data
{
    /// <summary>
    /// JSON 게임 데이터 로딩 단일 진입점.
    /// UnityEngine 의존은 여기서 끝나고, 이후 로직(RoomLayout 등)은 순수 C#으로 넘어간다.
    /// </summary>
    public static class GameDataLoader
    {
        public const string RoomsResourcePath = "GameData/rooms";
        public const string NpcsResourceFolder = "GameData/npcs";
        public const string EvidenceResourcePath = "GameData/evidence/evidence";
        public const string DialogueResourceFolder = "GameData/dialogue";
        public const string CasesResourceFolder = "GameData/cases/";
        public const string DefaultCaseId = "case_01";

        /// <summary>
        /// rooms.json을 읽어 온다. 파일이 없거나 깨져 있으면 빈 테이블을 돌려주고 에러 로그를 남긴다
        /// (예외를 던지면 씬 전체가 죽어서 원인 파악이 오히려 어려워진다).
        /// </summary>
        public static RoomTable LoadRoomTable()
        {
            var asset = Resources.Load<TextAsset>(RoomsResourcePath);
            if (asset == null)
            {
                Debug.LogError("[GameDataLoader] Resources/" + RoomsResourcePath + ".json 을 찾지 못했다.");
                return new RoomTable().Normalized();
            }

            return ParseRoomTable(asset.text);
        }

        /// <summary>텍스트에서 직접 파싱. 에디터 스크립트가 파일을 읽어 넘길 때도 쓴다.</summary>
        public static RoomTable ParseRoomTable(string json)
        {
            RoomTable table = Parse<RoomTable>(json, "rooms.json");
            if (table == null) return new RoomTable().Normalized();
            return table.Normalized();
        }

        /// <summary>npcs/ 폴더의 모든 JSON. 한 파일 = 한 사람.</summary>
        public static List<NpcDefinition> LoadNpcs()
        {
            var result = new List<NpcDefinition>();
            TextAsset[] assets = SortedByName(Resources.LoadAll<TextAsset>(NpcsResourceFolder));
            for (int i = 0; i < assets.Length; i++)
            {
                NpcDefinition npc = ParseNpc(assets[i].text, assets[i].name);
                if (npc != null) result.Add(npc);
            }
            if (result.Count == 0) Debug.LogError("[GameDataLoader] Resources/" + NpcsResourceFolder + " 에서 인물을 하나도 읽지 못했다.");
            return result;
        }

        public static NpcDefinition ParseNpc(string json, string label)
        {
            NpcDefinition npc = Parse<NpcDefinition>(json, label);
            return npc != null ? npc.Normalized() : null;
        }

        public static EvidenceTable LoadEvidenceTable()
        {
            var asset = Resources.Load<TextAsset>(EvidenceResourcePath);
            if (asset == null)
            {
                Debug.LogError("[GameDataLoader] Resources/" + EvidenceResourcePath + ".json 을 찾지 못했다.");
                return new EvidenceTable().Normalized();
            }
            return ParseEvidenceTable(asset.text);
        }

        public static EvidenceTable ParseEvidenceTable(string json)
        {
            EvidenceTable table = Parse<EvidenceTable>(json, "evidence.json");
            return (table ?? new EvidenceTable()).Normalized();
        }

        /// <summary>dialogue/ 폴더의 모든 JSON. 한 파일 = 한 사람의 대사.</summary>
        public static List<DialogueFile> LoadDialogues()
        {
            var result = new List<DialogueFile>();
            TextAsset[] assets = SortedByName(Resources.LoadAll<TextAsset>(DialogueResourceFolder));
            for (int i = 0; i < assets.Length; i++)
            {
                DialogueFile file = ParseDialogue(assets[i].text, assets[i].name);
                if (file != null) result.Add(file);
            }
            return result;
        }

        public static DialogueFile ParseDialogue(string json, string label)
        {
            DialogueFile file = Parse<DialogueFile>(json, label);
            return file != null ? file.Normalized() : null;
        }

        /// <summary>cases/{caseId}.json. 없으면 빈 사건(검증기가 잡아낸다).</summary>
        public static CaseDefinition LoadCase(string caseId)
        {
            var asset = Resources.Load<TextAsset>(CasesResourceFolder + caseId);
            if (asset == null)
            {
                Debug.LogError("[GameDataLoader] Resources/" + CasesResourceFolder + caseId + ".json 을 찾지 못했다.");
                return new CaseDefinition().Normalized();
            }
            return ParseCase(asset.text, caseId);
        }

        public static CaseDefinition ParseCase(string json, string label)
        {
            CaseDefinition definition = Parse<CaseDefinition>(json, label);
            return (definition ?? new CaseDefinition()).Normalized();
        }

        /// <summary>사건 데이터 전체를 Resources에서 읽어 하나로 묶는다.</summary>
        public static CaseDatabase LoadDatabase()
        {
            return BuildDatabase(LoadRoomTable(), DefaultCaseId);
        }

        /// <summary>이미 읽은 rooms.json에 나머지 데이터를 Resources에서 읽어 붙인다.</summary>
        public static CaseDatabase BuildDatabase(RoomTable rooms, string caseId)
        {
            return new CaseDatabase(RoomLayout.FromTable(rooms), new NpcRoster(LoadNpcs()), LoadEvidenceTable(), LoadDialogues(),
                LoadCase(caseId));
        }

        /// <summary>Resources.LoadAll은 순서를 보장하지 않는다. 에디터 검증(파일 이름 순)과 같은 순서로 맞춘다.</summary>
        private static TextAsset[] SortedByName(TextAsset[] assets)
        {
            System.Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));
            return assets;
        }

        /// <summary>
        /// 인물 이동 트랙(<c>cases/&lt;id&gt;/tracks.json</c>). 없으면 null —
        /// 대본만으로도 회차는 성립한다(사람이 안 보이고 이동 소리가 없을 뿐이다).
        /// </summary>
        public static Eavesdrop.MovementTrackTable LoadTracks(string caseId)
        {
            if (string.IsNullOrEmpty(caseId)) return null;
            var asset = Resources.Load<TextAsset>(CasesResourceFolder + caseId + "/tracks");
            if (asset == null) return null;

            Eavesdrop.MovementTrackTable table =
                Parse<Eavesdrop.MovementTrackTable>(asset.text, caseId + "/tracks.json");
            return table != null ? table.Normalized() : null;
        }

        /// <summary>
        /// 손으로 적은 이벤트(<c>cases/&lt;id&gt;/events.json</c>). 극적인 소리만 여기 적는다 —
        /// 문소리·발소리는 <c>MovementEvents</c>가 트랙에서 뽑으므로 적지 않는다.
        /// </summary>
        public static Eavesdrop.EventTable LoadEvents(string caseId)
        {
            if (string.IsNullOrEmpty(caseId)) return null;
            var asset = Resources.Load<TextAsset>(CasesResourceFolder + caseId + "/events");
            if (asset == null) return null;

            Eavesdrop.EventTable table = Parse<Eavesdrop.EventTable>(asset.text, caseId + "/events.json");
            return table != null ? table.Normalized() : null;
        }

        /// <summary>언어 파일이 놓이는 Resources 폴더.</summary>
        public const string LocaleResourceFolder = "GameData/locale/";

        /// <summary>
        /// 이 언어의 문자열 표를 전부 얹는다. UI → 콘텐츠 → 대본 순으로 겹쳐 쌓는다.
        ///
        /// 한국어(원문 언어)면 아무것도 읽지 않는다 — 원문이 코드와 데이터에 이미 있고,
        /// 원문 언어에서는 표를 보지 않는다(Localization.Text 참고).
        /// 파일이 없어도 조용히 넘어간다: 번역이 없는 항목은 한국어로 남고 화면은 비지 않는다.
        /// </summary>
        public static int ApplyLocale(string locale, string caseId)
        {
            Localization.SetLocale(locale);
            if (Localization.IsSourceLocale) return 0;

            int loaded = 0;
            loaded += LoadLocaleFile("ui." + locale);
            loaded += LoadLocaleFile("content." + locale);
            if (!string.IsNullOrEmpty(caseId)) loaded += LoadLocaleFile(caseId + ".script." + locale);

            Debug.Log("[GameDataLoader] 언어 '" + locale + "' 문자열 " + Localization.LoadedCount + "개 로드.");
            return loaded;
        }

        /// <summary>파일 하나. 없으면 0을 돌려주고 경고하지 않는다(번역 진행 중이 정상 상태다).</summary>
        private static int LoadLocaleFile(string name)
        {
            var asset = Resources.Load<TextAsset>(LocaleResourceFolder + name);
            if (asset == null) return 0;

            LocaleFile file = Parse<LocaleFile>(asset.text, name + ".json");
            if (file == null) return 0;

            Localization.Load(file);
            return file.Normalized().entries.Length;
        }

        /// <summary>JsonUtility 파싱 + 실패 로그. 실패하면 null.</summary>
        public static T Parse<T>(string json, string label) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[GameDataLoader] " + label + " 내용이 비어 있다.");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[GameDataLoader] " + label + " 파싱 실패: " + e.Message);
                return null;
            }
        }
    }
}
