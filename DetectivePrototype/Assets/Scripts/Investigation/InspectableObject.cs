using Detective.Core;
using Detective.Data;
using UnityEngine;

namespace Detective.Investigation
{
    /// <summary>
    /// E로 조사하면 설명을 띄우는 오브젝트. evidenceId가 있으면 처음 조사할 때 단서를 얻는다.
    /// 단서의 이름·설명은 실행 시 evidence.json에서 다시 읽는다 — 씬에 복사된 문구가 JSON과 어긋나지 않게.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InspectableObject : MonoBehaviour, IInteractable, IHighlightable
    {
        [Tooltip("안내문에 표시할 이름.")]
        public string displayName = "물건";

        [TextArea(2, 5)]
        public string description = "특별한 것은 없다.";

        [Tooltip("비워 두면 단서를 주지 않는다. evidence.json의 id.")]
        public string evidenceId = "";

        [Tooltip("이미 조사한 뒤에도 다시 조사할 수 있는가.")]
        public bool repeatable = true;

        /// <summary>한 번이라도 조사했는가. 런타임 상태이므로 데이터 파일에는 남기지 않는다.</summary>
        public bool Inspected { get; private set; }

        private SpriteRenderer _renderer;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;
        private bool _highlighted;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null) _baseColor = _renderer.color;
            _baseScale = transform.localScale;
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(evidenceId) || GameManager.Instance == null) return;

            EvidenceDefinition evidence;
            if (GameManager.Instance.Database.Evidence.TryGet(evidenceId, out evidence))
            {
                displayName = evidence.name;
                description = evidence.description;
            }
        }

        private void Update()
        {
            // 강조 중일 때만 크기를 건드린다. 매 프레임 스케일을 쓰면 정적 콜라이더가 계속 다시 만들어진다.
            if (!_highlighted) return;
            float pulse = 1.15f + Mathf.Sin(Time.unscaledTime * 8f) * 0.05f;
            transform.localScale = _baseScale * pulse;
        }

        public string InteractionPrompt
        {
            get { return Inspected ? "[E] " + displayName + " 다시 조사" : "[E] " + displayName + " 조사"; }
        }

        public bool CanInteract
        {
            get { return repeatable || !Inspected; }
        }

        public void Interact()
        {
            bool first = !Inspected;
            Inspected = true;

            bool isEvidence = !string.IsNullOrEmpty(evidenceId);
            string message = "<b>" + displayName + "</b>\n" + description;
            if (first && isEvidence) message += "\n<color=#FFD98C>▶ 수사 노트에 단서를 기록했다.</color>";
            GameEvents.ShowMessage(message);
            GameEvents.RequestSfx("inspect");

            // 조사를 마친 단서는 살짝 어둡게 해서 다시 찾아다니지 않게 한다.
            if (isEvidence && _renderer != null) _renderer.color = _baseColor * new Color(0.55f, 0.55f, 0.55f, 1f);

            if (first && isEvidence)
            {
                GameEvents.RaiseEvidenceCollected(evidenceId);
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
            if (!highlighted) transform.localScale = _baseScale;
        }
    }
}
