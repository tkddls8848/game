using System.Collections.Generic;
using Detective.Art;
using Detective.Core;
using Detective.Data;
using Detective.Eavesdrop;
using Detective.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Detective.UI
{
    /// <summary>
    /// 관찰 모드의 "청취(소나)" 화면(docs/art-concepts 10번 시안). 저택은 어둠에 가라앉고, 보이는 것은 소리뿐이다.
    /// 말하는 방에서 파문이 퍼지고, 귀가 있는 방은 호박색, 벽을 맞댄 방은 회색으로 표시된다.
    /// 같은 방의 말은 글자로, 옆 방의 말은 블록으로 가려진 웅얼거림으로 뜬다.
    ///
    /// 회차는 탐색 중 돌아가는 EavesdropController의 ListeningSession을 그대로 쓴다 — 여기서 들은 것은 거기서도 들은 것이다.
    /// 다른 점은 청취점이다. 탐색 중에는 탐정이 선 방이지만, 여기서는 **귀**를 따로 옮긴다(WASD).
    /// 닫을 때 청취점을 탐정의 방으로 되돌린다.
    ///
    /// 재생·가청·기록은 순수 C#(ListeningSession · SonarText)이고, 여기는 그리고 키를 넘길 뿐이다.
    /// TimelineController가 Tab으로 열고 닫는다. 그림은 전부 코드가 만든다(링·정사각형·원판) — 외부 에셋 0.
    /// </summary>
    public class SonarView : MonoBehaviour
    {
        [Tooltip("귀를 옮기는 속도(월드 유닛/초).")]
        public float earSpeed = 9f;

        [Tooltip("파문 한 바퀴의 재생 시간(ms). 재생 위치에 묶여 있어 정지하면 파문도 멈춘다.")]
        public int ripplePeriodMs = 2400;

        [Tooltip("같은 방에서 들리는 파문의 최대 반지름(월드 유닛).")]
        public float rippleRadius = 6f;

        [Tooltip("← → 로 건너뛰는 양(ms).")]
        public int seekStepMs = 5000;

        public bool IsActive { get; private set; }

        // 심해·청록·호박·잡음 회색.
        private static readonly Color Cyan = new Color(0.31f, 0.89f, 0.85f);
        private static readonly Color Amber = new Color(1f, 0.71f, 0.33f);
        private static readonly Color Muted = new Color(0.50f, 0.57f, 0.60f);
        private static readonly Color DimColor = new Color(0.016f, 0.027f, 0.039f, 0.84f);
        private static readonly Color PanelBg = new Color(0.016f, 0.047f, 0.063f, 0.90f);
        private static readonly Color Border = new Color(0.12f, 0.37f, 0.42f, 0.9f);
        private static readonly Color TextColor = new Color(0.75f, 0.91f, 0.90f);

        private const int SortDim = 70;
        private const int SortRoom = 71;
        private const int SortSweep = 73;
        private const int SortRipple = 75;
        private const int SortEar = 80;
        private const int SortEarLabel = 81;
        private const int RingCount = 4;
        private const int BubbleCount = 6;

        private ListeningSession _session;
        private ListeningSession _ownSession;
        private AudibilityModel _audibility;
        private EavesdropUI _exploreBubbles;
        private RoomLayout _layout;
        private float _msAccumulator;
        private float _minX, _minY, _maxX, _maxY;
        private int _passes;
        private string _loadError = string.Empty;

        // ----- 월드 --------------------------------------------------------------
        private Transform _stage;
        private Transform _ear;
        private Transform _sweepPivot;
        private readonly Dictionary<string, SpriteRenderer> _roomQuads = new Dictionary<string, SpriteRenderer>();
        private readonly Dictionary<string, RippleSet> _ripples = new Dictionary<string, RippleSet>();
        private readonly List<string> _staleRipples = new List<string>();

        // ----- UI ----------------------------------------------------------------
        private RectTransform _root;
        private Text _title;
        private Text _log;
        private RectTransform _laneArea;
        private RectTransform _playhead;
        private readonly List<Image> _blocks = new List<Image>();
        private readonly List<Utterance> _blockUtterances = new List<Utterance>();
        private readonly List<GameObject> _laneObjects = new List<GameObject>();
        private ListeningSession _lanesBuiltFor;
        private Bubble[] _bubbles;

        private sealed class RippleSet
        {
            public GameObject Root;
            public SpriteRenderer[] Rings;
            public int StartMs;
        }

        private sealed class Bubble
        {
            public RectTransform Rect;
            public Image Bar;
            public Text Who;
            public Text Body;
        }

        // ----- 열고 닫기 ----------------------------------------------------------

        public void Enter()
        {
            if (GameManager.Instance == null) return;
            _layout = GameManager.Instance.Layout;
            if (_layout == null || !_layout.TryGetBounds(out _minX, out _minY, out _maxX, out _maxY)) return;

            if (_stage == null) BuildStage();
            if (_root == null) BuildUI();
            if (_audibility == null) _audibility = new AudibilityModel(_layout);

            _session = ResolveSession();
            if (_session != _lanesBuiltFor) BuildLanes();

            if (_exploreBubbles == null) _exploreBubbles = FindAnyObjectByType<EavesdropUI>();
            if (_exploreBubbles != null) _exploreBubbles.Hidden = true;

            PlaceEarAtPlayer();
            _stage.gameObject.SetActive(true);
            _root.gameObject.SetActive(true);
            IsActive = true;
            _msAccumulator = 0f;
            if (_session != null)
            {
                _session.MoveTo(RoomIdAt(_ear.position));
                Redraw();
            }
        }

        public void Exit()
        {
            IsActive = false;
            if (_exploreBubbles != null) _exploreBubbles.Hidden = false;
            if (_stage != null) _stage.gameObject.SetActive(false);
            if (_root != null) _root.gameObject.SetActive(false);

            // 청취점을 탐정이 선 방으로 되돌린다. 안 그러면 탐색으로 돌아가도 귀가 여기 남아 있다.
            if (_session != null)
            {
                var tracker = FindAnyObjectByType<PlayerRoomTracker>();
                var player = FindAnyObjectByType<PlayerController>();
                string room = tracker != null ? tracker.CurrentRoom
                    : player != null ? RoomIdAt(player.transform.position) : string.Empty;
                _session.MoveTo(room);
            }
        }

        /// <summary>탐색 중 돌아가는 회차를 그대로 쓴다. 컨트롤러가 없으면(옛 씬) 슬라이스로 자기 회차를 만든다.</summary>
        private ListeningSession ResolveSession()
        {
            var controller = FindAnyObjectByType<EavesdropController>();
            if (controller != null && controller.Session != null) return controller.Session;
            if (controller != null && !string.IsNullOrEmpty(controller.LoadError)) _loadError = controller.LoadError;

            if (_ownSession == null)
            {
                ScriptDefinition script = EavesdropScriptLoader.Load(EavesdropScriptLoader.SliceResourcePath);
                if (script == null || script.durationMs <= 0)
                {
                    if (string.IsNullOrEmpty(_loadError)) _loadError = "대본을 읽지 못했다: " + EavesdropScriptLoader.SliceResourcePath;
                    return null;
                }
                _ownSession = new ListeningSession(new ScriptTimeline(script), new AudibilityModel(_layout));
            }
            return _ownSession;
        }

        private void PlaceEarAtPlayer()
        {
            float x, y;
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                x = Mathf.Clamp(player.transform.position.x, _minX, _maxX);
                y = Mathf.Clamp(player.transform.position.y, _minY, _maxY);
            }
            else if (!_layout.TryGetSpawnPosition(out x, out y))
            {
                x = (_minX + _maxX) * 0.5f;
                y = (_minY + _maxY) * 0.5f;
            }
            _ear.position = new Vector3(x, y, 0f);
        }

        private string RoomIdAt(Vector3 p)
        {
            RoomDefinition room = _layout.FindRoomAt(p.x, p.y);
            return room != null ? room.id : string.Empty;
        }

        // ----- 입력 ---------------------------------------------------------------

        /// <summary>TimelineController가 청취 화면일 때 매 프레임 넘겨준다. 닫는 키(T/Esc/Tab)는 거기서 처리한다.</summary>
        public void HandleInput()
        {
            if (!IsActive || _session == null) return;

            float dx = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float dy = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            if (dx != 0f || dy != 0f)
            {
                Vector3 p = _ear.position + new Vector3(dx, dy, 0f).normalized * earSpeed * Time.deltaTime;
                p.x = Mathf.Clamp(p.x, _minX, _maxX);
                p.y = Mathf.Clamp(p.y, _minY, _maxY);
                _ear.position = p;
            }

            PlaybackTransport t = _session.Transport;
            if (Input.GetKeyDown(KeyCode.Space)) t.TogglePlay();
            if (Input.GetKeyDown(KeyCode.R)) { _session.Restart(); _passes++; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) _session.SeekTo(t.PositionMs + seekStepMs);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) _session.SeekTo(t.PositionMs - seekStepMs);
            if (Input.GetKeyDown(KeyCode.LeftBracket)) t.SpeedPercent = Mathf.Max(25, t.SpeedPercent / 2);
            if (Input.GetKeyDown(KeyCode.RightBracket)) t.SpeedPercent = Mathf.Min(400, t.SpeedPercent * 2);
        }

        private void Update()
        {
            if (!IsActive) return;
            if (_session == null)
            {
                _title.text = UIFactory.Colorize(string.IsNullOrEmpty(_loadError) ? "회차가 없다." : _loadError, Amber);
                return;
            }

            // 귀가 선 방이 청취점이다. 벽 속·저택 밖이면 빈 문자열 = 무음.
            _session.MoveTo(RoomIdAt(_ear.position));

            // 프레임 시간을 정수 ms로 넘기되 남은 소수는 다음 프레임으로 넘겨 드리프트를 막는다.
            _msAccumulator += Time.deltaTime * 1000f;
            int ms = (int)_msAccumulator;
            _msAccumulator -= ms;
            if (ms > 0) _session.Advance(ms);

            Redraw();
        }

        // ----- 그리기 -------------------------------------------------------------

        private void Redraw()
        {
            PlaybackTransport t = _session.Transport;
            int now = t.PositionMs;
            string earRoom = _session.ListenerRoom;

            // 방 강조: 귀가 있는 방은 호박색, 벽 너머는 회색, 나머지는 없음.
            foreach (KeyValuePair<string, SpriteRenderer> kv in _roomQuads)
            {
                Audibility level = _audibility.Judge(earRoom, kv.Key);
                Color c = level == Audibility.Full ? Amber : Muted;
                c.a = level == Audibility.Full ? 0.10f : level == Audibility.Muffled ? 0.06f : 0f;
                kv.Value.color = c;
            }

            // 파문: 지금 이 귀에 닿는 발화마다 하나. 닿지 않는(None) 소리는 화면에도 없다.
            IList<PerceivedUtterance> current = _session.Current;
            _staleRipples.Clear();
            foreach (string id in _ripples.Keys) _staleRipples.Add(id);

            for (int i = 0; i < current.Count; i++)
            {
                PerceivedUtterance p = current[i];
                Utterance u;
                if (!_session.Timeline.TryGet(p.UtteranceId, out u)) continue;
                _staleRipples.Remove(u.id);

                RippleSet set;
                if (!_ripples.TryGetValue(u.id, out set))
                {
                    set = CreateRipple(u);
                    _ripples[u.id] = set;
                }
                bool full = p.Level == Audibility.Full;
                TickRipple(set, now, full ? Cyan : Muted, full ? rippleRadius : rippleRadius * 0.7f, full ? 0.9f : 0.5f);
            }
            for (int i = 0; i < _staleRipples.Count; i++)
            {
                Destroy(_ripples[_staleRipples[i]].Root);
                _ripples.Remove(_staleRipples[i]);
            }

            // 소나 회전선: 재생 위치에 묶여 6초에 한 바퀴.
            _sweepPivot.localRotation = Quaternion.Euler(0f, 0f, -(now % 6000) / 6000f * 360f);

            // 발화 버블
            int b = 0;
            for (int i = 0; i < current.Count && b < _bubbles.Length; i++, b++) ShowBubble(_bubbles[b], current[i]);
            for (; b < _bubbles.Length; b++) _bubbles[b].Rect.gameObject.SetActive(false);

            // 제목·기록·타임라인
            string state = t.State == TransportState.Playing ? "▶" : t.State == TransportState.Ended ? "회차 끝 · [R] 처음부터" : "❚❚";
            string where = string.IsNullOrEmpty(earRoom) ? "벽 속" : RoomName(earRoom);
            _title.text = "청취 · 기록 재생   " + UIFactory.Colorize(SonarText.Clock(now), Amber) + " / " + SonarText.Clock(t.DurationMs)
                + "   " + SonarText.Speed(t.SpeedPercent) + "   " + state
                + "\n<size=22>귀: " + UIFactory.Colorize(where, Amber) + "   회차 " + (_passes + 1)
                + "   같은 방 = 글자 · 벽 너머 = 웅얼거림 · 그 밖 = 무음</size>";

            RefreshLog();

            float f = t.DurationMs > 0 ? now / (float)t.DurationMs : 0f;
            _playhead.anchorMin = new Vector2(f, 0f);
            _playhead.anchorMax = new Vector2(f, 1f);
            for (int i = 0; i < _blocks.Count; i++)
            {
                Utterance u = _blockUtterances[i];
                bool ringing = u.startMs <= now && now < u.EndMs;
                bool heard = _session.HeardLevel(u.id) == Audibility.Full;
                Color c = heard ? Cyan : Muted;
                c.a = ringing ? 1f : heard ? 0.85f : 0.35f;
                _blocks[i].color = c;
            }
        }

        private RippleSet CreateRipple(Utterance u)
        {
            float cx, cy;
            if (!_layout.TryGetRoomCenter(u.room, out cx, out cy)) { cx = 0f; cy = 0f; }
            // 같은 방에서 두 목소리가 겹쳐도 파문의 중심이 갈리게 목소리 순번으로 살짝 비킨다.
            int voice = SonarText.Voices(_session.Timeline).IndexOf(u.voiceId);
            if (voice >= 0) cx += voice % 2 == 0 ? -1.4f : 1.4f;

            var set = new RippleSet { StartMs = u.startMs, Rings = new SpriteRenderer[RingCount] };
            set.Root = new GameObject("Ripple_" + u.id);
            set.Root.transform.SetParent(_stage, false);
            set.Root.transform.position = new Vector3(cx, cy, 0f);
            Sprite ring = ArtLibrary.Instance.RingSprite();
            for (int i = 0; i < RingCount; i++)
            {
                var go = new GameObject("Ring_" + i);
                go.transform.SetParent(set.Root.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = ring;
                r.sortingOrder = SortRipple;
                r.enabled = false;
                set.Rings[i] = r;
            }
            return set;
        }

        private void TickRipple(RippleSet set, int nowMs, Color color, float radius, float alpha)
        {
            int elapsed = nowMs - set.StartMs;
            for (int i = 0; i < set.Rings.Length; i++)
            {
                float progress;
                if (!SonarText.RipplePhase(elapsed, ripplePeriodMs, i, RingCount, out progress))
                {
                    set.Rings[i].enabled = false;
                    continue;
                }
                float diameter = Mathf.Max(0.3f, progress * radius * 2f);
                set.Rings[i].enabled = true;
                set.Rings[i].transform.localScale = new Vector3(diameter, diameter, 1f);
                Color c = color;
                c.a = (1f - progress) * (1f - progress) * alpha;
                set.Rings[i].color = c;
            }
        }

        /// <summary>버블은 PerceivedUtterance가 준 것만 그린다. 웅얼거림의 블록은 원문 길이만 빌리고 글자는 하나도 쓰지 않는다.</summary>
        private void ShowBubble(Bubble bubble, PerceivedUtterance p)
        {
            float cx, cy;
            if (!_layout.TryGetRoomCenter(p.Room, out cx, out cy)) { cx = 0f; cy = 0f; }

            bool full = p.Level == Audibility.Full;
            string text;
            if (full) text = p.Text;
            else
            {
                Utterance u;
                text = _session.Timeline.TryGet(p.UtteranceId, out u) ? SonarText.Garble(u.text) : SonarText.Garble("누군가 말하고 있다");
            }
            string who = full
                ? SonarText.VoiceName(p.VoiceId) + " · " + RoomName(p.Room) + " · 같은 방"
                : "웅얼거림 · " + RoomName(p.Room) + " · 벽 너머";

            bubble.Bar.color = full ? Cyan : Muted;
            bubble.Who.color = full ? Cyan : Muted;
            bubble.Body.color = full ? TextColor : Muted;
            bubble.Who.text = who;
            bubble.Body.text = text;
            bubble.Rect.sizeDelta = new Vector2(full ? 560f : 400f, full ? 150f : 96f);

            Vector2 local;
            Camera cam = Camera.main;
            Vector3 screen = cam != null ? cam.WorldToScreenPoint(new Vector3(cx, cy + 1.5f, 0f)) : Vector3.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out local);
            bubble.Rect.anchoredPosition = local;
            bubble.Rect.gameObject.SetActive(true);
        }

        private void RefreshLog()
        {
            List<Utterance> heard = SonarText.FullyHeard(_session);
            var sb = new System.Text.StringBuilder();
            sb.Append(UIFactory.Colorize("들은 말", Cyan)).Append("   ").Append(heard.Count).Append("건 온전히")
              .Append("   놓침 ").Append(SonarText.MissedSoFar(_session)).Append("건\n");
            int from = Mathf.Max(0, heard.Count - 8);
            for (int i = from; i < heard.Count; i++)
            {
                Utterance u = heard[i];
                string body = u.text.Length > 24 ? u.text.Substring(0, 24) + "…" : u.text;
                sb.Append("\n<color=#7F9297>").Append(SonarText.Clock(u.startMs)).Append("</color>  ")
                  .Append(UIFactory.Colorize(SonarText.VoiceName(u.voiceId), Amber)).Append("  ").Append(body);
            }
            if (heard.Count == 0) sb.Append("\n<color=#7F9297>아직 온전히 들은 말이 없다. 말하는 방으로 귀를 옮겨라.</color>");
            _log.text = sb.ToString();
        }

        private string RoomName(string roomId)
        {
            RoomDefinition room;
            return _layout.TryGetRoom(roomId, out room) && !string.IsNullOrEmpty(room.displayName) ? room.displayName : roomId;
        }

        // ----- 만들기: 월드 -------------------------------------------------------

        private void BuildStage()
        {
            _stage = new GameObject("SonarStage").transform;
            Sprite square = ArtLibrary.Instance.SquareSprite();

            // 가림막: 저택 전체를 어둠에 가라앉힌다. 벽·방 이름은 밑에서 희미하게 비친다.
            float margin = 6f;
            CreateQuad("Dim", square, (_minX + _maxX) * 0.5f, (_minY + _maxY) * 0.5f,
                _maxX - _minX + margin * 2f, _maxY - _minY + margin * 2f, DimColor, SortDim);

            IList<RoomDefinition> rooms = _layout.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomDefinition room = rooms[i];
                Color none = Amber;
                none.a = 0f;
                _roomQuads[room.id] = CreateQuad("Room_" + room.id, square, room.CenterX, room.CenterY, room.width, room.height, none, SortRoom);
            }

            // 귀: 호박색 원판 + 회전선.
            var ear = new GameObject("Ear");
            ear.transform.SetParent(_stage, false);
            _ear = ear.transform;
            var body = new GameObject("Body");
            body.transform.SetParent(_ear, false);
            body.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            var bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = ArtLibrary.Instance.DiscSprite();
            bodyRenderer.color = Amber;
            bodyRenderer.sortingOrder = SortEar;
            WorldLabel.Create(_ear, "EarLabel", new Vector3(0f, 0.8f, 0f), 0.04f, Amber, SortEarLabel).text = "귀";

            var pivot = new GameObject("SweepPivot");
            pivot.transform.SetParent(_ear, false);
            _sweepPivot = pivot.transform;
            Color sweep = Amber;
            sweep.a = 0.35f;
            SpriteRenderer line = CreateQuad("Sweep", square, 0f, 0f, rippleRadius * 0.8f, 0.06f, sweep, SortSweep);
            line.transform.SetParent(_sweepPivot, false);
            line.transform.localPosition = new Vector3(rippleRadius * 0.4f, 0f, 0f);
        }

        private SpriteRenderer CreateQuad(string name, Sprite square, float cx, float cy, float w, float h, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_stage, false);
            go.transform.position = new Vector3(cx, cy, 0f);
            go.transform.localScale = new Vector3(w, h, 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = square;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        // ----- 만들기: UI ---------------------------------------------------------

        private void BuildUI()
        {
            _root = UIFactory.CreateStretch("SonarOverlay", transform, 0f);

            RectTransform titleRect = UIFactory.CreateRect("SonarTitle", _root,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -130f), new Vector2(1000f, -30f));
            Panel(titleRect);
            _title = UIFactory.AddText(UIFactory.CreateStretch("Text", titleRect, 18f), 30, TextAnchor.MiddleLeft, TextColor);
            _title.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform logRect = UIFactory.CreateRect("SonarLog", _root,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-560f, 200f), new Vector2(-40f, -30f));
            Panel(logRect);
            _log = UIFactory.AddText(UIFactory.CreateStretch("Text", logRect, 20f), 24, TextAnchor.UpperLeft, TextColor);
            _log.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform stripRect = UIFactory.CreateRect("SonarStrip", _root,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 70f), new Vector2(-600f, 180f));
            Panel(stripRect);
            _laneArea = UIFactory.CreateRect("Lanes", stripRect, Vector2.zero, Vector2.one, new Vector2(120f, 14f), new Vector2(-20f, -14f));
            _playhead = UIFactory.CreateRect("Playhead", _laneArea, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-2f, -6f), new Vector2(2f, 6f));
            UIFactory.AddImage(_playhead, Amber);

            RectTransform helpRect = UIFactory.CreateRect("SonarHelp", _root,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 16f), new Vector2(-40f, 60f));
            Text help = UIFactory.AddText(helpRect, 24, TextAnchor.MiddleLeft, Muted);
            help.text = "[WASD] 귀 옮기기   [Space] 재생·정지   [← →] 5초   [R] 처음부터   [ [ ] ] 배속   [Tab] 타임라인으로   [T / Esc] 닫기";

            _bubbles = new Bubble[BubbleCount];
            for (int i = 0; i < BubbleCount; i++) _bubbles[i] = BuildBubble(i);
        }

        private Bubble BuildBubble(int index)
        {
            var b = new Bubble();
            b.Rect = UIFactory.CreateRect("Bubble_" + index, _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            b.Rect.pivot = new Vector2(0.5f, 0f);
            b.Rect.sizeDelta = new Vector2(560f, 150f);
            Panel(b.Rect);
            RectTransform bar = UIFactory.CreateRect("Bar", b.Rect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(5f, 0f));
            b.Bar = UIFactory.AddImage(bar, Cyan);
            RectTransform who = UIFactory.CreateRect("Who", b.Rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -40f), new Vector2(-14f, -8f));
            b.Who = UIFactory.AddText(who, 19, TextAnchor.MiddleLeft, Cyan);
            RectTransform body = UIFactory.CreateRect("Body", b.Rect, Vector2.zero, Vector2.one, new Vector2(20f, 10f), new Vector2(-14f, -42f));
            b.Body = UIFactory.AddText(body, 26, TextAnchor.UpperLeft, TextColor);
            b.Rect.gameObject.SetActive(false);
            return b;
        }

        private static void Panel(RectTransform rect)
        {
            UIFactory.AddImage(rect, PanelBg);
            UIFactory.AddFrame(rect, 0f, 1.5f, Border);
        }

        /// <summary>목소리마다 한 줄. 발화는 시작·끝 비율을 앵커로 잡아 해상도와 무관하게 맞는다.</summary>
        private void BuildLanes()
        {
            for (int i = 0; i < _laneObjects.Count; i++) Destroy(_laneObjects[i]);
            _laneObjects.Clear();
            _blocks.Clear();
            _blockUtterances.Clear();
            _lanesBuiltFor = _session;
            if (_session == null) return;

            List<string> voices = SonarText.Voices(_session.Timeline);
            int n = Mathf.Max(1, voices.Count);
            float duration = Mathf.Max(1, _session.Transport.DurationMs);
            IList<Utterance> all = _session.Timeline.Utterances;
            for (int v = 0; v < voices.Count; v++)
            {
                float top = 1f - v / (float)n, bottom = 1f - (v + 1) / (float)n;
                float pad = (top - bottom) * 0.2f;

                RectTransform label = UIFactory.CreateRect("Lane_" + v, _laneArea,
                    new Vector2(0f, bottom), new Vector2(0f, top), new Vector2(-100f, 0f), new Vector2(-8f, 0f));
                UIFactory.AddText(label, 20, TextAnchor.MiddleRight, Muted).text = SonarText.VoiceName(voices[v]);
                _laneObjects.Add(label.gameObject);

                for (int i = 0; i < all.Count; i++)
                {
                    Utterance u = all[i];
                    if (u.voiceId != voices[v]) continue;
                    RectTransform block = UIFactory.CreateRect("Block_" + u.id, _laneArea,
                        new Vector2(u.startMs / duration, bottom + pad), new Vector2(u.EndMs / duration, top - pad), Vector2.zero, Vector2.zero);
                    _blocks.Add(UIFactory.AddImage(block, Muted));
                    _blockUtterances.Add(u);
                    _laneObjects.Add(block.gameObject);
                }
            }
            _playhead.SetAsLastSibling();
        }
    }
}
