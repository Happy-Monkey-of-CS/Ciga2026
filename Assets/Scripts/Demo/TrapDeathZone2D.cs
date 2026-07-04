using UnityEngine;

namespace Ciga.Demo
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TrapDeathZone2D : MonoBehaviour
    {
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
                player.Kill();
            }

            Enemy2D enemy = other.GetComponent<Enemy2D>();
            if (enemy == null)
            {
                enemy = other.GetComponentInParent<Enemy2D>();
            }

            if (enemy != null)
            {
                enemy.Defeat();
            }
        }
    }
}
