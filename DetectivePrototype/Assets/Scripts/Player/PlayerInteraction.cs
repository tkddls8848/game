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

        private IHighlightable _highlighted;

        private void Update()
        {
            // 창이 열려 있는 동안에는 대상도 잡지 않는다(안내문·하이라이트가 창 뒤에 남지 않게).
            Current = ModalState.IsExploring ? FindClosestInteractable() : null;
            UpdateHighlight();

            if (Current == null) return;
            if (!Input.GetKeyDown(interactKey)) return;
            if (!Current.CanInteract) return;
            if (!ModalState.AcceptsInput(GameMode.Explore, Time.frameCount)) return;

            Current.Interact();
        }

        private void UpdateHighlight()
        {
            IHighlightable next = Current as IHighlightable;
            if (ReferenceEquals(next, _highlighted)) return;

            // 이미 파괴된 컴포넌트는 C# 참조는 남아 있어도 Unity 쪽 == null이 참이다. 그런 대상은 건드리지 않는다.
            var previousObject = _highlighted as Object;
            bool destroyed = !ReferenceEquals(previousObject, null) && previousObject == null;
            if (_highlighted != null && !destroyed) _highlighted.SetHighlighted(false);

            _highlighted = next;
            if (_highlighted != null) _highlighted.SetHighlighted(true);
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
