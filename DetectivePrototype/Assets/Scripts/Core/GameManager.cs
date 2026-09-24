using System.Collections.Generic;
using Detective.Data;
using Detective.Investigation;
using Detective.NPC;
using UnityEngine;

namespace Detective.Core
{
    /// <summary>
    /// 씬에 하나 존재하는 부팅 지점. 게임 데이터를 읽어 CaseDatabase를 만들어 두고,
    /// 다른 컴포넌트가 방·인물 정보를 물어볼 수 있게 한다.
    /// 매니저끼리의 직접 참조는 여기까지만 허용하고, 그 외 통신은 GameEvents를 쓴다(§18-5).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Tooltip("cases/ 폴더의 사건 파일 이름(확장자 제외).")]
        public string caseId = GameDataLoader.DefaultCaseId;

        /// <summary>사건 데이터 전체. Awake 이후에 유효하다.</summary>
        public CaseDatabase Database { get; private set; }

        /// <summary>rooms.json에서 만들어진 맵 레이아웃. Awake 이후에 유효하다.</summary>
        public RoomLayout Layout { get { return Database != null ? Database.Layout : null; } }

        /// <summary>이번 판의 수사 진행 상태. Awake 이후에 유효하다.</summary>
        public InvestigationState State { get; private set; }

        /// <summary>
        /// 타임라인 관찰 화면이 인물 위치를 묻는 곳. 지금까지 모은 기록으로 매번 새로 복원한다(기록 수십 건이라 싸다).
        /// </summary>
        public ITimelinePlacementSource TimelineSource
        {
            get
            {
                if (ShowTruthForDebug) return _truth;
                return State != null ? TimelineBoard.Build(State) : null;
            }
        }

        /// <summary>개발 확인용: 실제 스케줄을 그대로 보여 준다(에디터에서 타임라인 관찰 중 F9).</summary>
        public bool ShowTruthForDebug { get; set; }

        private readonly ScheduleTruthSource _truth = new ScheduleTruthSource();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ModalState.Reset();

            RoomTable table = GameDataLoader.LoadRoomTable();

            // 데이터가 깨진 채로 조용히 굴러가지 않도록 런타임에서도 한 번 검사한다.
            var errors = RoomLayoutValidator.Validate(table);
            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError("[rooms.json] " + errors[i]);
            }

            Database = GameDataLoader.BuildDatabase(table, caseId);

            List<string> dataErrors = GameDataValidator.Validate(Database);
            for (int i = 0; i < dataErrors.Count; i++) Debug.LogError("[GameData] " + dataErrors[i]);

            State = new InvestigationState(Database);
            Debug.Log("[GameManager] 방 " + Layout.RoomCount + "개, 인물 " + Database.Npcs.All.Count + "명, 단서 "
                + Database.Evidence.All.Count + "개 로드 완료.");
        }

        private void OnEnable()
        {
            GameEvents.EvidenceCollected += OnEvidenceCollected;
        }

        private void OnDisable()
        {
            GameEvents.EvidenceCollected -= OnEvidenceCollected;
        }

        private void OnEvidenceCollected(string evidenceId)
        {
            if (State == null) return;

            EvidenceDefinition evidence;
            if (!Database.Evidence.TryGet(evidenceId, out evidence))
            {
                Debug.LogWarning("[GameManager] evidence.json에 없는 단서: " + evidenceId);
                return;
            }
            if (State.CollectEvidence(evidenceId)) GameEvents.RaiseNotebookUpdated();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
