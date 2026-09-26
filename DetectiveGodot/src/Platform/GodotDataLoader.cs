using System.Collections.Generic;
using Detective.Data;
using Detective.Eavesdrop;
using Detective.NPC;
using Godot;
using FileAccess = Godot.FileAccess;

namespace DetectiveGodot.Platform
{
    /// <summary>
    /// JSON 게임 데이터 로딩 단일 진입점 — Unity의 <c>GameDataLoader</c>와 같은 역할.
    /// Godot 의존은 여기서 끝나고, 이후 로직(<c>RoomLayout</c> 등)은 공유 순수 C#으로 넘어간다.
    ///
    /// 바뀐 것은 두 줄뿐이다: <c>Resources.Load&lt;TextAsset&gt;</c> → <c>FileAccess</c>,
    /// <c>JsonUtility</c> → <see cref="Json"/>. 파싱 결과 타입은 Unity 판과 **같은 클래스**다.
    ///
    /// 데이터는 <c>res://data/</c>에 있다(Godot은 프로젝트 밖을 읽지 못한다).
    /// 원본은 Unity 프로젝트 하나이고 이쪽은 <c>tools/sync_godot.py</c>가 맞춘 사본이다.
    /// </summary>
    public static class GodotDataLoader
    {
        public const string Root = "res://data";
        public const string DefaultCaseId = "case_01";
        public const string EavesdropCaseId = "case_02";

        /// <summary>없으면 null. 예외를 던지지 않는다 — 씬 전체가 죽으면 원인 파악이 더 어렵다.</summary>
        private static string ReadText(string relativePath)
        {
            string path = Root + "/" + relativePath;
            if (!FileAccess.FileExists(path))
            {
                GD.PushError("[GodotDataLoader] " + path + " 을 찾지 못했다. `python tools/sync_godot.py`를 돌렸는가?");
                return null;
            }
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushError("[GodotDataLoader] " + path + " 를 열 수 없다: " + FileAccess.GetOpenError());
                return null;
            }
            return file.GetAsText();
        }

        private static T Parse<T>(string json, string label) where T : class
        {
            if (json == null) return null;
            T value = Json.Parse<T>(json, label, out string error);
            if (error != null) GD.PushError("[GodotDataLoader] " + error);
            return value;
        }

        // ── 맵 ────────────────────────────────────────────────

        public static RoomTable LoadRoomTable()
        {
            RoomTable table = Parse<RoomTable>(ReadText("rooms.json"), "rooms.json");
            return (table ?? new RoomTable()).Normalized();
        }

        // ── 인물 ──────────────────────────────────────────────

        /// <summary>npcs/ 폴더의 모든 JSON. 한 파일 = 한 사람. 이름 순으로 읽어 순서를 고정한다.</summary>
        public static List<NpcDefinition> LoadNpcs()
        {
            var result = new List<NpcDefinition>();
            foreach (string name in ListJson("npcs"))
            {
                NpcDefinition npc = Parse<NpcDefinition>(ReadText("npcs/" + name), name);
                if (npc != null) result.Add(npc.Normalized());
            }
            if (result.Count == 0) GD.PushError("[GodotDataLoader] npcs/ 에서 인물을 하나도 읽지 못했다.");
            return result;
        }

        /// <summary>dialogue/ 폴더의 모든 JSON. 한 파일 = 한 사람의 대사.</summary>
        public static List<DialogueFile> LoadDialogues()
        {
            var result = new List<DialogueFile>();
            foreach (string name in ListJson("dialogue"))
            {
                DialogueFile file = Parse<DialogueFile>(ReadText("dialogue/" + name), name);
                if (file != null) result.Add(file.Normalized());
            }
            return result;
        }

        public static EvidenceTable LoadEvidenceTable()
        {
            EvidenceTable table = Parse<EvidenceTable>(ReadText("evidence/evidence.json"), "evidence.json");
            return (table ?? new EvidenceTable()).Normalized();
        }

        // ── 사건 ──────────────────────────────────────────────

        public static CaseDefinition LoadCase(string caseId)
        {
            CaseDefinition def = Parse<CaseDefinition>(ReadText("cases/" + caseId + ".json"), caseId + ".json");
            return def != null ? def.Normalized() : null;
        }

        /// <summary>엿듣기 대본. <c>cases/&lt;id&gt;/script.json</c>.</summary>
        public static ScriptDefinition LoadScript(string caseId)
        {
            string rel = "cases/" + caseId + "/script.json";
            ScriptDefinition script = Parse<ScriptDefinition>(ReadText(rel), rel);
            return script != null ? script.Normalized() : null;
        }

        /// <summary>인물 이동 트랙. 없으면 null — 대본만으로도 게임은 성립한다.</summary>
        public static MovementTrackTable LoadTracks(string caseId)
        {
            string rel = "cases/" + caseId + "/tracks.json";
            if (!FileAccess.FileExists(Root + "/" + rel)) return null;
            MovementTrackTable table = Parse<MovementTrackTable>(ReadText(rel), rel);
            return table;
        }

        // ── 연출 ──────────────────────────────────────────────

        public static ArtManifest LoadArtManifest()
        {
            ArtManifest manifest = Parse<ArtManifest>(ReadText("art.json"), "art.json");
            return manifest;
        }

        // ── 도우미 ────────────────────────────────────────────

        private static List<string> ListJson(string folder)
        {
            var names = new List<string>();
            using DirAccess dir = DirAccess.Open(Root + "/" + folder);
            if (dir == null)
            {
                GD.PushError("[GodotDataLoader] " + Root + "/" + folder + " 폴더를 열 수 없다.");
                return names;
            }
            foreach (string name in dir.GetFiles())
            {
                // 내보낸 빌드에서는 임포트된 파일이 .remap 으로 보일 수 있다.
                string clean = name.EndsWith(".remap") ? name.Substring(0, name.Length - 6) : name;
                if (clean.EndsWith(".json")) names.Add(clean);
            }
            names.Sort();
            return names;
        }
    }
}
