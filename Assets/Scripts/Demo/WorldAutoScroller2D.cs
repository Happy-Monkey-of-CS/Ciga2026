using UnityEngine;

namespace Ciga.Demo
{
    public sealed class WorldAutoScroller2D : MonoBehaviour
    {
        [Tooltip("How fast this object moves left in world units per second.")]
        [SerializeField] private float scrollSpeed = 3f;
        [Tooltip("When set, scrolling is driven by this target's actual positive X movement instead of raw time.")]
        [SerializeField] private Transform scrollDriver;
        [SerializeField] private float referenceDriverSpeed = 4f;

        private float lastDriverX;
        private bool hasDriverPosition;

        public void SetScrollDriver(Transform driver, float referenceSpeed)
        {
            scrollDriver = driver;
            referenceDriverSpeed = Mathf.Max(0.01f, referenceSpeed);
            ResetDriverPosition();
        }

        private void OnEnable()
        {
            ResetDriverPosition();
        }

        private void Update()
        {
            float movement = GetScrollMovement();
            if (movement > 0f)
            {
                transform.position += Vector3.left * movement;
            }
        }

        private void OnValidate()
        {
            scrollSpeed = Mathf.Max(0f, scrollSpeed);
            referenceDriverSpeed = Mathf.Max(0.01f, referenceDriverSpeed);
        }

        private float GetScrollMovement()
        {
            if (scrollDriver == null)
            {
                return scrollSpeed * Time.deltaTime;
            }

            float currentX = scrollDriver.position.x;
            if (!hasDriverPosition)
            {
                lastDriverX = currentX;
                hasDriverPosition = true;
                return 0f;
            }

            float driverDelta = Mathf.Max(0f, currentX - lastDriverX);
            lastDriverX = currentX;
            return driverDelta * (scrollSpeed / referenceDriverSpeed);
        }

        private void ResetDriverPosition()
        {
            hasDriverPosition = false;
            if (scrollDriver != null)
            {
                lastDriverX = scrollDriver.position.x;
            }
        }
    }
}
