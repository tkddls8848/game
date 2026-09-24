using System.Collections.Generic;
using System.IO;
using Detective.Core;
using Detective.Data;
using Detective.NPC;
using UnityEditor;
using UnityEngine;

namespace DetectiveEditor
{
    /// <summary>
    /// JSON 게임 데이터 무결성 검사. 씬을 만들기 전과 사람이 메뉴를 눌렀을 때 돌린다.
    /// 검사 규칙 자체는 순수 C#(RoomLayoutValidator)에 있고 여기서는 파일 읽기와 로그만 담당한다.
    /// </summary>
    public static class DataValidator
    {
        public const string RoomsJsonPath = "Assets/Resources/GameData/rooms.json";
        public const string NpcsFolder = "Assets/Resources/GameData/npcs";
        public const string EvidenceJsonPath = "Assets/Resources/GameData/evidence/evidence.json";

        [MenuItem("Tools/Detective/Validate Game Data")]
        public static void ValidateFromMenu()
        {
            List<string> errors = ValidateAll();
            if (errors.Count == 0)
            {
                Debug.Log("[DataValidator] 게임 데이터 무결성 검사 통과.");
                return;
            }

            for (int i = 0; i < errors.Count; i++) Debug.LogError("[DataValidator] " + errors[i]);
        }

        /// <summary>문제 목록. 비어 있으면 통과.</summary>
        public static List<string> ValidateAll()
        {
            var errors = new List<string>();
            LoadDatabase(errors);
            return errors;
        }

        /// <summary>
        /// 디스크의 JSON을 전부 읽어 CaseDatabase로 묶고, 파싱·무결성 문제를 errors에 쌓는다.
        /// rooms.json을 못 읽으면 null. 검사 규칙은 순수 C#(RoomLayoutValidator, GameDataValidator)에 있다.
        /// </summary>
        public static CaseDatabase LoadDatabase(List<string> errors)
        {
            RoomTable rooms = LoadRoomTable(errors);
            if (rooms == null) return null;
            errors.AddRange(RoomLayoutValidator.Validate(rooms));

            List<NpcDefinition> npcs = LoadNpcs(errors);
            EvidenceTable evidence = LoadEvidenceTable(errors);
            var database = new CaseDatabase(RoomLayout.FromTable(rooms), new NpcRoster(npcs), evidence);
            errors.AddRange(GameDataValidator.Validate(database));
            return database;
        }

        /// <summary>npcs/ 폴더의 JSON을 디스크에서 직접 읽는다(파일 이름 순).</summary>
        public static List<NpcDefinition> LoadNpcs(List<string> errors)
        {
            var result = new List<NpcDefinition>();
            foreach (string path in ReadJsonFiles(NpcsFolder, errors))
            {
                NpcDefinition npc = GameDataLoader.ParseNpc(File.ReadAllText(path), path);
                if (npc == null) errors.Add(path + " 를 파싱하지 못했다.");
                else result.Add(npc);
            }
            return result;
        }

        public static EvidenceTable LoadEvidenceTable(List<string> errors)
        {
            if (!File.Exists(EvidenceJsonPath))
            {
                errors.Add(EvidenceJsonPath + " 파일이 없다.");
                return new EvidenceTable().Normalized();
            }
            return GameDataLoader.ParseEvidenceTable(File.ReadAllText(EvidenceJsonPath));
        }

        private static List<string> ReadJsonFiles(string folder, List<string> errors)
        {
            var files = new List<string>();
            if (!Directory.Exists(folder))
            {
                errors.Add(folder + " 폴더가 없다.");
                return files;
            }
            files.AddRange(Directory.GetFiles(folder, "*.json"));
            files.Sort(System.StringComparer.Ordinal);
            if (files.Count == 0) errors.Add(folder + " 에 JSON 파일이 없다.");
            return files;
        }

        /// <summary>
        /// rooms.json을 디스크에서 직접 읽는다.
        /// batchmode에서 Resources.Load는 임포트 타이밍을 타므로 파일 읽기가 더 안전하다.
        /// </summary>
        public static RoomTable LoadRoomTable(List<string> errors)
        {
            if (!File.Exists(RoomsJsonPath))
            {
                errors.Add(RoomsJsonPath + " 파일이 없다.");
                return null;
            }

            string json = File.ReadAllText(RoomsJsonPath);
            RoomTable table = GameDataLoader.ParseRoomTable(json);
            if (table.rooms.Length == 0) errors.Add(RoomsJsonPath + " 를 파싱했지만 방이 하나도 없다.");
            return table;
        }
    }
}
