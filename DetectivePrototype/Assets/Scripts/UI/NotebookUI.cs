using System.Collections.Generic;
using Detective.Core;
using Detective.Investigation;
using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 수사 노트(N). 탭 3개: 인물 / 증거 / 타임라인. 왼쪽 목록에서 고르면 오른쪽에 상세가 나온다.
    /// 문구는 전부 NotebookPresenter(순수 C#)가 만들고, 여기서는 배치와 키 입력만 처리한다.
    /// </summary>
    public class NotebookUI : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.N;

        private static readonly string[] TabNames = { "인물", "증거", "타임라인" };
        private const int TabPeople = 0;
        private const int TabEvidence = 1;
        private const int TabTimeline = 2;

        private int _tab = TabEvidence;
        private readonly int[] _selection = new int[3];

        private RectTransform _root;
        private Text _tabsLabel;
        private Text _listLabel;
        private Text _detailLabel;
        private Text _helpLabel;
        private RectTransform _listPanel;
        private RectTransform _detailPanel;
        private RectTransform _timelinePanel;
        private Text _timelineLabel;

        private NotebookPresenter _presenter;

        private void Awake()
        {
            BuildUI();
            _root.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (GameManager.Instance != null) _presenter = new NotebookPresenter(GameManager.Instance.State);
        }

        private void OnEnable()
        {
            GameEvents.NotebookUpdated += OnNotebookUpdated;
        }

        private void OnDisable()
        {
            GameEvents.NotebookUpdated -= OnNotebookUpdated;
        }

        private void OnNotebookUpdated()
        {
            if (_root.gameObject.activeSelf) Refresh();
        }

        private void Update()
        {
            if (_presenter == null) return;
            int frame = Time.frameCount;

            if (ModalState.IsExploring)
            {
                if (Input.GetKeyDown(toggleKey) && ModalState.TryEnter(GameMode.Notebook, frame))
                {
                    _root.gameObject.SetActive(true);
                    Refresh();
                }
                return;
            }

            if (!ModalState.AcceptsInput(GameMode.Notebook, frame)) return;

            if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(KeyCode.Escape))
            {
                _root.gameObject.SetActive(false);
                ModalState.Exit(GameMode.Notebook, frame);
                return;
            }

            bool changed = false;
            if (Input.GetKeyDown(KeyCode.Alpha1)) { _tab = TabPeople; changed = true; }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { _tab = TabEvidence; changed = true; }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { _tab = TabTimeline; changed = true; }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { _tab = (_tab + 1) % TabNames.Length; changed = true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { _tab = (_tab + TabNames.Length - 1) % TabNames.Length; changed = true; }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { _selection[_tab]++; changed = true; }
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { _selection[_tab]--; changed = true; }

            if (changed) Refresh();
        }

        private void Refresh()
        {
            var tabs = new System.Text.StringBuilder();
            for (int i = 0; i < TabNames.Length; i++)
            {
                string name = "[" + (i + 1) + "] " + TabNames[i];
                tabs.Append(i == _tab ? UIFactory.Colorize("<b>" + name + "</b>", UIFactory.AccentColor) : name);
                tabs.Append("      ");
            }
            _tabsLabel.text = "<size=40><b>수사 노트</b></size>      " + tabs;

            bool timeline = _tab == TabTimeline;
            _listPanel.gameObject.SetActive(!timeline);
            _detailPanel.gameObject.SetActive(!timeline);
            _timelinePanel.gameObject.SetActive(timeline);

            if (timeline)
            {
                _timelineLabel.text = TimelineText();
                _helpLabel.text = "[1~3 / ← →] 탭   [N / Esc] 닫기";
                return;
            }

            List<NotebookEntry> entries = _tab == TabPeople ? _presenter.PeopleEntries() : _presenter.EvidenceEntries();
            if (entries.Count == 0)
            {
                _listLabel.text = UIFactory.Colorize("아직 없음", UIFactory.MutedColor);
                _detailLabel.text = _tab == TabEvidence
                    ? UIFactory.Colorize("저택을 돌아다니며 [E]로 물건을 조사하면 단서가 여기에 기록된다.", UIFactory.MutedColor)
                    : string.Empty;
                _helpLabel.text = "[1~3 / ← →] 탭   [N / Esc] 닫기";
                return;
            }

            int selected = Mathf.Clamp(_selection[_tab], 0, entries.Count - 1);
            _selection[_tab] = selected;

            var list = new System.Text.StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i == selected) list.Append(UIFactory.Colorize("▶ <b>" + entries[i].Title + "</b>", UIFactory.AccentColor));
                else list.Append("   ").Append(entries[i].Title);
                list.Append("\n");
            }
            _listLabel.text = list.ToString();

            string id = entries[selected].Id;
            _detailLabel.text = _tab == TabPeople ? _presenter.PersonDetail(id) : _presenter.EvidenceDetail(id);
            _helpLabel.text = "[↑ ↓] 선택   [1~3 / ← →] 탭   [N / Esc] 닫기";
        }

        private string TimelineText()
        {
            return UIFactory.Colorize("아직 복원된 행적이 없다.", UIFactory.MutedColor);
        }

        private void BuildUI()
        {
            _root = UIFactory.CreateRect("Notebook", transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-840f, -480f), new Vector2(840f, 480f));
            UIFactory.AddImage(_root, UIFactory.PanelColor);

            RectTransform tabsRect = UIFactory.CreateRect("Tabs", _root,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -90f), new Vector2(-32f, -16f));
            _tabsLabel = UIFactory.AddText(tabsRect, 30, TextAnchor.MiddleLeft, Color.white);

            _listPanel = UIFactory.CreateRect("List", _root,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(32f, 70f), new Vector2(472f, -100f));
            UIFactory.AddImage(_listPanel, new Color(1f, 1f, 1f, 0.04f));
            _listLabel = UIFactory.AddText(UIFactory.CreateStretch("Text", _listPanel, 18f), 28, TextAnchor.UpperLeft, Color.white);

            _detailPanel = UIFactory.CreateRect("Detail", _root,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(496f, 70f), new Vector2(-32f, -100f));
            UIFactory.AddImage(_detailPanel, new Color(1f, 1f, 1f, 0.04f));
            _detailLabel = UIFactory.AddText(UIFactory.CreateStretch("Text", _detailPanel, 24f), 28, TextAnchor.UpperLeft, Color.white);

            _timelinePanel = UIFactory.CreateRect("Timeline", _root,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(32f, 70f), new Vector2(-32f, -100f));
            UIFactory.AddImage(_timelinePanel, new Color(1f, 1f, 1f, 0.04f));
            _timelineLabel = UIFactory.AddText(UIFactory.CreateStretch("Text", _timelinePanel, 18f), 24, TextAnchor.UpperLeft, Color.white);

            RectTransform helpRect = UIFactory.CreateRect("Help", _root,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(32f, 14f), new Vector2(-32f, 60f));
            _helpLabel = UIFactory.AddText(helpRect, 24, TextAnchor.MiddleLeft, UIFactory.MutedColor);
        }
    }
}
