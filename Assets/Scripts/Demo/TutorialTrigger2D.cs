using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.Demo
{
    /// <summary>
    /// Place in the scene as a trigger volume. When the player enters,
    /// shows a speech-bubble tutorial. One-shot (triggers once, then self-destructs).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TutorialTrigger2D : MonoBehaviour
    {
        [Header("Bubble Text (one line per entry, blank = pause)")]
        [TextArea(2, 6)]
        [SerializeField] private string[] bubbleLines = { "这里是教程文字。", "按任意键继续……" };

        [Header("Settings")]
        [SerializeField] private bool destroyAfterUse = true;
        [SerializeField] private bool pauseGame = true;

        [Header("Visual (optional)")]
        [SerializeField] private SpriteRenderer indicatorRenderer;

        private bool triggered;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnValidate()
        {
            var col = GetComponent<Collider2D>();
            if (!col.isTrigger) col.isTrigger = true;
        }

        private void Start()
        {
            if (indicatorRenderer != null) indicatorRenderer.enabled = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggered) return;

            PlayerController2D player = other.GetComponent<PlayerController2D>();
            if (player == null) player = other.GetComponentInParent<PlayerController2D>();
            if (player == null) return;

            triggered = true;
            if (indicatorRenderer != null) indicatorRenderer.enabled = false;

            StartCoroutine(RunTutorial(player));
        }

        private IEnumerator RunTutorial(PlayerController2D player)
        {
            var cutscene = CutsceneManager2D.Instance;
            if (cutscene == null || cutscene.IsPlaying)
            {
                // Cutscene system not ready or busy — just skip
                if (destroyAfterUse) Destroy(gameObject);
                yield break;
            }

            if (pauseGame)
            {
                Time.timeScale = 0f;
                player.StopAllAudioLoops();
            }

            yield return cutscene.ShowGameplayBubble(bubbleLines);

            if (pauseGame) Time.timeScale = 1f;

            if (destroyAfterUse) Destroy(gameObject);
        }
    }
}
