using System.Collections.Generic;
using Detective.Art;
using Detective.Core;
using Detective.Data;
using Detective.Dialogue;
using Detective.Investigation;
using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 대화창. 인물에게 말을 걸면(GameEvents.TalkRequested) 대사를 한 줄씩 보여 준다.
    /// 화면에 띄운 줄만 "들은 것"으로 기록한다 — 수사 노트와 타임라인 복원은 그 기록만 쓴다.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        private RectTransform _root;
        private Image _portrait;
        private Text _portraitInitial;
        private Text _speakerLabel;
        private Text _bodyLabel;
        private Text _footerLabel;

        private readonly List<ConversationLine> _lines = new List<ConversationLine>();
        private int _index;
        private string _npcId;
        private string _npcName;
        private int _newLines;

        private void Awake()
        {
            _root = UIFactory.CreateRect("Dialogue", transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-760f, 40f), new Vector2(760f, 360f));
            UIFactory.AddPaper(_root);

            // 왼쪽 초상화 액자. 그림이 없으면 세피아 실루엣 위에 이름 첫 글자를 얹는다.
            RectTransform frame = UIFactory.CreateRect("Portrait", _root,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(28f, 28f), new Vector2(292f, -28f));
            _portrait = UIFactory.AddFramedImage(frame, null);
            RectTransform initial = UIFactory.CreateStretch("Initial", frame, 0f);
            _portraitInitial = UIFactory.AddText(initial, 96, TextAnchor.MiddleCenter, new Color(0.85f, 0.78f, 0.62f, 0.75f));

            RectTransform speaker = UIFactory.CreateRect("Speaker", _root,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(320f, -76f), new Vector2(-36f, -16f));
            _speakerLabel = UIFactory.AddText(speaker, 34, TextAnchor.MiddleLeft, UIFactory.AccentColor);

            RectTransform body = UIFactory.CreateRect("Body", _root,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(320f, 64f), new Vector2(-36f, -84f));
            _bodyLabel = UIFactory.AddText(body, 30, TextAnchor.UpperLeft, UIFactory.Ink);

            RectTransform footer = UIFactory.CreateRect("Footer", _root,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(36f, 12f), new Vector2(-36f, 56f));
            _footerLabel = UIFactory.AddText(footer, 24, TextAnchor.MiddleRight, UIFactory.MutedColor);

            _root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.TalkRequested += OnTalkRequested;
        }

        private void OnDisable()
        {
            GameEvents.TalkRequested -= OnTalkRequested;
        }

        private void OnTalkRequested(string npcId)
        {
            GameManager manager = GameManager.Instance;
            if (manager == null) return;

            NpcDefinition npc;
            if (!manager.Database.Npcs.TryGet(npcId, out npc)) return;

            List<ConversationLine> lines = ConversationBuilder.Build(manager.State, npcId);
            if (lines.Count == 0)
            {
                GameEvents.ShowMessage(npc.displayName + "은(는) 아무 말도 하지 않는다.");
                return;
            }
            if (!ModalState.TryEnter(GameMode.Dialogue, Time.frameCount)) return;

            _lines.Clear();
            _lines.AddRange(lines);
            _index = 0;
            _npcId = npcId;
            _npcName = npc.displayName;
            _newLines = 0;

            _portrait.sprite = ArtLibrary.Instance.Portrait(npc);
            _portraitInitial.text = ArtLibrary.Instance.HasPortraitFile(npcId) ? string.Empty : npc.displayName.Substring(0, 1);

            _root.gameObject.SetActive(true);
            ShowCurrent();
        }

        private void Update()
        {
            int frame = Time.frameCount;
            if (!ModalState.AcceptsInput(GameMode.Dialogue, frame)) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)
                || Input.GetMouseButtonDown(0))
            {
                _index++;
                if (_index >= _lines.Count) Close();
                else
                {
                    GameEvents.RequestSfx("page");
                    ShowCurrent();
                }
            }
        }

        private void ShowCurrent()
        {
            ConversationLine line = _lines[_index];
            if (GameManager.Instance.State.MarkHeard(_npcId, line.Key)) _newLines++;

            string tag = line.IsSighting ? "  " + UIFactory.Colorize("<size=24>" + Localization.Text("dialogue.sighting", "[목격]") + "</size>", UIFactory.MutedColor)
                : line.IsConditional ? "  " + UIFactory.Colorize("<size=24>" + Localization.Text("dialogue.onevidence", "[단서에 대한 반응]") + "</size>", UIFactory.MutedColor)
                : string.Empty;

            _speakerLabel.text = _npcName + tag;
            _bodyLabel.text = line.Text;
            _footerLabel.text = (_index + 1) + " / " + _lines.Count + Localization.Text("dialogue.keys", "     [E / Space] 다음   [Esc] 닫기");
        }

        private void Close()
        {
            _root.gameObject.SetActive(false);
            ModalState.Exit(GameMode.Dialogue, Time.frameCount);

            if (_newLines > 0)
            {
                GameEvents.RaiseNotebookUpdated();
                GameEvents.ShowMessage(UIFactory.Colorize("▶ 수사 노트에 " + KoreanText.WaGwa(_npcName) + "의 대화를 기록했다.", UIFactory.AccentColor));
            }
        }
    }
}
