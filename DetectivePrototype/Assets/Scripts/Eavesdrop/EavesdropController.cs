using System.Collections.Generic;
using Detective.Core;
using Detective.Data;
using Detective.NPC;
using UnityEngine;

namespace Detective.Eavesdrop
{
    /// <summary>
    /// 엿듣기 한 회차를 씬에서 돌린다(DEVELOPMENT_PLAN_UNHEARD.md Phase U-2).
    ///
    /// 하는 일은 셋뿐이다. 대본을 올리고, 재생을 진행시키고, 플레이어가 선 방을 청취점으로 넘긴다.
    /// 무엇이 들리는지 판정하는 것은 순수 C#(ListeningSession · AudibilityModel)이고
    /// 보여 주는 것은 EavesdropUI다. 여기서는 Unity 쪽 배선만 한다.
    ///
    /// 탐색 모드에서만 시간이 흐른다. 대화·노트·고발 창이 열려 있는 동안 회차가 지나가 버리면
    /// 플레이어가 놓친 것이 자기 선택이 아니게 된다.
    /// </summary>
    public class EavesdropController : MonoBehaviour
    {
        [Tooltip("올릴 대본의 Resources 경로. 비우면 수직 슬라이스를 쓴다.")]
        public string scriptResourcePath = EavesdropScriptLoader.SliceResourcePath;

        [Tooltip("씬이 시작하면 바로 회차를 재생한다.")]
        public bool autoPlay = true;

        /// <summary>회차 상태. UI가 읽는다. 대본을 못 읽으면 null이다.</summary>
        public ListeningSession Session { get; private set; }

        /// <summary>대본을 올리지 못했다면 이유가 여기 남는다(UI가 조용히 비어 있지 않도록).</summary>
        public string LoadError { get; private set; }

        /// <summary>이 회차의 사건 id. 음성 파일 경로를 만들 때 쓴다.</summary>
        public string CaseId { get; private set; }

        /// <summary>인물 이동 트랙. 없을 수도 있다.</summary>
        public MovementTracks Tracks { get; private set; }

        /// <summary>누가 지금 어디 있는가 — 거리감 계산의 근거.</summary>
        public SpeakerPositions Positions { get; private set; }

        /// <summary>앞뒤로 건너뛰는 폭(ms).</summary>
        public int seekStepMs = 5000;

        private void Start()
        {
            LoadError = string.Empty;

            ScriptDefinition script = EavesdropScriptLoader.Load(scriptResourcePath);
            if (script == null)
            {
                LoadError = "대본을 읽지 못했다: " + scriptResourcePath;
                Debug.LogWarning("[EavesdropController] " + LoadError);
                return;
            }

            // 대본 검사는 방·인물 참조까지 본다. 그래서 데이터를 먼저 갖춰 놓고 검사한다.
            GameManager manager = GameManager.Instance;
            CaseDatabase database = manager != null ? manager.Database : null;
            RoomLayout layout = manager != null ? manager.Layout : null;
            if (layout == null || database == null)
            {
                LoadError = "방 배치를 읽지 못해 가청 판정을 할 수 없다.";
                Debug.LogWarning("[EavesdropController] " + LoadError);
                return;
            }

            var errors = ScriptValidator.Validate(script, layout, database.Npcs);
            if (errors.Count > 0)
            {
                LoadError = "대본에 오류 " + errors.Count + "건 — " + errors[0];
                for (int i = 0; i < errors.Count; i++) Debug.LogError("[EavesdropController] " + errors[i]);
                return;
            }

            CaseId = script.caseId ?? string.Empty;

            // 이동 트랙 → 이동이 내는 소리. 트랙을 고치면 소리도 따라 고쳐진다.
            MovementTrackTable trackTable = GameDataLoader.LoadTracks(CaseId);
            Tracks = trackTable != null ? MovementTracks.FromTable(trackTable) : null;
            Positions = new SpeakerPositions(script, Tracks, layout);

            var timeline = new ScriptTimeline(script);
            Session = new ListeningSession(timeline, new AudibilityModel(layout), BuildEvents(script, layout));

            // 씬이 만들어질 때 PlayerRoomTracker가 이미 첫 방을 알렸을 수 있다. 지금 값을 직접 받아 둔다.
            var tracker = FindAnyObjectByType<Player.PlayerRoomTracker>();
            if (tracker != null) Session.MoveTo(tracker.CurrentRoom);

            if (autoPlay) Session.Transport.Play();
        }

