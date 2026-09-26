using Detective.Data;
using Detective.Eavesdrop;
using UnityEngine;

namespace Detective.Art
{
    /// <summary>
    /// <see cref="SpatialVoiceDirector"/>를 회차에 붙여 준다.
    ///
    /// 왜 따로 있는가: 씬 빌더는 <c>SpeakerPositions</c>를 만들어 줄 수 없다 — 그것은 대본을
    /// 읽은 뒤에야 생기고, 대본은 실행 시 <see cref="EavesdropController"/>가 읽는다.
    /// 씬에는 "누구를 누구에게 붙일지"만 저장해 두고, 실제 연결은 첫 프레임에 한다.
    ///
    /// 이 프로젝트의 규칙(§18-2: 손으로만 만들 수 있는 에셋을 만들지 않는다)을 지키는 방식이다 —
    /// 씬 파일에는 직렬화 가능한 참조만 들어가고, 런타임 객체는 코드가 잇는다.
    /// </summary>
    public class SpatialAudioBinder : MonoBehaviour
    {
        public EavesdropController controller;
        public SpatialVoiceDirector spatial;
        public Transform listener;

        private bool _bound;

        private void Update()
        {
            if (_bound) return;
            if (controller == null || spatial == null || listener == null)
            {
                // 씬이 낡아 참조가 비어 있다. 같은 씬에서 찾아 본다.
                if (controller == null) controller = FindAnyObjectByType<EavesdropController>();
                if (spatial == null) spatial = FindAnyObjectByType<SpatialVoiceDirector>();
                if (listener == null)
                {
                    var tracker = FindAnyObjectByType<Player.PlayerRoomTracker>();
                    if (tracker != null) listener = tracker.transform;
                }
                if (controller == null || spatial == null || listener == null) return;
            }

            // 대본을 아직 못 읽었으면 기다린다. 실패했으면 더 기다리지 않는다.
            if (controller.Session == null)
            {
                if (!string.IsNullOrEmpty(controller.LoadError)) _bound = true;
                return;
            }

            ArtManifest art = ArtLibrary.Instance != null ? ArtLibrary.Instance.Manifest : null;
            spatial.Bind(controller, controller.Positions, art, listener);
            _bound = true;
        }
    }
}
