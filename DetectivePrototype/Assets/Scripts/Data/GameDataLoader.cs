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
            TextAsset[] assets = Resources.LoadAll<TextAsset>(NpcsResourceFolder);
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

        /// <summary>사건 데이터 전체를 Resources에서 읽어 하나로 묶는다.</summary>
        public static CaseDatabase LoadDatabase()
        {
            RoomTable rooms = LoadRoomTable();
            return new CaseDatabase(RoomLayout.FromTable(rooms), new NpcRoster(LoadNpcs()));
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
