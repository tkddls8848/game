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

        /// <summary>테스트/씬 재시작용. 남아 있는 구독을 전부 끊는다.</summary>
        public static void ClearAllSubscribers()
        {
            MessageShown = null;
            EvidenceCollected = null;
            TalkRequested = null;
            NotebookUpdated = null;
        }
    }
}
