using Detective.Data;
using UnityEngine;

namespace Detective.Core
{
    /// <summary>
    /// 씬에 하나 존재하는 부팅 지점. 게임 데이터를 읽어 RoomLayout을 만들어 두고,
    /// 다른 컴포넌트가 방 정보를 물어볼 수 있게 한다.
    /// 매니저끼리의 직접 참조는 여기까지만 허용하고, 그 외 통신은 GameEvents를 쓴다(§18-5).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>rooms.json에서 만들어진 맵 레이아웃. Awake 이후에 유효하다.</summary>
        public RoomLayout Layout { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            RoomTable table = GameDataLoader.LoadRoomTable();

            // 데이터가 깨진 채로 조용히 굴러가지 않도록 런타임에서도 한 번 검사한다.
            var errors = RoomLayoutValidator.Validate(table);
            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError("[rooms.json] " + errors[i]);
            }

            Layout = RoomLayout.FromTable(table);
            Debug.Log("[GameManager] 방 " + Layout.RoomCount + "개 로드 완료.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
