using Detective.Core;
using UnityEngine;

namespace Detective.Investigation
{
    /// <summary>
    /// E로 조사하면 설명을 띄우는 오브젝트.
    /// Phase 1에서는 상호작용 파이프라인 확인용이고, Phase 3에서 evidence.json이
    /// evidenceId를 채워 주면 그대로 단서 획득으로 이어진다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InspectableObject : MonoBehaviour, IInteractable
    {
        [Tooltip("안내문에 표시할 이름.")]
        public string displayName = "물건";

        [TextArea(2, 5)]
        public string description = "특별한 것은 없다.";

        [Tooltip("비워 두면 단서를 주지 않는다. Phase 3에서 evidence.json의 id를 넣는다.")]
        public string evidenceId = "";

        [Tooltip("이미 조사한 뒤에도 다시 조사할 수 있는가.")]
        public bool repeatable = true;

        /// <summary>한 번이라도 조사했는가. 런타임 상태이므로 데이터 파일에는 남기지 않는다.</summary>
        public bool Inspected { get; private set; }

        public string InteractionPrompt
        {
            get { return Inspected ? "[E] " + displayName + " 다시 조사" : "[E] " + displayName + " 조사"; }
        }

        public bool CanInteract
        {
            get { return repeatable || !Inspected; }
        }

        public void Interact()
        {
            bool first = !Inspected;
            Inspected = true;

            GameEvents.ShowMessage(displayName + " — " + description);

            if (first && !string.IsNullOrEmpty(evidenceId))
            {
                GameEvents.RaiseEvidenceCollected(evidenceId);
            }
        }
    }
}
