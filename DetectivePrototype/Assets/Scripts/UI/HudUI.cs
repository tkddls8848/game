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

        private float _messageTimer;

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
            UpdateMessageTimer();
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
            if (messageBackground != null) messageBackground.enabled = visible;
            if (!visible) _messageTimer = 0f;
        }
    }
}
