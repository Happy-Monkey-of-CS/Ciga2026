using UnityEngine;
using UnityEngine.Events;

namespace Ciga.Demo
{
    /// <summary>
    /// Tracks how many collectibles have been picked up and fires an event
    /// (e.g. spawn the boss) when the target count is reached.
    /// Place one in the scene — it does NOT persist between scenes.
    /// </summary>
    public sealed class CollectionManager2D : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("How many collectibles must be picked up to trigger the event.")]
        [SerializeField] private int totalNeeded = 3;

        [Header("Boss Spawn")]
        [Tooltip("Prefab to spawn when all collectibles are gathered.")]
        [SerializeField] private GameObject bossPrefab;
        [Tooltip("Where the boss appears (uses this object's position if left empty).")]
        [SerializeField] private Transform bossSpawnPoint;

        [Header("Audio")]
        [SerializeField] private AudioClip allCollectedClip;

        [Header("Events")]
        [Tooltip("Fired each time a collectible is picked up. Receives current count.")]
        public UnityEvent<int> OnCountChanged;
        [Tooltip("Fired when all collectibles have been collected.")]
        public UnityEvent OnAllCollected;

        private static CollectionManager2D instance;
        private int collectedCount;
        private bool bossSpawned;

        public static CollectionManager2D Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<CollectionManager2D>();
                }
                return instance;
            }
        }

        public int CollectedCount => collectedCount;
        public int TotalNeeded => totalNeeded;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Called by Collectible2D when the player picks one up.</summary>
        public void OnCollected(Vector3 position)
        {
            collectedCount++;
            OnCountChanged?.Invoke(collectedCount);

            if (collectedCount >= totalNeeded && !bossSpawned)
            {
                bossSpawned = true;
                SpawnBoss();
            }
        }

        private void SpawnBoss()
        {
            if (bossPrefab == null)
            {
                Debug.LogWarning("[CollectionManager] Boss prefab not assigned.");
                return;
            }

            Vector3 spawnPos = bossSpawnPoint != null
                ? bossSpawnPoint.position
                : transform.position;

            Instantiate(bossPrefab, spawnPos, Quaternion.identity);

            // Sound
            if (allCollectedClip != null)
            {
                AudioManager2D audio = AudioManager2D.Instance;
                if (audio != null)
                {
                    audio.PlayOneShot(allCollectedClip);
                }
            }

            OnAllCollected?.Invoke();
            Debug.Log($"[CollectionManager] All {totalNeeded} collected — Boss spawned!");
        }

        private void OnValidate()
        {
            totalNeeded = Mathf.Max(1, totalNeeded);
        }
    }
}
