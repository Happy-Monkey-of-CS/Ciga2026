using UnityEngine;

namespace Ciga.Demo
{
    /// <summary>
    /// Attach to a GameObject with a trigger Collider2D.
    /// When a matching Rigidbody2D enters the trigger, it is teleported
    /// to the target position (Transform or a fixed world offset).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TeleportZone2D : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The Transform whose position will be used as the destination. " +
                 "If left empty, uses the worldOffset field below.")]
        [SerializeField] private Transform targetDestination;

        [Tooltip("Fixed world offset applied on top of the target destination " +
                 "(or used as the absolute destination when no Transform is set).")]
        [SerializeField] private Vector2 worldOffset;

        [Header("Filter")]
        [Tooltip("Only teleport objects with this tag. Leave empty to accept any tag.")]
        [SerializeField] private string requiredTag = "";

        [Tooltip("When true, only teleports objects that carry a PlayerController2D component.")]
        [SerializeField] private bool onlyPlayer = true;

        [Header("Behaviour")]
        [Tooltip("Keep the entering object's velocity after teleporting.")]
        [SerializeField] private bool preserveVelocity = true;

        [Tooltip("Cooldown in seconds before this zone can trigger again. " +
                 "Prevents rapid re-triggering when destination overlaps another zone.")]
        [SerializeField] private float cooldown = 0.3f;

        [Header("Audio")]
        [SerializeField] private AudioClip teleportClip;

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
            lastTriggerTime = now;

            // tag filter
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

            // player-only filter
            if (onlyPlayer)
            {
                PlayerController2D player = other.GetComponent<PlayerController2D>();
                if (player == null) player = other.GetComponentInParent<PlayerController2D>();
                if (player == null) return;

                TeleportRigidbody(other.attachedRigidbody);
            }
            else
            {
                TeleportRigidbody(other.attachedRigidbody);
            }
        }

        private void TeleportRigidbody(Rigidbody2D body)
        {
            Vector2 destination = targetDestination != null
                ? (Vector2)targetDestination.position + worldOffset
                : worldOffset;

            Vector2 velocity = body.velocity;

            body.position = destination;
            Physics2D.SyncTransforms();

            if (preserveVelocity)
            {
                body.velocity = velocity;
            }

            // audio
            if (teleportClip != null)
            {
                AudioManager2D manager = AudioManager2D.Instance;
                if (manager != null)
                {
                    manager.PlayOneShotAt(teleportClip, destination);
                }
            }

            // also stop any grapple/aim state on the player
            PlayerController2D player = body.GetComponent<PlayerController2D>();
            if (player == null) player = body.GetComponentInParent<PlayerController2D>();
            if (player != null)
            {
                // notify the player of teleport (handled by public method)
                player.OnTeleported();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Vector3 dest = targetDestination != null
                ? targetDestination.position + (Vector3)worldOffset
                : (Vector3)worldOffset;

            // draw line from zone to destination
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
            Gizmos.DrawLine(transform.position, dest);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(dest, 0.35f);
            Gizmos.DrawSphere(dest, 0.12f);

            // label
            UnityEditor.Handles.Label(dest + Vector3.up * 0.5f, "Teleport Dest", new GUIStyle
            {
                normal = new GUIStyleState { textColor = Color.cyan },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
            });
        }
#endif
    }
}
