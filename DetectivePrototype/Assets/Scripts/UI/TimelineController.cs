using Detective.Core;
using Detective.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 타임라인 관찰 모드(T). 18:00~19:00을 앞뒤로 오가며 그 시각 인물들이 어디 있었는지 저택 전체를 내려다본다.
    /// 무엇을 보여 줄지는 GameManager.TimelineSource가 정한다 — 플레이어가 모은 기록만 보인다.
    /// </summary>
    public class TimelineController : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.T;

        [Tooltip("자동 재생 시 한 틱에 머무는 시간(초).")]
        public float autoplayInterval = 1.8f;

        [Tooltip("전체 맵이 화면에 들어오도록 할 때 가장자리 여백(월드 유닛).")]
        public float overviewMargin = 2f;

        public int CurrentTick { get; private set; }
        public bool IsOpen { get { return ModalState.Current == GameMode.Timeline; } }

        private NpcDirector _director;
        private Camera _camera;
        private CameraFollow _follow;
        private Vector3 _savedCameraPosition;
        private float _savedCameraSize;

        private bool _autoplay;
        private float _autoplayTimer;

        private RectTransform _root;
        private Text _titleLabel;
        private Text _helpLabel;
        private Text[] _tickLabels;
        private Image[] _tickBackgrounds;

        private static readonly Color TickIdle = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        private static readonly Color TickActive = new Color(0.85f, 0.66f, 0.30f, 0.95f);

        private void Awake()
        {
            BuildUI();
            _root.gameObject.SetActive(false);
        }

        private void Start()
        {
            _director = FindAnyObjectByType<NpcDirector>();
            _camera = Camera.main;
            if (_camera != null) _follow = _camera.GetComponent<CameraFollow>();
        }

        private void Update()
        {
            int frame = Time.frameCount;

            if (ModalState.IsExploring)
            {
                if (Input.GetKeyDown(toggleKey) && ModalState.TryEnter(GameMode.Timeline, frame)) Open();
                return;
            }

            if (!ModalState.AcceptsInput(GameMode.Timeline, frame)) return;

            if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(+1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
            for (int t = GameTime.FirstTick; t <= GameTime.LastTick; t++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + t)) SetTick(t, false);
            }

#if UNITY_EDITOR
            // 개발 확인용: 실제 스케줄과 복원된 타임라인을 오간다. 빌드에는 들어가지 않는다.
            if (Input.GetKeyDown(KeyCode.F9) && GameManager.Instance != null)
            {
                GameManager.Instance.ShowTruthForDebug = !GameManager.Instance.ShowTruthForDebug;
                SetTick(CurrentTick, false);
            }
