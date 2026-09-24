using Detective.Art;
using Detective.Core;
using Detective.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 화면 하단 HUD. 상호작용 안내문과 조사 결과 메시지를 띄운다.
    /// 참조는 SceneBuilder가 씬을 만들 때 끼워 넣는다(손으로 드래그하지 않는다, §18-9).
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        public PlayerInteraction player;
        public Text promptLabel;
        public Text messageLabel;
        public Graphic messageBackground;

        [Tooltip("메시지가 화면에 남아 있는 시간(초).")]
        public float messageDuration = 5f;

        [Tooltip("화면 왼쪽 위 조작 안내.")]
        [TextArea(2, 4)]
        public string controlsHint = "[WASD] 이동   [E] 조사·대화\n[T] 타임라인   [N] 수사 노트   [F] 고발";

        private float _messageTimer;
        private Text _statusLabel;
        private RectTransform _statusPanel;

        private void Awake()
        {
            // 씬에 저장된 메시지 패널에 종이 질감을 입힌다(런타임 생성 스프라이트는 씬에 직렬화되지 않는다).
            var paper = messageBackground as Image;
            if (paper != null)
            {
                paper.sprite = ArtLibrary.Instance.ParchmentSprite();
                paper.color = UIFactory.PanelColor;
                UIFactory.AddFrame(paper.rectTransform, 0f, 2f, new Color(0.16f, 0.13f, 0.10f, 0.85f));
            }
            UIFactory.AddVignette(transform);

            _statusLabel = UIFactory.CreateTextPanel("StatusPanel", transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -190f), new Vector2(600f, -24f),
                26, TextAnchor.MiddleLeft, out _statusPanel);
            // 다른 창(노트·대화·고발)이 항상 이 패널 위에 그려지도록 맨 아래로 보낸다.
            _statusPanel.SetAsFirstSibling();
            _statusLabel.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void OnEnable()
        {
            GameEvents.MessageShown += ShowMessage;
            SetMessageVisible(false);
        }

        private void OnDisable()
        {
            GameEvents.MessageShown -= ShowMessage;
        }

        private void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerInteraction>();
        }

        private void Update()
        {
            UpdatePrompt();
            UpdateStatus();
            UpdateMessageTimer();
        }

        private void UpdateStatus()
        {
            // 탐색 중에만 보인다. 다른 창은 자기 안내문을 따로 띄운다.
            bool visible = ModalState.IsExploring;
            if (_statusPanel.gameObject.activeSelf != visible) _statusPanel.gameObject.SetActive(visible);
            if (!visible) return;

            _statusLabel.text = UIFactory.Colorize("블랙우드 저택 · 현재 " + GameTime.ToLabel(GameTime.PresentTick), UIFactory.AccentColor)
                + "\n" + controlsHint;
        }

        private void UpdatePrompt()
        {
            if (promptLabel == null) return;

            IInteractable current = player != null ? player.Current : null;
            promptLabel.text = current != null ? current.InteractionPrompt : string.Empty;
        }

        private void UpdateMessageTimer()
        {
            if (_messageTimer <= 0f) return;

            _messageTimer -= Time.deltaTime;
            if (_messageTimer > 0f) return;

            SetMessageVisible(false);
        }

        private void ShowMessage(string message)
        {
            if (messageLabel == null) return;

            messageLabel.text = message;
            _messageTimer = messageDuration;
            SetMessageVisible(true);
        }

        private void SetMessageVisible(bool visible)
        {
            if (messageLabel != null) messageLabel.enabled = visible;
            // 테두리 선이 패널의 자식이므로 Image만 끄면 선이 남는다. 오브젝트째로 끈다.
            if (messageBackground != null) messageBackground.gameObject.SetActive(visible);
            if (!visible) _messageTimer = 0f;
        }
    }
}
