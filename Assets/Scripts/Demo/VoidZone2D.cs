using UnityEngine;

namespace Ciga.Demo
{
    /// <summary>
    /// Attach to a GameObject with a trigger Collider2D placed in void areas
    /// (below the map, behind the scrolling view, etc.).
    /// When the player enters, they are teleported back to their last safe position.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class VoidZone2D : MonoBehaviour
    {
        [Header("Teleport")]
        [Tooltip("If true, resets the player's velocity after teleporting back.")]
        [SerializeField] private bool resetVelocity = true;

        [Tooltip("Cooldown in seconds before this zone can trigger again.")]
        [SerializeField] private float cooldown = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip triggerClip;

        private float lastTriggerTime = float.MinValue;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnValidate()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.attachedRigidbody) return;

            // cooldown guard
            float now = Time.time;
            if (now - lastTriggerTime < cooldown) return;

            PlayerController2D player = other.GetComponent<PlayerController2D>();
            if (player == null) player = other.GetComponentInParent<PlayerController2D>();
            if (player == null) return;

            lastTriggerTime = now;

            // Teleport player back to last safe position
            bool success = player.TeleportToLastSafePosition();

            if (success && triggerClip != null)
            {
                AudioManager2D manager = AudioManager2D.Instance;
                if (manager != null)
                {
                    manager.PlayOneShotAt(triggerClip, player.transform.position);
                }
            }
        }
    }
}
