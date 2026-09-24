using Detective.Core;
using Detective.Data;
using UnityEngine;

namespace Detective.Art
{
    /// <summary>바닥 위쪽에 방 이름을 옅게 적는다(평면도 느낌). 실행 시 GameManager의 레이아웃을 읽어 만든다.</summary>
    public class RoomLabels : MonoBehaviour
    {
        public Color color = new Color(1f, 0.94f, 0.82f, 0.38f);
        public float characterSize = 0.06f;

        [Tooltip("방 위쪽 벽에서 아래로 이만큼 내려온 자리에 적는다.")]
        public float insetFromTop = 1.1f;

        private void Start()
        {
            if (GameManager.Instance == null) return;
            RoomLayout layout = GameManager.Instance.Layout;

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                RoomDefinition room = layout.Rooms[i];
                Vector3 position = new Vector3(room.CenterX, room.MaxY - insetFromTop, 0f);
                // 바닥(-20)보다 위, 소품(0)보다 아래.
                WorldLabel.Create(transform, "RoomLabel_" + room.id, position, characterSize, color, -5).text = room.displayName;
            }
        }
    }
}
