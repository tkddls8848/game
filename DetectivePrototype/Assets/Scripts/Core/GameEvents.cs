using System;

namespace Detective.Core
{
    /// <summary>
    /// 매니저끼리 직접 참조하지 않기 위한 최소한의 이벤트 통로(§18-5).
    /// UnityEngine에 의존하지 않으므로 테스트에서도 그대로 쓸 수 있다.
    /// 씬을 다시 로드하면 구독이 남아 있을 수 있으므로 구독자는 OnDisable에서 반드시 해제한다.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>화면에 잠깐 띄울 메시지(조사 결과, 안내문 등).</summary>
        public static event Action<string> MessageShown;

        /// <summary>단서를 새로 획득했을 때. 인자는 evidence id.</summary>
        public static event Action<string> EvidenceCollected;

        /// <summary>플레이어가 인물에게 말을 걸었을 때. 인자는 npc id.</summary>
        public static event Action<string> TalkRequested;

        /// <summary>수사 노트에 새 정보가 들어갔을 때(단서·증언·목격).</summary>
        public static event Action NotebookUpdated;

        /// <summary>효과음 요청. 인자는 종류: "inspect" / "page" / "chime". 소리 담당이 없으면 조용히 무시된다.</summary>
        public static event Action<string> SfxRequested;

        /// <summary>타임라인 관찰 모드에서 보고 있는 시각이 바뀌었을 때. 인자는 틱.</summary>
        public static event Action<int> TimelineTickChanged;

        /// <summary>
        /// 청취점이 다른 방으로 옮겨 갔을 때. 인자는 방 id, 어느 방에도 속하지 않으면 빈 문자열.
        /// 방 환경음 교체(AudioDirector)와 엿듣기 가청 판정(EavesdropController)이 이걸 듣는다.
        /// 둘이 서로를 참조하지 않도록 이벤트로 흘린다(§18-5).
        /// </summary>
        public static event Action<string> PlayerRoomChanged;

        public static void ShowMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Action<string> handler = MessageShown;
            if (handler != null) handler(message);
        }

        public static void RaiseEvidenceCollected(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId)) return;
            Action<string> handler = EvidenceCollected;
            if (handler != null) handler(evidenceId);
        }

        public static void RequestTalk(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            Action<string> handler = TalkRequested;
            if (handler != null) handler(npcId);
        }

        public static void RaiseNotebookUpdated()
        {
            Action handler = NotebookUpdated;
            if (handler != null) handler();
        }

        public static void RequestSfx(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return;
            Action<string> handler = SfxRequested;
            if (handler != null) handler(kind);
        }

        public static void RaiseTimelineTickChanged(int tick)
        {
            Action<int> handler = TimelineTickChanged;
            if (handler != null) handler(tick);
        }

        /// <summary>방을 벗어나 어디에도 속하지 않을 수 있으므로 빈 문자열도 그대로 흘린다.</summary>
        public static void RaisePlayerRoomChanged(string roomId)
        {
            Action<string> handler = PlayerRoomChanged;
            if (handler != null) handler(roomId ?? string.Empty);
        }

        /// <summary>테스트/씬 재시작용. 남아 있는 구독을 전부 끊는다.</summary>
        public static void ClearAllSubscribers()
        {
            MessageShown = null;
            EvidenceCollected = null;
            TalkRequested = null;
            NotebookUpdated = null;
            SfxRequested = null;
            TimelineTickChanged = null;
            PlayerRoomChanged = null;
        }
    }
}
