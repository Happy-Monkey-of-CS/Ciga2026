using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Effects")]
        [Tooltip("Show a full-screen fade during teleport.")]
        [SerializeField] private bool useScreenFade = true;

        [Tooltip("Screen fade color and maximum opacity.")]
        [SerializeField] private Color fadeColor = new Color(0f, 0f, 0f, 0.85f);

        [Tooltip("Seconds spent fading the screen out before teleporting.")]
        [SerializeField] private float fadeOutDuration = 0.25f;

        [Tooltip("Seconds to hold the screen at full fade after the body has moved.")]
        [SerializeField] private float fadeHoldDuration = 0.12f;

        [Tooltip("Seconds spent fading the screen back in after teleporting.")]
        [SerializeField] private float fadeInDuration = 0.35f;

        [Tooltip("Fade the teleported object's SpriteRenderer along with the screen fade.")]
        [SerializeField] private bool fadeTeleportedSprite = true;

        [Tooltip("Seconds spent fading the teleported object's sprite out/in.")]
        [SerializeField] private float spriteFadeDuration = 0.22f;

        [Tooltip("Temporarily zero the Rigidbody2D velocity while the teleport effect is playing.")]
        [SerializeField] private bool freezeDuringEffect = true;

        [Tooltip("Spawn an expanding ring at the destination after teleporting.")]
        [SerializeField] private bool spawnArrivalRing = true;

        [Tooltip("Color and opacity of the destination arrival ring.")]
        [SerializeField] private Color arrivalRingColor = new Color(0.2f, 0.95f, 1f, 0.9f);

        [Tooltip("Seconds the destination arrival ring remains visible.")]
        [SerializeField] private float arrivalRingDuration = 0.65f;

        [Tooltip("Starting radius of the destination arrival ring.")]
        [SerializeField] private float arrivalRingStartRadius = 0.25f;

        [Tooltip("Ending radius of the destination arrival ring.")]
        [SerializeField] private float arrivalRingEndRadius = 1.1f;

        [Tooltip("Line width of the destination arrival ring.")]
        [SerializeField] private float arrivalRingWidth = 0.055f;

        private float lastTriggerTime = float.MinValue;
        private bool isTeleporting;

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

            cooldown = Mathf.Max(0f, cooldown);
            fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
            fadeHoldDuration = Mathf.Max(0f, fadeHoldDuration);
            fadeInDuration = Mathf.Max(0f, fadeInDuration);
            spriteFadeDuration = Mathf.Max(0f, spriteFadeDuration);
            arrivalRingDuration = Mathf.Max(0.01f, arrivalRingDuration);
            arrivalRingStartRadius = Mathf.Max(0.01f, arrivalRingStartRadius);
            arrivalRingEndRadius = Mathf.Max(arrivalRingStartRadius, arrivalRingEndRadius);
            arrivalRingWidth = Mathf.Max(0.001f, arrivalRingWidth);
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

                StartCoroutine(TeleportRoutine(other.attachedRigidbody));
            }
            else
            {
                StartCoroutine(TeleportRoutine(other.attachedRigidbody));
            }
        }

        private IEnumerator TeleportRoutine(Rigidbody2D body)
        {
            if (isTeleporting || body == null)
            {
                yield break;
            }

            isTeleporting = true;
            Vector2 destination = targetDestination != null
                ? (Vector2)targetDestination.position + worldOffset
                : worldOffset;

            Vector2 velocity = body.velocity;
            float angularVelocity = body.angularVelocity;
            SpriteRenderer spriteRenderer = body.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = body.GetComponentInChildren<SpriteRenderer>();
            }

            Color originalSpriteColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            Image fadeImage = useScreenFade ? CreateFadeImage() : null;

            if (freezeDuringEffect)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            yield return RunPreTeleportEffects(fadeImage, spriteRenderer, originalSpriteColor);

            body.position = destination;
            Physics2D.SyncTransforms();

            if (preserveVelocity)
            {
                body.velocity = velocity;
                body.angularVelocity = angularVelocity;
            }

            if (teleportClip != null)
            {
                AudioManager2D manager = AudioManager2D.Instance;
                if (manager != null)
                {
                    manager.PlayOneShotAt(teleportClip, destination);
                }
            }

            PlayerController2D player = body.GetComponent<PlayerController2D>();
            if (player == null) player = body.GetComponentInParent<PlayerController2D>();
            if (player != null)
            {
                player.OnTeleported();
            }

            if (spawnArrivalRing)
            {
                StartCoroutine(PlayArrivalRing(destination));
            }

            yield return RunPostTeleportEffects(fadeImage, spriteRenderer, originalSpriteColor);

            if (fadeImage != null)
            {
                Destroy(fadeImage.transform.root.gameObject);
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalSpriteColor;
            }

            isTeleporting = false;
        }

        private IEnumerator RunPreTeleportEffects(Image fadeImage, SpriteRenderer spriteRenderer, Color originalSpriteColor)
        {
            float duration = Mathf.Max(fadeOutDuration, spriteFadeDuration);
            if (duration <= 0f)
            {
                SetFadeAlpha(fadeImage, fadeColor.a);
                SetSpriteAlpha(spriteRenderer, originalSpriteColor, fadeTeleportedSprite ? 0f : originalSpriteColor.a);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                if (fadeImage != null)
                {
                    SetFadeAlpha(fadeImage, Mathf.Lerp(0f, fadeColor.a, Smooth(t, fadeOutDuration, duration)));
                }

                if (fadeTeleportedSprite && spriteRenderer != null)
                {
                    SetSpriteAlpha(spriteRenderer, originalSpriteColor, Mathf.Lerp(originalSpriteColor.a, 0f, Smooth(t, spriteFadeDuration, duration)));
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            SetFadeAlpha(fadeImage, fadeColor.a);
            if (fadeTeleportedSprite)
            {
                SetSpriteAlpha(spriteRenderer, originalSpriteColor, 0f);
            }

            if (fadeHoldDuration > 0f)
            {
                yield return new WaitForSeconds(fadeHoldDuration);
            }
        }

        private IEnumerator RunPostTeleportEffects(Image fadeImage, SpriteRenderer spriteRenderer, Color originalSpriteColor)
        {
            float duration = Mathf.Max(fadeInDuration, spriteFadeDuration);
            if (duration <= 0f)
            {
                SetFadeAlpha(fadeImage, 0f);
                SetSpriteAlpha(spriteRenderer, originalSpriteColor, originalSpriteColor.a);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                if (fadeImage != null)
                {
                    SetFadeAlpha(fadeImage, Mathf.Lerp(fadeColor.a, 0f, Smooth(t, fadeInDuration, duration)));
                }

                if (fadeTeleportedSprite && spriteRenderer != null)
                {
                    SetSpriteAlpha(spriteRenderer, originalSpriteColor, Mathf.Lerp(0f, originalSpriteColor.a, Smooth(t, spriteFadeDuration, duration)));
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            SetFadeAlpha(fadeImage, 0f);
            SetSpriteAlpha(spriteRenderer, originalSpriteColor, originalSpriteColor.a);
        }

        private static float Smooth(float normalizedTime, float phaseDuration, float totalDuration)
        {
            if (phaseDuration <= 0f)
            {
                return 1f;
            }

            float phaseT = Mathf.Clamp01(normalizedTime * totalDuration / phaseDuration);
            return phaseT * phaseT * (3f - 2f * phaseT);
        }

        private Image CreateFadeImage()
        {
            GameObject canvasObject = new GameObject("Teleport Fade Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject imageObject = new GameObject("Fade");
            imageObject.transform.SetParent(canvasObject.transform, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private void SetFadeAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            image.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, Mathf.Clamp01(alpha));
        }

        private static void SetSpriteAlpha(SpriteRenderer spriteRenderer, Color baseColor, float alpha)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(alpha));
        }

        private IEnumerator PlayArrivalRing(Vector2 position)
        {
            GameObject ringObject = new GameObject("Teleport Arrival Ring");
            ringObject.transform.position = new Vector3(position.x, position.y, 0f);
            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 64;
            line.startWidth = arrivalRingWidth;
            line.endWidth = arrivalRingWidth;
            line.numCapVertices = 0;
            line.alignment = LineAlignment.View;
            line.sortingOrder = 40;
            line.material = new Material(Shader.Find("Sprites/Default"));

            float duration = Mathf.Max(0.01f, arrivalRingDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float radius = Mathf.Lerp(arrivalRingStartRadius, arrivalRingEndRadius, eased);
                Color color = arrivalRingColor;
                color.a *= 1f - eased;
                line.startColor = color;
                line.endColor = color;
                SetRingPositions(line, radius);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(ringObject);
        }

        private static void SetRingPositions(LineRenderer line, float radius)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
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
