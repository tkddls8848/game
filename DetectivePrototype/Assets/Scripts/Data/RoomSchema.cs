using System;

namespace Detective.Data
{
    /// <summary>
    /// rooms.json의 방 하나. 좌표는 월드 유닛, (x, y)는 좌하단 모서리다.
    /// JsonUtility가 다룰 수 있도록 public 필드만 사용한다(프로퍼티/딕셔너리 금지).
    /// </summary>
    [Serializable]
    public class RoomDefinition
    {
        public string id;
        public string displayName;
        public float x;
        public float y;
        public float width;
        public float height;

        /// <summary>바닥 색. "#RRGGBB" 형식. 비어 있으면 기본색을 쓴다.</summary>
        public string floorColor;

        public float MinX { get { return x; } }
        public float MinY { get { return y; } }
        public float MaxX { get { return x + width; } }
        public float MaxY { get { return y + height; } }
        public float CenterX { get { return x + width * 0.5f; } }
        public float CenterY { get { return y + height * 0.5f; } }

        public bool Contains(float px, float py)
        {
            return px >= MinX && px <= MaxX && py >= MinY && py <= MaxY;
        }
    }

    /// <summary>
    /// 두 방을 잇는 출입구. 벽 위에 뚫리는 구멍의 월드 사각형이다.
    /// 벽 두께보다 두꺼워야 벽을 관통해서 잘린다(RoomLayoutValidator가 검사한다).
    /// </summary>
    [Serializable]
    public class DoorDefinition
    {
        public string id;
        public string roomA;
        public string roomB;
        public float x;
        public float y;
        public float width;
        public float height;

        public float MinX { get { return x; } }
        public float MinY { get { return y; } }
        public float MaxX { get { return x + width; } }
        public float MaxY { get { return y + height; } }
        public float CenterX { get { return x + width * 0.5f; } }
        public float CenterY { get { return y + height * 0.5f; } }
    }

    /// <summary>rooms.json 최상위 객체.</summary>
    [Serializable]
    public class RoomTable
    {
        public RoomDefinition[] rooms;
        public DoorDefinition[] doors;

        /// <summary>플레이어 시작 방 id. 비어 있으면 첫 번째 방을 쓴다.</summary>
        public string playerSpawnRoom;

        /// <summary>JsonUtility는 누락된 배열을 null로 둔다. 이후 코드가 null 검사를 하지 않도록 정리한다.</summary>
        public RoomTable Normalized()
        {
            if (rooms == null) rooms = new RoomDefinition[0];
            if (doors == null) doors = new DoorDefinition[0];
            return this;
        }
    }
}
