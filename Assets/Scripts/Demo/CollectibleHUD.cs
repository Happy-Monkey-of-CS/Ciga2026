using UnityEngine;
using UnityEngine.UI;

namespace Ciga.Demo
{
    /// <summary>
    /// Canvas-based HUD that displays the collectible progress (e.g. "2 / 5")
    /// in the top-right corner of the screen. Subscribes to CollectionManager2D
    /// events automatically. Can be shown/hidden via UnityEvents or script calls.
    /// </summary>
    public sealed class CollectibleHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The UI Text element that shows the counter (e.g. '2 / 3').")]
        [SerializeField] private Text counterText;

        [Tooltip("The UI Image that shows the collectible icon.")]
        [SerializeField] private Image iconImage;

        [Tooltip("Optional root GameObject toggled by Show/Hide. Falls back to this GameObject.")]
        [SerializeField] private GameObject panelRoot;

        [Header("Format")]
        [Tooltip("Format string for the counter. {0} = current, {1} = total.")]
        [SerializeField] private string counterFormat = "{0} / {1}";

        [Tooltip("If true, hides the counter when count is 0.")]
        [SerializeField] private bool hideWhenZero = false;

        [Tooltip("Pulse scale animation when a collectible is picked up.")]
        [SerializeField] private bool animateOnCollect = true;

        [Tooltip("Duration of the pickup pulse animation in seconds.")]
        [SerializeField] private float pulseDuration = 0.25f;

        [Tooltip("Scale multiplier during the pulse peak.")]
        [SerializeField] private float pulseScale = 1.35f;

        [Header("Audio")]
        [Tooltip("Optional sound played when the HUD updates (on top of the collectible's own sound).")]
        [SerializeField] private AudioClip updateClip;

        private CollectionManager2D manager;
        private Vector3 counterOriginalScale;
        private float pulseTimer;
        private bool isPulsing;
        private bool initialized;

        public bool IsVisible
        {
            get
            {
                GameObject target = panelRoot != null ? panelRoot : gameObject;
                return target.activeSelf;
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (initialized)
            {
                // Re-hookup in case manager changed (scene reload etc.)
                HookManager();
                RefreshDisplay();
            }
        }

        private void OnDisable()
        {
            UnhookManager();
        }

        private void Update()
        {
            if (!isPulsing) return;

            pulseTimer += Time.unscaledDeltaTime;
            float t = pulseTimer / pulseDuration;

            if (t >= 1f)
            {
                counterText.rectTransform.localScale = counterOriginalScale;
                isPulsing = false;
                return;
            }

            // Simple ease-out pulse: scale up quickly then settle back
            float scale = 1f + (pulseScale - 1f) * (1f - t) * Mathf.Sin(t * Mathf.PI);
            counterText.rectTransform.localScale = counterOriginalScale * scale;
        }

        private void Initialize()
        {
            if (initialized) return;

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (counterText != null)
            {
                counterOriginalScale = counterText.rectTransform.localScale;
            }

            HookManager();
            RefreshDisplay();
            initialized = true;
        }

        private void HookManager()
        {
            UnhookManager();

            manager = CollectionManager2D.Instance;
            if (manager != null)
            {
                manager.OnCountChanged.AddListener(OnCountChanged);
                manager.OnAllCollected.AddListener(OnAllCollected);
            }
        }

        private void UnhookManager()
        {
            if (manager != null)
            {
                manager.OnCountChanged.RemoveListener(OnCountChanged);
                manager.OnAllCollected.RemoveListener(OnAllCollected);
                manager = null;
            }
        }

        private void OnCountChanged(int currentCount)
        {
            RefreshDisplay();

            if (animateOnCollect && currentCount > 0 && isActiveAndEnabled)
            {
                TriggerPulse();
            }

            if (updateClip != null)
            {
                AudioManager2D audio = AudioManager2D.Instance;
                if (audio != null)
                {
                    audio.PlayOneShot(updateClip);
                }
            }
        }

        private void OnAllCollected()
        {
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (counterText == null) return;

            if (manager == null)
            {
                counterText.text = string.Format(counterFormat, 0, 0);
                return;
            }

            counterText.text = string.Format(counterFormat, manager.CollectedCount, manager.TotalNeeded);

            if (hideWhenZero)
            {
                SetVisibleInternal(manager.CollectedCount > 0);
            }
        }

        private void TriggerPulse()
        {
            if (counterText == null) return;

            pulseTimer = 0f;
            isPulsing = true;
            counterText.rectTransform.localScale = counterOriginalScale * pulseScale;
        }

        /// <summary>
        /// Show the HUD. Can be called from UnityEvents.
        /// </summary>
        public void Show()
        {
            SetVisibleInternal(true);
        }

        /// <summary>
        /// Hide the HUD. Can be called from UnityEvents.
        /// </summary>
        public void Hide()
        {
            SetVisibleInternal(false);
        }

        /// <summary>
        /// Set the HUD visibility. Can be called from UnityEvents.
        /// </summary>
        public void SetVisible(bool visible)
        {
            SetVisibleInternal(visible);
        }

        /// <summary>
        /// Toggle the HUD visibility. Can be called from UnityEvents.
        /// </summary>
        public void Toggle()
        {
            SetVisibleInternal(!IsVisible);
        }

        private void SetVisibleInternal(bool visible)
        {
            GameObject target = panelRoot != null ? panelRoot : gameObject;
            if (target.activeSelf == visible) return;
            target.SetActive(visible);
        }

        private void OnValidate()
        {
            pulseDuration = Mathf.Max(0.01f, pulseDuration);
            pulseScale = Mathf.Max(1f, pulseScale);
        }
    }
}