        /// <summary>
        /// 이동에서 뽑은 소리 + 손으로 적은 소리. 문소리·발소리를 손으로 적지 않는 이유는
        /// 트랙과 반드시 어긋나기 때문이다(MovementEvents가 트랙에서 만든다).
        /// </summary>
        private EventTimeline BuildEvents(ScriptDefinition script, RoomLayout layout)
        {
            var all = new List<ScriptEvent>();
            if (Tracks != null)
                all.AddRange(MovementEvents.Derive(Tracks, layout, script.durationMs));

            EventTable authored = GameDataLoader.LoadEvents(CaseId);
            if (authored != null) all.AddRange(authored.events);

            if (all.Count == 0) return null;
            Debug.Log("[EavesdropController] 이동·사건 소리 " + all.Count + "개");
            return new EventTimeline(all);
        }

        private void OnEnable()
        {
            GameEvents.PlayerRoomChanged += OnPlayerRoomChanged;
        }

        private void OnDisable()
        {
            GameEvents.PlayerRoomChanged -= OnPlayerRoomChanged;
        }

        private void OnPlayerRoomChanged(string roomId)
        {
            if (Session != null) Session.MoveTo(roomId);
        }

        private void Update()
        {
            if (Session == null) return;

            int frame = Time.frameCount;
            bool listening = ModalState.AcceptsInput(GameMode.Explore, frame);

            if (listening) HandleTransportKeys();

            // 창이 열려 있는 동안에는 회차를 세워 둔다. 놓친 것은 플레이어의 선택이어야 한다.
            if (!listening) return;

            int deltaMs = Mathf.RoundToInt(Time.deltaTime * 1000f);
            if (deltaMs > 0) Session.Advance(deltaMs);
        }

        /// <summary>
        /// 회차를 영상처럼 다룬다. J 역재생 / L 정주행은 영상 편집기의 관습이다.
        /// 판정은 전부 공유 PlaybackTransport가 하고 여기서는 키만 옮긴다.
        /// </summary>
        private void HandleTransportKeys()
        {
            PlaybackTransport transport = Session.Transport;

            if (Input.GetKeyDown(KeyCode.Space)) transport.TogglePlay();
            if (Input.GetKeyDown(KeyCode.R)) Session.Restart();
            if (Input.GetKeyDown(KeyCode.J)) transport.Play(PlayDirection.Backward);
            if (Input.GetKeyDown(KeyCode.L)) transport.Play(PlayDirection.Forward);

            if (Input.GetKeyDown(KeyCode.Comma)) Session.SeekTo(transport.PositionMs - seekStepMs);
            if (Input.GetKeyDown(KeyCode.Period)) Session.SeekTo(transport.PositionMs + seekStepMs);

            if (Input.GetKeyDown(KeyCode.LeftBracket)) transport.SpeedPercent = StepSpeed(transport.SpeedPercent, -1);
            if (Input.GetKeyDown(KeyCode.RightBracket)) transport.SpeedPercent = StepSpeed(transport.SpeedPercent, 1);
        }

        /// <summary>배속 단계를 한 칸 옮긴다. 단계는 공유 로직이 들고 있다.</summary>
        private static int StepSpeed(int current, int direction)
        {
            int[] presets = PlaybackTransport.SpeedPresets;
            if (direction > 0)
            {
                for (int i = 0; i < presets.Length; i++) if (presets[i] > current) return presets[i];
                return presets[presets.Length - 1];
            }
            for (int i = presets.Length - 1; i >= 0; i--) if (presets[i] < current) return presets[i];
            return presets[0];
        }
    }
}
