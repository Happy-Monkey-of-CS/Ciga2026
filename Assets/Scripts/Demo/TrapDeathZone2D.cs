using UnityEngine;

namespace Ciga.Demo
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TrapDeathZone2D : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioClip triggerClip;

        private bool hasTriggered;

        private void OnEnable()
        {
            hasTriggered = false;
        }

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController2D player = other.GetComponent<PlayerController2D>();
            if (player == null)
            {
                player = other.GetComponentInParent<PlayerController2D>();
            }

            if (player != null)
            {
                TriggerTrap();
                player.Kill();
                return;
            }

            Enemy2D enemy = other.GetComponent<Enemy2D>();
            if (enemy == null)
            {
                enemy = other.GetComponentInParent<Enemy2D>();
            }

            if (enemy != null)
            {
                TriggerTrap();
                enemy.Defeat();
                return;
            }

            BossController2D boss = other.GetComponent<BossController2D>();
            if (boss == null)
            {
                boss = other.GetComponentInParent<BossController2D>();
            }

            if (boss != null)
            {
                TriggerTrap();
                boss.TakeTrapDamageFrom(GetComponent<Collider2D>());
            }
        }

        private void TriggerTrap()
        {
            if (hasTriggered) return;
            hasTriggered = true;

            if (triggerClip != null)
            {
                AudioManager2D manager = AudioManager2D.Instance;
                if (manager != null)
                {
                    manager.PlayOneShotAt(triggerClip, transform.position);
                }
            }
        }
    }
}
