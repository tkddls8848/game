using UnityEngine;

namespace Detective.Core
{
    /// <summary>플레이어를 부드럽게 따라가는 2D 카메라. z는 그대로 유지한다.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;

        [Tooltip("클수록 천천히 따라온다.")]
        public float smoothTime = 0.15f;

        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }
    }
}
