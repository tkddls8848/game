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
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[GameDataLoader] rooms.json 내용이 비어 있다.");
                return new RoomTable().Normalized();
            }

            RoomTable table = null;
            try
            {
                table = JsonUtility.FromJson<RoomTable>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[GameDataLoader] rooms.json 파싱 실패: " + e.Message);
            }

            if (table == null) return new RoomTable().Normalized();
            return table.Normalized();
        }
    }
}
