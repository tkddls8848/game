using System.Collections.Generic;
using System.IO;
using Detective.Core;
using Detective.Data;
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

            RoomTable rooms = LoadRoomTable(errors);
            if (rooms != null) errors.AddRange(RoomLayoutValidator.Validate(rooms));

            // Phase 3 이후 evidence/npc/dialogue 검사가 여기에 추가된다.
            return errors;
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
