using UnityEngine;

namespace Ciga.Demo
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(2f, 1.2f, -10f);
        [SerializeField] private float smoothTime = 0.18f;
        [SerializeField] private float snapDistance = 8f;
        [SerializeField] private float minY = 0f;

        private Vector3 velocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = target.position + offset;
            desired.y = Mathf.Max(desired.y, minY);
            desired.z = offset.z;

            if (Vector2.Distance(transform.position, desired) > snapDistance)
            {
                velocity = Vector3.zero;
                transform.position = desired;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }

        private void OnValidate()
        {
            smoothTime = Mathf.Max(0f, smoothTime);
            snapDistance = Mathf.Max(0.1f, snapDistance);
        }
    }
}
