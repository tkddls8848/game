using Detective.Core;
using UnityEngine;

namespace Detective.Player
{
    /// <summary>
    /// WASD/화살표 이동. 레거시 Input 클래스를 쓴다(Input System 패키지는 프로토타입 범위 밖).
    /// 물리 이동은 FixedUpdate에서 MovePosition으로 처리해 벽 충돌이 자연스럽게 걸리도록 한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 5f;

        private Rigidbody2D _body;
        private Vector2 _input;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private void Update()
        {
            // 대화창·노트·타임라인이 열려 있으면 제자리에 선다.
            if (!ModalState.IsExploring)
            {
                _input = Vector2.zero;
                return;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            _input = new Vector2(h, v);
            if (_input.sqrMagnitude > 1f) _input = _input.normalized;
        }

        private void FixedUpdate()
        {
            if (_input == Vector2.zero) return;
            _body.MovePosition(_body.position + _input * (moveSpeed * Time.fixedDeltaTime));
        }
    }
}