#endif

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _autoplay = !_autoplay;
                _autoplayTimer = autoplayInterval;
                RefreshLabels();
            }

            if (_autoplay)
            {
                _autoplayTimer -= Time.deltaTime;
                if (_autoplayTimer <= 0f)
                {
                    _autoplayTimer = autoplayInterval;
                    SetTick(CurrentTick >= GameTime.LastTick ? GameTime.FirstTick : CurrentTick + 1, false);
                }
            }
        }

        private void Open()
        {
            _autoplay = false;
            _root.gameObject.SetActive(true);
            EnterOverviewCamera();
            SetTick(CurrentTick, true);
        }

        private void Close()
        {
            _autoplay = false;
            _root.gameObject.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.ShowTruthForDebug = false;
            ExitOverviewCamera();
            if (_director != null) _director.ReturnToPresent(true);
            ModalState.Exit(GameMode.Timeline, Time.frameCount);
        }

        private void Step(int delta)
        {
            _autoplay = false;
            SetTick(GameTime.Clamp(CurrentTick + delta), false);
        }

        private void SetTick(int tick, bool instant)
        {
            CurrentTick = GameTime.Clamp(tick);
            ITimelinePlacementSource source = GameManager.Instance != null ? GameManager.Instance.TimelineSource : null;
            if (_director != null) _director.ShowTick(CurrentTick, source, instant);
            RefreshLabels();
        }

        // ----- 카메라 ----------------------------------------------------------

        private void EnterOverviewCamera()
        {
            if (_camera == null || GameManager.Instance == null) return;

            _savedCameraPosition = _camera.transform.position;
            _savedCameraSize = _camera.orthographicSize;
            if (_follow != null) _follow.enabled = false;

            float minX, minY, maxX, maxY;
            if (!GameManager.Instance.Layout.TryGetBounds(out minX, out minY, out maxX, out maxY)) return;

            float width = maxX - minX + overviewMargin * 2f;
            // 위쪽 제목 패널과 아래쪽 시간 막대가 화면을 가리므로 세로 여백을 더 둔다.
            float height = maxY - minY + overviewMargin * 2f + 6f;
            float aspect = _camera.aspect > 0.01f ? _camera.aspect : 16f / 9f;

            _camera.orthographicSize = Mathf.Max(height * 0.5f, width * 0.5f / aspect);
            _camera.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f - 1f, _savedCameraPosition.z);
        }

        private void ExitOverviewCamera()
        {
            if (_camera == null) return;

            _camera.transform.position = _savedCameraPosition;
            _camera.orthographicSize = _savedCameraSize;
            if (_follow != null)
            {
                _follow.ResetVelocity();
                _follow.enabled = true;
            }
        }

        // ----- UI --------------------------------------------------------------

        private void BuildUI()
        {
            _root = UIFactory.CreateStretch("TimelineOverlay", transform, 0f);

            RectTransform titlePanel;
            _titleLabel = UIFactory.CreateTextPanel("TimelineTitle", _root,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-600f, -160f), new Vector2(600f, -20f),
                38, TextAnchor.MiddleCenter, out titlePanel);
            _titleLabel.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform bar = UIFactory.CreateRect("TickBar", _root,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-770f, 70f), new Vector2(770f, 150f));

            _tickLabels = new Text[GameTime.TickCount];
            _tickBackgrounds = new Image[GameTime.TickCount];
            float cellWidth = 1540f / GameTime.TickCount;
            for (int t = 0; t < GameTime.TickCount; t++)
            {
                RectTransform cell = UIFactory.CreateRect("Tick_" + t, bar,
                    new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(t * cellWidth + 4f, 0f), new Vector2((t + 1) * cellWidth - 4f, 0f));
                _tickBackgrounds[t] = UIFactory.AddImage(cell, TickIdle);
                RectTransform labelRect = UIFactory.CreateStretch("Label", cell, 4f);
                _tickLabels[t] = UIFactory.AddText(labelRect, 32, TextAnchor.MiddleCenter, Color.white);
                _tickLabels[t].text = GameTime.ToLabel(t);
            }

            RectTransform helpRect = UIFactory.CreateRect("TimelineHelp", _root,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-770f, 16f), new Vector2(770f, 64f));
            _helpLabel = UIFactory.AddText(helpRect, 26, TextAnchor.MiddleCenter, UIFactory.MutedColor);
        }

        private void RefreshLabels()
        {
            bool truth = GameManager.Instance != null && GameManager.Instance.ShowTruthForDebug;
            _titleLabel.text = "타임라인 관찰  " + UIFactory.Colorize(GameTime.ToLabel(CurrentTick), UIFactory.AccentColor)
                + (_autoplay ? "  ▶ 재생 중" : string.Empty)
                + (truth
                    ? "\n<size=24><color=#FF7070>[개발용] 실제 스케줄 표시 중 (F9)</color></size>"
                    : "\n<size=24>수집한 증언·목격·물증으로 복원한 동선만 보인다</size>");

            for (int t = 0; t < GameTime.TickCount; t++)
            {
                bool active = t == CurrentTick;
                _tickBackgrounds[t].color = active ? TickActive : TickIdle;
                _tickLabels[t].color = active ? Color.black : Color.white;
            }

            _helpLabel.text = "[← →] 시각 이동   [1~7] 바로 가기   [Space] 자동 재생   [T / Esc] 닫기";
        }
    }
}
