using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 지금 이 귀에 닿는 발화를 화면에 띄운다(Phase U-2의 "발화 버블 UI, 웅얼거림 표시").
    ///
    /// 같은 방이면 대사 전문이, 벽 너머면 웅얼거림 표시만 나온다.
    /// 웅얼거림에 내용을 조금이라도 흘리면 안 된다 — 그걸 들으러 옮겨 가는 것이 이 게임이다.
    /// 그래서 이 화면은 PerceivedUtterance가 준 것만 그린다. 대본 원문을 다시 뒤지지 않는다.
    /// </summary>
    public class EavesdropUI : MonoBehaviour
    {
        [Tooltip("동시에 띄울 수 있는 버블 수. 대본에서 겹치는 발화가 이보다 많으면 오래된 것부터 밀린다.")]
        public int bubbleCapacity = 4;

        public EavesdropController controller;

        private readonly List<RectTransform> _panels = new List<RectTransform>();
        private readonly List<Text> _labels = new List<Text>();
        private Text _status;
        private RectTransform _statusPanel;

        /// <summary>다른 화면(청취·소나)이 같은 회차를 자기 식으로 그리는 동안 이 버블은 숨긴다.</summary>
        public bool Hidden { get; set; }

        private static readonly Color MuffledInk = new Color(0.42f, 0.37f, 0.32f);
        private static readonly Color MuffledPaper = new Color(0.62f, 0.58f, 0.50f, 0.72f);

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<EavesdropController>();

            // 회차 시계 — 오른쪽 위.
            _status = UIFactory.CreateTextPanel("EavesdropStatus", transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-520f, -132f), new Vector2(-24f, -24f),
                26, TextAnchor.MiddleLeft, out _statusPanel);

            // 발화 버블 — 오른쪽에 세로로 쌓는다.
            const float height = 128f;
            const float gap = 12f;
            for (int i = 0; i < Mathf.Max(1, bubbleCapacity); i++)
            {
                float top = -150f - i * (height + gap);
                RectTransform panel;
                Text label = UIFactory.CreateTextPanel("Bubble" + i, transform,
                    new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-520f, top - height), new Vector2(-24f, top),
                    24, TextAnchor.UpperLeft, out panel);
                panel.gameObject.SetActive(false);
                _panels.Add(panel);
                _labels.Add(label);
            }
        }

        private void LateUpdate()
        {
            _statusPanel.gameObject.SetActive(!Hidden);
            if (Hidden)
            {
                HideFrom(0);
                return;
            }

            ListeningSession session = controller != null ? controller.Session : null;
            if (session == null)
            {
                _status.text = controller != null && !string.IsNullOrEmpty(controller.LoadError)
                    ? UIFactory.Colorize(controller.LoadError, UIFactory.AccentColor)
                    : string.Empty;
                HideFrom(0);
                return;
            }

            _status.text = BuildStatus(session);

            IList<PerceivedUtterance> current = session.Current;
            int shown = 0;
            for (int i = 0; i < current.Count && shown < _panels.Count; i++, shown++)
            {
                PerceivedUtterance p = current[i];
                bool full = p.Level == Audibility.Full;

                var paper = _panels[shown].GetComponent<Image>();
                if (paper != null) paper.color = full ? UIFactory.PanelColor : MuffledPaper;
                _labels[shown].color = full ? UIFactory.Ink : MuffledInk;
                _labels[shown].text = full ? FullText(p) : MuffledText(p);
                _panels[shown].gameObject.SetActive(true);
            }
            HideFrom(shown);
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < _panels.Count; i++) _panels[i].gameObject.SetActive(false);
        }

        private string FullText(PerceivedUtterance p)
        {
            return UIFactory.Colorize(VoiceLabel(p.VoiceId), UIFactory.AccentColor) + "\n" + p.Text;
        }

        private string MuffledText(PerceivedUtterance p)
        {
            // 누가 말하는지도, 무슨 말인지도 주지 않는다. 방향만 준다.
            return "<b>" + Localization.Text("eaves.murmur", "웅얼거림") + "</b>\n" + RoomName(p.Room) + Localization.Text("eaves.direction", " 쪽에서 누군가 말하고 있다");
        }

        /// <summary>목소리는 아직 이름이 아니다(U-3에서 플레이어가 배정한다). 번호로만 부른다.</summary>
        private static string VoiceLabel(string voiceId)
        {
            if (string.IsNullOrEmpty(voiceId)) return "목소리";
            string digits = voiceId.TrimStart('v', 'V');
            int n;
            return int.TryParse(digits, out n) ? "목소리 " + n : voiceId;
        }

        private static string RoomName(string roomId)
        {
            GameManager manager = GameManager.Instance;
            RoomLayout layout = manager != null ? manager.Layout : null;
            RoomDefinition room;
            if (layout != null && layout.TryGetRoom(roomId, out room) && !string.IsNullOrEmpty(room.displayName))
                return room.displayName;
            return string.IsNullOrEmpty(roomId) ? Localization.Text("eaves.somewhere", "어딘가") : roomId;
        }

        private static string BuildStatus(ListeningSession session)
        {
            string here = string.IsNullOrEmpty(session.ListenerRoom) ? Localization.Text("eaves.threshold", "문턱") : RoomName(session.ListenerRoom);
            string clock = Clock(session.PositionMs) + " / " + Clock(session.Timeline.DurationMs);
            string state = session.Transport.IsPlaying ? string.Empty
                : "  " + UIFactory.Colorize(Localization.Text("eaves.paused", "멈춤"), UIFactory.AccentColor);

            return "회차 " + clock + state
                + "\n지금 " + here + "   들은 발화 " + session.FullyHeardCount + "개"
                + "\n<size=20>" + Localization.Text("eaves.keys", "[Space] 멈춤·이어 듣기   [R] 처음부터") + "</size>";
        }

        private static string Clock(int ms)
        {
            if (ms < 0) ms = 0;
            int totalSeconds = ms / 1000;
            return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        }
    }
}
