using Detective.Core;
using Detective.Data;
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

            Session = new ListeningSession(new ScriptTimeline(script), new AudibilityModel(layout));

            // 씬이 만들어질 때 PlayerRoomTracker가 이미 첫 방을 알렸을 수 있다. 지금 값을 직접 받아 둔다.
            var tracker = FindAnyObjectByType<Player.PlayerRoomTracker>();
            if (tracker != null) Session.MoveTo(tracker.CurrentRoom);

            if (autoPlay) Session.Transport.Play();
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

            if (listening)
            {
                if (Input.GetKeyDown(KeyCode.Space)) Session.Transport.TogglePlay();
                if (Input.GetKeyDown(KeyCode.R)) Session.Restart();
            }

            // 창이 열려 있는 동안에는 회차를 세워 둔다. 놓친 것은 플레이어의 선택이어야 한다.
            if (!listening) return;

            int deltaMs = Mathf.RoundToInt(Time.deltaTime * 1000f);
            if (deltaMs > 0) Session.Advance(deltaMs);
        }
    }
}
