using UnityEngine;

namespace Ciga.Demo
{
    /// <summary>
    /// Attach to a trigger collider. When the player enters, this item is collected,
    /// increments the CollectionManager, and destroys itself.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Collectible2D : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Optional effect prefab spawned when collected.")]
        [SerializeField] private GameObject collectEffect;

        [Header("Audio")]
        [SerializeField] private AudioClip collectClip;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other.GetComponent<PlayerController2D>();
            if (player == null) player = other.GetComponentInParent<PlayerController2D>();
            if (player == null) return;

            Collect();
        }

        private void Collect()
        {
            // Notify the manager
            CollectionManager2D manager = CollectionManager2D.Instance;
            if (manager != null)
            {
                manager.OnCollected(transform.position);
            }

            // Visual effect
            if (collectEffect != null)
            {
                Instantiate(collectEffect, transform.position, Quaternion.identity);
            }

            // Sound
            if (collectClip != null)
            {
                AudioManager2D audio = AudioManager2D.Instance;
                if (audio != null)
                {
                    audio.PlayOneShotAt(collectClip, transform.position);
                }
            }

            Destroy(gameObject);
        }
    }
}
