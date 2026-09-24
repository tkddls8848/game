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
        private Text _timelineLegend;
        private Text[,] _timelineCells;
        private List<NotebookEntry> _timelineRows;

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
                RefreshTimeline();
                _helpLabel.text = "[1~3 / ← →] 탭   [N / Esc] 닫기   ·   [T] 타임라인 관찰 모드에서 동선을 움직여 볼 수 있다";
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

        private void RefreshTimeline()
        {
            if (_timelineCells == null) BuildTimelineGrid();

            TimelineBoard board = TimelineBoard.Build(GameManager.Instance.State);
            for (int r = 0; r < _timelineRows.Count; r++)
            {
                for (int t = GameTime.FirstTick; t <= GameTime.LastTick; t++)
                {
                    _timelineCells[r, t].text = _presenter.TimelineCell(board, _timelineRows[r].Id, t);
                }
            }
        }

        /// <summary>표 모양은 인물 수를 알아야 정해지므로 처음 열 때 만든다.</summary>
        private void BuildTimelineGrid()
        {
            _timelineRows = _presenter.TimelineRows();
            _timelineCells = new Text[_timelineRows.Count, GameTime.TickCount];

            RectTransform grid = UIFactory.CreateRect("Grid", _timelinePanel,
                Vector2.zero, Vector2.one, new Vector2(12f, 52f), new Vector2(-12f, -12f));

            int columns = GameTime.TickCount + 1;
            int rows = _timelineRows.Count + 1;
            const float nameColumnShare = 0.14f;
            float tickShare = (1f - nameColumnShare) / GameTime.TickCount;

            for (int r = 0; r < rows; r++)
            {
                float yMax = 1f - (float)r / rows;
                float yMin = 1f - (float)(r + 1) / rows;
                for (int c = 0; c < columns; c++)
                {
                    float xMin = c == 0 ? 0f : nameColumnShare + (c - 1) * tickShare;
                    float xMax = c == 0 ? nameColumnShare : nameColumnShare + c * tickShare;

                    RectTransform cell = UIFactory.CreateRect("Cell_" + r + "_" + c, grid,
                        new Vector2(xMin, yMin), new Vector2(xMax, yMax), new Vector2(2f, 2f), new Vector2(-2f, -2f));
                    bool header = r == 0 || c == 0;
                    UIFactory.AddImage(cell, header ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.03f));
                    Text label = UIFactory.AddText(UIFactory.CreateStretch("Text", cell, 6f),
                        header ? 24 : 21, header ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, Color.white);

                    if (r == 0 && c == 0) label.text = UIFactory.Colorize("인물 / 시각", UIFactory.MutedColor);
                    else if (r == 0) label.text = "<b>" + GameTime.ToLabel(c - 1) + "</b>";
                    else if (c == 0) label.text = "<b>" + _timelineRows[r - 1].Title + "</b>";
                    else _timelineCells[r - 1, c - 1] = label;
                }
            }
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
            _detailLabel = UIFactory.AddText(UIFactory.CreateStretch("Text", _detailPanel, 24f), 24, TextAnchor.UpperLeft, Color.white);

            _timelinePanel = UIFactory.CreateRect("Timeline", _root,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(32f, 70f), new Vector2(-32f, -100f));
            RectTransform legend = UIFactory.CreateRect("Legend", _timelinePanel,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 4f), new Vector2(-12f, 46f));
            _timelineLegend = UIFactory.AddText(legend, 22, TextAnchor.MiddleLeft, UIFactory.MutedColor);
            _timelineLegend.text = NotebookPresenter.TimelineLegend;

            RectTransform helpRect = UIFactory.CreateRect("Help", _root,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(32f, 14f), new Vector2(-32f, 60f));
            _helpLabel = UIFactory.AddText(helpRect, 24, TextAnchor.MiddleLeft, UIFactory.MutedColor);
        }
    }
}
