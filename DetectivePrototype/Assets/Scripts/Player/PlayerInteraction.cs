using Detective.Core;
using UnityEngine;

namespace Detective.Player
{
    /// <summary>
    /// 주변에서 상호작용 가능한 대상을 찾아 두고, E 키로 실행한다.
    /// 대상 탐색은 매 프레임 반경 검사 한 번으로 끝낸다(프로토타입 규모에서 최적화 불필요).
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        public float interactionRadius = 1.6f;
        public KeyCode interactKey = KeyCode.E;

        /// <summary>지금 상호작용 대상. 없으면 null. HUD가 이 값을 읽어 안내문을 띄운다.</summary>
        public IInteractable Current { get; private set; }

        private void Update()
        {
            Current = FindClosestInteractable();

            if (Current == null) return;
            if (!Input.GetKeyDown(interactKey)) return;
            if (!Current.CanInteract) return;

            Current.Interact();
        }

        private IInteractable FindClosestInteractable()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRadius);

            IInteractable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                var candidate = hits[i].GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                if (!candidate.CanInteract) continue;

                Vector2 closest = hits[i].ClosestPoint(transform.position);
                float distance = ((Vector2)transform.position - closest).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
