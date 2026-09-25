using Detective.Core;
using Detective.Data;
using UnityEngine;

namespace Detective.Player
{
    /// <summary>
    /// 플레이어가 지금 어느 방에 있는지 지켜보다가 바뀌면 알린다 — 이것이 엿듣기의 "청취점"이다.
    ///
    /// 방 환경음(AudioDirector)과 가청 판정(EavesdropController)이 둘 다 이 값을 필요로 한다.
    /// 각자 플레이어 위치를 따로 계산하면 같은 프레임에 서로 다른 방을 볼 수 있으므로,
    /// 한 곳에서만 판정하고 이벤트로 흘린다(§18-5).
    ///
    /// 방과 방 사이(벽 두께 안)에서는 어느 방에도 속하지 않는다. 그때는 빈 문자열이다 —
    /// 문턱에 선 채로 양쪽을 다 듣는 일이 없어야 한다.
    /// </summary>
    public class PlayerRoomTracker : MonoBehaviour
    {
        /// <summary>지금 서 있는 방. 어디에도 속하지 않으면 빈 문자열.</summary>
        public string CurrentRoom { get; private set; }

        private RoomLayout _layout;
        private bool _announcedOnce;

        private void Start()
        {
            CurrentRoom = string.Empty;
            Refresh(true);
        }

        private void Update()
        {
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            if (_layout == null)
            {
                GameManager manager = GameManager.Instance;
                _layout = manager != null ? manager.Layout : null;
                if (_layout == null) return; // 아직 데이터가 안 올라왔다. 다음 프레임에 다시 본다.
            }

            Vector3 p = transform.position;
            RoomDefinition room = _layout.FindRoomAt(p.x, p.y);
            string id = room != null ? room.id : string.Empty;

            if (!force && _announcedOnce && id == CurrentRoom) return;

            CurrentRoom = id;
            _announcedOnce = true;
            GameEvents.RaisePlayerRoomChanged(id);
        }
    }
}
