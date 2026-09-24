using System.Collections.Generic;
using Detective.Art;
using Detective.Core;
using Detective.Investigation;
using UnityEngine;

namespace Detective.NPC
{
    /// <summary>
    /// 인물 하나의 겉모습과 이동. 스케줄 판단은 하지 않는다 — NpcDirector가 경로를 넘겨주면 따라갈 뿐이다.
    /// 몸통 스프라이트는 SceneBuilder가 자식("Body")으로 만들고, 이름표는 실행 시 코드로 붙인다(OS 한글 폰트가 직렬화되지 않기 때문).
    /// </summary>
    public class NPCController : MonoBehaviour, IInteractable, IHighlightable
    {
        public string npcId;
        public string displayName;
        public bool isVictim;

        [Tooltip("초당 이동 거리(월드 유닛).")]
        public float moveSpeed = 5f;

        public string CurrentRoomId { get; set; }
        public bool IsMoving { get { return _route.Count > 0; } }
        public bool IsVisible { get; private set; }

        private readonly List<Vector3> _route = new List<Vector3>();
        private float _speedMultiplier = 1f;

        private SpriteRenderer _body;
        private Collider2D _collider;
        private TextMesh _nameLabel;
        private TextMesh _captionLabel;
        private Vector3 _bodyScale = Vector3.one;
        private bool _highlighted;

        private void Awake()
        {
            _body = GetComponentInChildren<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
            if (_body != null) _bodyScale = _body.transform.localScale;

            _nameLabel = WorldLabel.Create(transform, "NameLabel", new Vector3(0f, 0.85f, 0f), 0.045f, new Color(0.94f, 0.90f, 0.80f), 60);
            _captionLabel = WorldLabel.Create(transform, "CaptionLabel", new Vector3(0f, -0.8f, 0f), 0.032f, new Color(0.85f, 0.70f, 0.40f), 60);
            // 지도 위에는 성을 뺀 짧은 이름만 적어 이름표끼리 겹치지 않게 한다. 전체 이름은 안내문·대화창에 나온다.
            _nameLabel.text = TimelineBoard.ShortName(displayName);
            _captionLabel.text = string.Empty;
            IsVisible = true;
        }

        private void Update()
        {
            if (_route.Count > 0)
            {
                Vector3 target = _route[0];
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * _speedMultiplier * Time.deltaTime);
                if ((transform.position - target).sqrMagnitude < 0.0004f) _route.RemoveAt(0);
            }

            if (_body != null)
            {
                float pulse = _highlighted ? 1.12f + Mathf.Sin(Time.unscaledTime * 8f) * 0.04f : 1f;
                _body.transform.localScale = _bodyScale * pulse;
            }
        }

        // ----- 이동 ------------------------------------------------------------

        public void FollowRoute(List<Point2> route, float speedMultiplier)
        {
            _route.Clear();
            if (route == null) return;
            for (int i = 0; i < route.Count; i++) _route.Add(new Vector3(route[i].X, route[i].Y, 0f));
            _speedMultiplier = speedMultiplier <= 0f ? 1f : speedMultiplier;
        }

        public void TeleportTo(float x, float y)
        {
            _route.Clear();
            transform.position = new Vector3(x, y, 0f);
        }

        // ----- 겉모습 ----------------------------------------------------------

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            if (_body != null) _body.enabled = visible;
            if (_collider != null) _collider.enabled = visible;
            if (_nameLabel != null) _nameLabel.gameObject.SetActive(visible);
            if (_captionLabel != null) _captionLabel.gameObject.SetActive(visible);
        }

        public void SetCaption(string caption)
        {
            if (_captionLabel != null) _captionLabel.text = caption ?? string.Empty;
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
        }

        // ----- 상호작용 --------------------------------------------------------

        public string InteractionPrompt
        {
            get { return "[E] " + KoreanText.WaGwa(displayName) + " 대화"; }
        }

        public bool CanInteract
        {
            get { return !isVictim && IsVisible && ModalState.IsExploring; }
        }

        public void Interact()
        {
            GameEvents.RequestTalk(npcId);
        }
    }
}
