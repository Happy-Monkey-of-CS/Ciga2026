using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ciga.Demo
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        private const string GroundTag = "DemoGround";
        private const string GrappleStepTag = "GrappleStep";
        private const string GrappleWallTag = "GrappleWall";
        private const string EnemyTag = "Enemy";
        private const float PulledObjectSkinWidth = 0.01f;

        [Header("Movement")]
        [SerializeField] private float autoRunSpeed = 4f;
        [SerializeField] private float jumpForce = 13f;
        [SerializeField] private bool wrapAtMapEdges = true;
        [SerializeField] private float wrapLeftX = -9.5f;
        [SerializeField] private float wrapRightX = 21f;
        [SerializeField] private float groundCheckDistance = 0.08f;
        [SerializeField] private float wallCheckDistance = 0.08f;
        [SerializeField] private float groundNormalThreshold = 0.65f;
        [Tooltip("Multiplier applied to gravity while sliding down a wall. Lower values fall more slowly.")]
        [SerializeField, Range(0.05f, 1f)] private float wallSlideFallSpeedMultiplier = 0.35f;
        [Tooltip("Maximum downward speed while sliding on a wall.")]
        [SerializeField] private float wallSlideMaxFallSpeed = 2.5f;
        [Tooltip("Horizontal speed applied away from the wall when jumping during wall slide.")]
        [SerializeField] private float wallJumpHorizontalSpeed = 8f;
        [Tooltip("Vertical speed applied when jumping during wall slide.")]
        [SerializeField] private float wallJumpVerticalSpeed = 13f;
        [SerializeField] private float attackComboResetTime = 1f;
        [Header("Combat")]
        [SerializeField] private Vector2 attackHitOffset = new Vector2(0.75f, 0.55f);
        [SerializeField] private Vector2 attackHitSize = new Vector2(1.2f, 1f);
        [Header("Grapple")]
        [SerializeField] private float grappleAimRadius = 5f;
        [SerializeField, Range(0.01f, 1f)] private float grappleAimMoveSpeedMultiplier = 0.15f;
        [SerializeField] private float grapplePullSpeed = 14f;
        [Tooltip("Speed used when the grapple pulls a target object toward the player while auto-run is blocked.")]
        [SerializeField] private float grappleObjectPullSpeed = 8f;
        [SerializeField] private float grappleStopDistance = 0.65f;
        [SerializeField] private float grappleClimbAnimationDuration = 0.45f;
        [Tooltip("Forward ray distance used to decide whether auto-run is currently blocked by an obstacle.")]
        [SerializeField] private float runBlockedCheckDistance = 0.12f;
        [SerializeField] private int grappleAimCircleSegments = 72;
        [SerializeField] private Color grappleAimCircleColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] private Color grappleAimRayColor = new Color(0.35f, 0.85f, 1f, 0.9f);
        [SerializeField] private Color grappleHighlightColor = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private Sprite grappleAimSprite;
        [Tooltip("Optional material used by the fallback aim preview line.")]
        [SerializeField] private Material grappleAimRayMaterial;
        [Tooltip("Sprite image used to build the rope visually. Mermaid/Chain.png should be assigned here.")]
        [SerializeField] private Sprite grappleRopeSprite;
        [Tooltip("Width used by the chain during aim preview and after the grapple is fired.")]
        [SerializeField, Min(0.001f)] private float grappleRopeWidth = 0.12f;
        [Tooltip("World-space length of each repeated rope image segment.")]
        [SerializeField, Min(0.01f)] private float grappleRopeSegmentLength = 0.18f;
        [Tooltip("Local offset from the player's collider center where the rope starts. X follows facing direction; Y moves the origin up/down.")]
        [SerializeField] private Vector2 grappleOriginOffset = new Vector2(0.15f, -0.35f);
        [Header("Strike")]
        [SerializeField] private float strikeAimRadius = 2f;
        [SerializeField] private float strikeObjectSpeed = 12f;
        [SerializeField] private Sprite strikeAimSprite;
        [Header("Visual")]
        [Tooltip("Anchor icon shown at the aim target point during grapple/strike aim.")]
        [SerializeField] private Sprite anchorSprite;
        [Tooltip("Scale multiplier for the anchor indicator. Tune until it visually matches the chain.")]
        [SerializeField] private float anchorScale = 0.5f;
        [Header("Death")]
        [SerializeField] private bool restartOnDeath = true;
        [SerializeField] private float deathRestartDelay = 1.5f;
        [Header("Audio")]
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip landClip;
        [SerializeField] private AudioClip attackClip;
        [SerializeField] private AudioClip attackHitClip;
        [SerializeField] private AudioClip deathClip;
        [SerializeField] private AudioClip footstepsLoopClip;
        [SerializeField] private AudioClip wallSlideLoopClip;
        [SerializeField] private AudioClip wallJumpClip;
        [SerializeField] private AudioClip grappleAimStartClip;
        [SerializeField] private AudioClip grappleFireClip;
        [SerializeField] private AudioClip grappleConnectClip;
        [SerializeField] private AudioClip grapplePullSelfLoopClip;
        [SerializeField] private AudioClip grapplePullObjectLoopClip;
        [SerializeField] private AudioClip grappleLandClip;
        [SerializeField] private AudioClip grappleClimbClip;
        [SerializeField] private AudioClip strikeAimStartClip;
        [SerializeField] private AudioClip strikeFireClip;
        [SerializeField] private AudioClip strikeObjectLoopClip;
        [SerializeField] private AudioClip strikeImpactClip;
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField] private LayerMask grappleMask = 1;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private LineRenderer grappleLine;
        private LineRenderer grappleAimCircleLine;
        private LineRenderer grappleAimRayLine;
        private float timeSinceAttack;
        private float defaultGravityScale;
        private float defaultAnimatorSpeed = 1f;
        private Collider2D grappleTarget;
        private Vector2 grappleLocalPoint;
        private bool grappleStartedFromLeft;
        private bool grappleStartedAboveTarget;
        private Collider2D pulledGrappleTarget;
        private Vector2 pulledGrappleLocalPoint;
        private Collider2D struckTarget;
        private Vector2 struckDirection;
        private Coroutine climbRoutine;
        private Collider2D aimedGrappleTarget;
        private Vector2 aimedGrapplePoint;
        private Collider2D aimedStrikeTarget;
        private Vector2 aimedStrikePoint;
        private SpriteRenderer highlightedRenderer;
        private Color highlightedOriginalColor;
        private Sprite spriteBeforeGrappleAim;
        private bool animatorEnabledBeforeGrappleAim;
        private bool hasGrappleAimVisualOverride;
        private bool jumpRequested;
        private bool isGrounded;
        private bool isWallSliding;
        private bool isDead;
        private bool isGrappling;
        private bool isPullingGrappleObject;
        private bool isStrikingObject;
        private bool isGrappleAiming;
        private bool isStrikeAiming;
        private bool isClimbing;
        private bool isForcedWallSliding;
        private bool isRunBlockedAhead;
        private int wallSide;
        private bool isWallJumpControlling;
        private float wallJumpHorizontalVelocity;
        private int wallJumpStartWallSide;
        private Vector2 movingStepCarryThisFrame;
        private int currentAttack;

        // safe position tracking for void teleport
        private Vector2 lastSafePosition;
        private bool hasSafePosition;

        // audio state tracking
        private bool wasGrounded;
        private bool wasWallSliding;
        private bool wasGrappling;
        private bool wasPullingGrappleObject;
        private bool wasStrikingObject;
        private bool footstepLoopActive;
        private bool wallSlideLoopActive;
        private GameObject anchorIndicator;
        private bool grapplePullLoopActive;
        private bool grappleObjectLoopActive;
        private bool strikeObjectLoopActive;
        private Transform grappleAimRopeRoot;
        private Transform grappleActiveRopeRoot;

        private readonly RaycastHit2D[] pulledObjectCastResults = new RaycastHit2D[16];
        private readonly Collider2D[] attackOverlapResults = new Collider2D[12];
        private readonly List<Enemy2D> enemyPlatformResults = new List<Enemy2D>();
        private readonly Collider2D[] pulledObjectOverlapResults = new Collider2D[16];
        private readonly List<Collider2D> initialPulledObjectOverlaps = new List<Collider2D>();
        private readonly List<Collider2D> initialStruckObjectOverlaps = new List<Collider2D>();
        private readonly List<Enemy2D> carriedEnemiesOnStruckStep = new List<Enemy2D>();
        private readonly List<SpriteRenderer> grappleAimRopeSegments = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> grappleActiveRopeSegments = new List<SpriteRenderer>();

        private static readonly int AnimStateHash = Animator.StringToHash("AnimState");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int AirSpeedYHash = Animator.StringToHash("AirSpeedY");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int WallSlideHash = Animator.StringToHash("WallSlide");
        private static readonly int BlockHash = Animator.StringToHash("Block");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int DeathStateHash = Animator.StringToHash("Death");
        private static readonly int NoBloodHash = Animator.StringToHash("noBlood");
        private static readonly int PullObjectHash = Animator.StringToHash("PullObject");
        private static readonly int PreviewModeHash = Animator.StringToHash("PreviewMode");
        private static readonly int[] AttackHashes =
        {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3"),
        };

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            grappleLine = GetComponent<LineRenderer>();
            body.freezeRotation = true;
            defaultGravityScale = body.gravityScale;
            lastSafePosition = body.position;
            hasSafePosition = true;
            if (animator != null)
            {
                defaultAnimatorSpeed = animator.speed;
                SetAnimatorBoolIfPresent(PreviewModeHash, false);
            }

            if (grappleLine != null)
            {
                grappleLine.positionCount = 2;
                grappleLine.enabled = false;
                ApplyGrappleRopeWidth();
            }

            CreateGrappleAimLines();
        }

        private void Update()
        {
            timeSinceAttack += Time.deltaTime;

            if (isDead)
            {
                jumpRequested = false;
                StopGrappleAim();
                StopStrikeAim();
                return;
            }

            if (Input.GetButtonDown("Jump") && !IsTimeStopAiming())
            {
                jumpRequested = true;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                Attack();
            }

            if (Input.GetMouseButtonDown(0))
            {
                StartGrappleAim();
            }

            if (isGrappleAiming && Input.GetMouseButton(0))
            {
                UpdateGrappleAim();
            }

            if (Input.GetMouseButtonUp(0))
            {
                ReleaseGrappleAim();
            }

            if (Input.GetMouseButtonDown(1))
            {
                StartStrikeAim();
            }

            if (isStrikeAiming && Input.GetMouseButton(1))
            {
                UpdateStrikeAim();
            }

            if (Input.GetMouseButtonUp(1))
            {
                ReleaseStrikeAim();
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                Kill();
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            if (spriteRenderer != null && !isGrappleAiming && !isStrikeAiming && !isWallSliding && !isWallJumpControlling)
            {
                spriteRenderer.flipX = false;
            }
        }

        private void FixedUpdate()
        {
            isGrounded = CheckGrounded();
            isRunBlockedAhead = IsRunBlockedAhead();
            bool hasWallBeside = TryGetWallBeside(out int detectedWallSide, out Collider2D detectedWall);
            if (hasWallBeside)
            {
                wallSide = detectedWallSide;
            }

            bool caughtOppositeWallAfterJump = TryCatchOppositeWallAfterWallJump(hasWallBeside, detectedWallSide, detectedWall);
            if (ShouldEndWallJumpControl(isGrounded))
            {
                isWallJumpControlling = false;
            }

            if (isForcedWallSliding && (isGrounded || !hasWallBeside))
            {
                isForcedWallSliding = false;
            }

            bool naturalWallSliding = !isGrounded && !isGrappling && !isClimbing && (body.velocity.y < 0f || caughtOppositeWallAfterJump) && hasWallBeside;
            isWallSliding = isForcedWallSliding || naturalWallSliding;
            SnapWallSlideToWall(hasWallBeside, detectedWallSide, detectedWall);

            if (isDead)
            {
                StopAllAudioLoops();
                StopGrapple();
                StopPullGrappleObject();
                StopStrikeObject();
                StopClimb();
                isForcedWallSliding = false;
                isWallSliding = false;
                ApplyWallSlideGravity();
                body.velocity = new Vector2(0f, body.velocity.y);
                UpdateAnimator();
                return;
            }

            if (isClimbing)
            {
                StopAllAudioLoops();
                body.gravityScale = 0f;
                body.velocity = Vector2.zero;
                UpdateAnimator();
                UpdateGrappleLine();
                jumpRequested = false;
                return;
            }

            ApplyWallSlideGravity();

            bool didWallJump = false;
            if (jumpRequested && isWallSliding && !IsTimeStopAiming())
            {
                WallJump();
                didWallJump = true;
            }

            if (isPullingGrappleObject)
            {
                body.velocity = new Vector2(GetCurrentHorizontalSpeed(), body.velocity.y);
                PullGrappleObjectTowardPlayer();
            }
            else if (isStrikingObject)
            {
                body.velocity = new Vector2(GetCurrentHorizontalSpeed(), body.velocity.y);
                MoveStruckObject();
            }
            else if (isGrappling)
            {
                PullTowardGrapplePoint();
            }
            else if (isWallSliding)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
                ClampWallSlideFallSpeed();
            }
            else
            {
                body.velocity = new Vector2(GetCurrentHorizontalSpeed(), body.velocity.y);
            }

            if (!didWallJump && jumpRequested && isGrounded && !IsTimeStopAiming())
            {
                StopGrapple();
                StopPullGrappleObject();
                StopStrikeObject();
                isForcedWallSliding = false;
                isWallSliding = false;
                ApplyWallSlideGravity();
                body.velocity = new Vector2(body.velocity.x, jumpForce);
                isGrounded = false;

                if (animator != null)
                {
                    animator.SetTrigger(JumpHash);
                }

                PlaySound(jumpClip);
            }

            // detect landing
            if (isGrounded && !wasGrounded)
            {
                PlaySound(landClip);
            }

            UpdateAnimator();
            UpdateGrappleLine();
            ApplyMovingStepCarry();
            WrapAtMapEdges();
            UpdateAudioLoops();
            UpdateAudioStateTracking();
            UpdateSafePosition();
            jumpRequested = false;
        }

        private void LateUpdate()
        {
            if (isGrappleAiming)
            {
                UpdateGrappleAim();
            }

            if (isStrikeAiming)
            {
                UpdateStrikeAim();
            }

            UpdateGrappleLine();
        }

        private void OnDisable()
        {
            StopAllAudioLoops();
            StopGrappleAim();
            StopStrikeAim();
            StopGrapple();
            StopPullGrappleObject();
            StopStrikeObject();
            StopClimb();
            isForcedWallSliding = false;
            isWallJumpControlling = false;
        }

        // ---- audio helpers -----------------------------------------------------------

        private void PlaySound(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            AudioManager2D manager = AudioManager2D.Instance;
            if (manager != null)
            {
                manager.PlayOneShotAt(clip, transform.position, volume, pitch);
            }
        }

        private void StartLoop(ref bool active, string key, AudioClip clip, float volume = 1f)
        {
            if (active || clip == null) return;
            AudioManager2D manager = AudioManager2D.Instance;
            if (manager != null)
            {
                manager.StartLoop(key, clip, volume);
                active = true;
            }
        }

        private void StopLoop(ref bool active, string key)
        {
            if (!active) return;
            AudioManager2D manager = AudioManager2D.Instance;
            if (manager != null)
            {
                manager.StopLoop(key);
            }
            active = false;
        }

        // ---- anchor indicator -------------------------------------------------------

        private void CreateAnchorIndicator()
        {
            if (anchorSprite == null) return;
            DestroyAnchorIndicator();
            anchorIndicator = new GameObject("AnchorIndicator");
            ApplyAnchorIndicatorScale();
            SpriteRenderer sr = anchorIndicator.AddComponent<SpriteRenderer>();
            sr.sprite = anchorSprite;
            sr.sortingOrder = 20;
        }

        private void DestroyAnchorIndicator()
        {
            if (anchorIndicator != null)
            {
                Destroy(anchorIndicator);
                anchorIndicator = null;
            }
        }

        private void UpdateAnchorIndicator(Vector2 position, Vector2 direction)
        {
            if (anchorIndicator == null) return;
            anchorIndicator.transform.position = position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
            anchorIndicator.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            ApplyAnchorIndicatorScale();
        }

        private void ApplyAnchorIndicatorScale()
        {
            if (anchorIndicator == null)
            {
                return;
            }

            anchorIndicator.transform.localScale = Vector3.one * Mathf.Max(0.001f, anchorScale);
        }

        private void StopAllAudioLoops()
        {
            StopLoop(ref footstepLoopActive, "player_footstep");
            StopLoop(ref wallSlideLoopActive, "player_wallslide");
            StopLoop(ref grapplePullLoopActive, "player_grapple_pull");
            StopLoop(ref grappleObjectLoopActive, "player_grapple_object");
            StopLoop(ref strikeObjectLoopActive, "player_strike_object");
        }

        private void UpdateAudioLoops()
        {
            if (isDead)
            {
                StopAllAudioLoops();
                return;
            }

            // footsteps: when grounded and moving (auto-run is active)
            bool shouldFootstep = isGrounded && !isRunBlockedAhead && !IsTimeStopAiming() && !isWallSliding;
            if (shouldFootstep && !footstepLoopActive)
                StartLoop(ref footstepLoopActive, "player_footstep", footstepsLoopClip);
            else if (!shouldFootstep && footstepLoopActive)
                StopLoop(ref footstepLoopActive, "player_footstep");

            // wall slide: when sliding down a wall
            if (isWallSliding && !wallSlideLoopActive)
                StartLoop(ref wallSlideLoopActive, "player_wallslide", wallSlideLoopClip);
            else if (!isWallSliding && wallSlideLoopActive)
                StopLoop(ref wallSlideLoopActive, "player_wallslide");

            // grapple pull self
            if (isGrappling && !grapplePullLoopActive)
                StartLoop(ref grapplePullLoopActive, "player_grapple_pull", grapplePullSelfLoopClip);
            else if (!isGrappling && grapplePullLoopActive)
                StopLoop(ref grapplePullLoopActive, "player_grapple_pull");

            // grapple pull object
            if (isPullingGrappleObject && !grappleObjectLoopActive)
                StartLoop(ref grappleObjectLoopActive, "player_grapple_object", grapplePullObjectLoopClip);
            else if (!isPullingGrappleObject && grappleObjectLoopActive)
                StopLoop(ref grappleObjectLoopActive, "player_grapple_object");

            // strike object moving
            if (isStrikingObject && !strikeObjectLoopActive)
                StartLoop(ref strikeObjectLoopActive, "player_strike_object", strikeObjectLoopClip);
            else if (!isStrikingObject && strikeObjectLoopActive)
                StopLoop(ref strikeObjectLoopActive, "player_strike_object");
        }

        private void UpdateAudioStateTracking()
        {
            wasGrounded = isGrounded;
            wasWallSliding = isWallSliding;
            wasGrappling = isGrappling;
            wasPullingGrappleObject = isPullingGrappleObject;
            wasStrikingObject = isStrikingObject;
        }

        // ---- gameplay ---------------------------------------------------------------

        private void Attack(bool dealDamage = true)
        {
            if (animator == null)
            {
                return;
            }

            if (timeSinceAttack > attackComboResetTime)
            {
                currentAttack = 0;
            }

            animator.SetTrigger(AttackHashes[currentAttack]);
            currentAttack = (currentAttack + 1) % AttackHashes.Length;
            timeSinceAttack = 0f;
            PlaySound(attackClip);
            if (dealDamage)
            {
                HitEnemiesInAttackRange();
            }
        }

        private void HitEnemiesInAttackRange()
        {
            float facing = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
            Vector2 center = (Vector2)transform.position + new Vector2(attackHitOffset.x * facing, attackHitOffset.y);
            int hitCount = Physics2D.OverlapBoxNonAlloc(center, attackHitSize, 0f, attackOverlapResults);
            bool hitTarget = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = attackOverlapResults[i];
                if (hit == null)
                {
                    continue;
                }

                BossController2D boss = hit.GetComponentInParent<BossController2D>();
                if (boss != null)
                {
                    boss.TakePlayerAttackDamage();
                    hitTarget = true;
                    continue;
                }

                if (hit.CompareTag(EnemyTag))
                {
                    Enemy2D enemy = hit.GetComponent<Enemy2D>();
                    if (enemy == null)
                    {
                        enemy = hit.GetComponentInParent<Enemy2D>();
                    }

                    if (enemy != null)
                    {
                        enemy.Defeat();
                        hitTarget = true;
                    }
                }
            }

            if (hitTarget)
            {
                PlaySound(attackHitClip);
            }
        }

        public void Kill()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            jumpRequested = false;
            StopAllAudioLoops();
            StopGrappleAim();
            StopStrikeAim();
            StopGrapple();
            StopPullGrappleObject();
            StopStrikeObject();
            StopClimb();
            isForcedWallSliding = false;
            isWallJumpControlling = false;
            body.velocity = Vector2.zero;

            PlaySound(deathClip);

            PlayDeathAnimation();

            if (restartOnDeath)
            {
                StartCoroutine(RestartAfterDelay());
            }
        }

        private void PlayDeathAnimation()
        {
            if (animator == null)
            {
                return;
            }

            animator.enabled = true;
            animator.speed = defaultAnimatorSpeed;
            animator.ResetTrigger(JumpHash);
            for (int i = 0; i < AttackHashes.Length; i++)
            {
                animator.ResetTrigger(AttackHashes[i]);
            }

            animator.SetBool(BlockHash, false);
            animator.SetBool(WallSlideHash, false);
            animator.SetBool(NoBloodHash, false);
            animator.SetInteger(AnimStateHash, 0);
            animator.SetFloat(AirSpeedYHash, 0f);

            if (animator.HasState(0, DeathStateHash))
            {
                animator.Play(DeathStateHash, 0, 0f);
                animator.Update(0f);
                return;
            }

            animator.SetTrigger(DeathHash);
        }

        private IEnumerator RestartAfterDelay()
        {
            float delay = Mathf.Max(0f, deathRestartDelay);
            yield return new WaitForSeconds(delay);
            AudioManager2D manager = AudioManager2D.Instance;
            if (manager != null)
            {
                manager.StopAll();
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Called by TeleportZone2D (and other external systems) after moving the player.
        /// Cleans up grapple/aim/wall-slide state so the player arrives cleanly.
        /// </summary>
        public void OnTeleported()
        {
            StopGrappleAim();
            StopStrikeAim();
            StopGrapple();
            StopPullGrappleObject();
            StopStrikeObject();
            StopClimb();
            StopAllAudioLoops();
            isForcedWallSliding = false;
            isWallJumpControlling = false;
            UpdateAudioLoops();
        }

        /// <summary>
        /// Teleport the player back to the last recorded safe position.
        /// Returns false if no safe position has been recorded yet.
        /// </summary>
        public bool TeleportToLastSafePosition()
        {
            if (!hasSafePosition || isDead)
            {
                return false;
            }

            OnTeleported();
            body.position = lastSafePosition;
            body.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            return true;
        }

        private void UpdateSafePosition()
        {
            if (isDead)
            {
                return;
            }

            // Only record position when grounded and not in special states
            if (isGrounded && !isClimbing && !isGrappling && !isPullingGrappleObject && !isStrikingObject)
            {
                lastSafePosition = body.position;
                hasSafePosition = true;
            }
        }

        // Called by the Hero Knight wall-slide animation event.
        public void AE_SlideDust()
        {
        }

        private void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(GroundedHash, isGrounded);
            SetAnimatorBoolIfPresent(PreviewModeHash, false);
            animator.SetBool(WallSlideHash, isWallSliding);
            SetAnimatorBoolIfPresent(PullObjectHash, isPullingGrappleObject);
            animator.SetFloat(AirSpeedYHash, body.velocity.y);
            animator.SetInteger(AnimStateHash, ShouldPlayRunAnimation() ? 1 : 0);
            UpdateWallSlideFacing();
            UpdateWallJumpFacing();
        }

        private void SetAnimatorBoolIfPresent(int parameterHash, bool value)
        {
            if (animator == null || !HasAnimatorParameter(parameterHash))
            {
                return;
            }

            animator.SetBool(parameterHash, value);
        }

        private bool HasAnimatorParameter(int parameterHash)
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == parameterHash)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateWallSlideFacing()
        {
            if (spriteRenderer == null || !isWallSliding || isGrappleAiming || isStrikeAiming)
            {
                return;
            }

            spriteRenderer.flipX = wallSide < 0;
        }

        private void UpdateWallJumpFacing()
        {
            if (spriteRenderer == null || !isWallJumpControlling || isWallSliding || isGrappleAiming || isStrikeAiming)
            {
                return;
            }

            spriteRenderer.flipX = wallJumpHorizontalVelocity < 0f;
        }

        private bool ShouldPlayRunAnimation()
        {
            if (isDead)
            {
                return false;
            }

            if (!isGrounded)
            {
                return true;
            }

            if (isRunBlockedAhead && !isGrappling && !isPullingGrappleObject && !isStrikingObject && !IsTimeStopAiming() && !isClimbing && !isWallSliding)
            {
                return false;
            }

            return true;
        }

        public void CarryByMovingStep(Vector2 movement)
        {
            if (isDead || isClimbing || isGrappling || isPullingGrappleObject || isStrikingObject)
            {
                return;
            }

            movingStepCarryThisFrame += movement;
        }

        private void ApplyMovingStepCarry()
        {
            if (movingStepCarryThisFrame.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            body.position += movingStepCarryThisFrame;
            movingStepCarryThisFrame = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private void ApplyWallSlideGravity()
        {
            float gravityScale = defaultGravityScale;
            if (isWallSliding)
            {
                gravityScale *= wallSlideFallSpeedMultiplier;
            }

            if (isGrappleAiming || isStrikeAiming)
            {
                gravityScale *= grappleAimMoveSpeedMultiplier;
            }

            body.gravityScale = gravityScale;
        }

        private void ClampWallSlideFallSpeed()
        {
            if (!isWallSliding || body.velocity.y >= -wallSlideMaxFallSpeed)
            {
                return;
            }

            body.velocity = new Vector2(body.velocity.x, -wallSlideMaxFallSpeed);
        }

        private void StartGrappleAim()
        {
            if (isGrappleAiming || isStrikeAiming || isClimbing)
            {
                return;
            }

            if (isGrappling)
            {
                StopGrapple();
            }

            if (isPullingGrappleObject)
            {
                StopPullGrappleObject();
            }

            if (isStrikingObject)
            {
                StopStrikeObject();
            }

            isGrappleAiming = true;
            aimedGrappleTarget = null;
            jumpRequested = false;
            body.velocity *= grappleAimMoveSpeedMultiplier;
            ApplyWallSlideGravity();
            EnterGrappleAimVisual();

            if (animator != null)
            {
                animator.speed = defaultAnimatorSpeed * grappleAimMoveSpeedMultiplier;
            }

            if (grappleAimCircleLine != null)
            {
                grappleAimCircleLine.enabled = true;
            }

            if (grappleAimRayLine != null)
            {
                grappleAimRayLine.enabled = grappleRopeSprite == null;
            }

            SetRopeSegmentsActive(grappleAimRopeSegments, false);

            PlaySound(grappleAimStartClip);
            CreateAnchorIndicator();
            UpdateGrappleAim();
        }

        private void UpdateGrappleAim()
        {
            if (!isGrappleAiming)
            {
                return;
            }

            Vector2 origin = GetGrappleOrigin();
            UpdateGrappleAimCircle(origin, grappleAimRadius);

            if (!TryGetMouseAimDirection(origin, out Vector2 direction))
            {
                ClearGrappleHighlight();
                UpdateGrappleAimRay(origin, origin);
                return;
            }

            UpdateGrappleAimFacing(direction);

            RaycastHit2D hit = GetFirstGrappleRayHit(origin, direction, grappleAimRadius);
            if (hit.collider != null)
            {
                aimedGrappleTarget = hit.collider;
                aimedGrapplePoint = hit.point;
                ApplyGrappleHighlight(aimedGrappleTarget);
                UpdateGrappleAimRay(origin, aimedGrapplePoint);
                UpdateAnchorIndicator(aimedGrapplePoint, direction);
                return;
            }

            aimedGrappleTarget = null;
            ClearGrappleHighlight();
            UpdateGrappleAimRay(origin, origin + direction * grappleAimRadius);
            UpdateAnchorIndicator(origin + direction * grappleAimRadius, direction);
        }

        private void ReleaseGrappleAim()
        {
            Collider2D target = aimedGrappleTarget;
            Vector2 targetPoint = aimedGrapplePoint;
            StopGrappleAim();

            if (target != null && !target.isTrigger)
            {
                PlaySound(grappleFireClip);
                PlaySound(grappleConnectClip);

                if (TryGetRunBlocker(out Collider2D blocker) && IsTargetOnSameSideAsBlocker(target, blocker))
                {
                    StartPullGrappleObject(target, targetPoint);
                    return;
                }

                StartGrapple(target, targetPoint);
            }
        }

        private void StopGrappleAim()
        {
            bool wasAiming = isGrappleAiming;
            isGrappleAiming = false;
            aimedGrappleTarget = null;
            ClearGrappleHighlight();

            if (wasAiming)
            {
                ApplyWallSlideGravity();
            }

            if (animator != null)
            {
                animator.speed = defaultAnimatorSpeed;
            }

            ExitGrappleAimVisual();

            if (grappleAimCircleLine != null)
            {
                grappleAimCircleLine.enabled = false;
            }

            if (grappleAimRayLine != null)
            {
                grappleAimRayLine.enabled = false;
            }

            SetRopeSegmentsActive(grappleAimRopeSegments, false);
            DestroyAnchorIndicator();
        }

        private void EnterGrappleAimVisual()
        {
            EnterAimVisual(grappleAimSprite);
        }

        private void EnterAimVisual(Sprite aimSprite)
        {
            if (spriteRenderer == null || aimSprite == null)
            {
                return;
            }

            spriteBeforeGrappleAim = spriteRenderer.sprite;
            animatorEnabledBeforeGrappleAim = animator != null && animator.enabled;
            hasGrappleAimVisualOverride = true;

            if (animator != null)
            {
                animator.enabled = false;
            }

            spriteRenderer.sprite = aimSprite;
        }

        private void UpdateGrappleAimFacing(Vector2 direction)
        {
            if (spriteRenderer == null || Mathf.Abs(direction.x) <= 0.01f)
            {
                return;
            }

            spriteRenderer.flipX = direction.x < 0f;
        }

        private void ExitGrappleAimVisual()
        {
            if (!hasGrappleAimVisualOverride)
            {
                return;
            }

            if (spriteRenderer != null && spriteBeforeGrappleAim != null)
            {
                spriteRenderer.sprite = spriteBeforeGrappleAim;
            }

            if (animator != null)
            {
                animator.enabled = animatorEnabledBeforeGrappleAim;
            }

            hasGrappleAimVisualOverride = false;
            spriteBeforeGrappleAim = null;
        }

        private void StartGrapple(Collider2D target, Vector2 anchorPoint)
        {
            StopPullGrappleObject();
            StopStrikeObject();
            grappleTarget = target;
            grappleLocalPoint = target.transform.InverseTransformPoint(anchorPoint);
            grappleStartedFromLeft = body.position.x <= target.bounds.center.x;
            grappleStartedAboveTarget = bodyCollider.bounds.min.y >= target.bounds.max.y;
            isGrappling = true;

            if (grappleLine != null)
            {
                grappleLine.enabled = grappleRopeSprite == null;
            }
        }

        private void StartPullGrappleObject(Collider2D target, Vector2 anchorPoint)
        {
            if (target == null)
            {
                return;
            }

            StopGrapple();
            StopStrikeObject();
            pulledGrappleTarget = target;
            pulledGrappleLocalPoint = target.transform.InverseTransformPoint(anchorPoint);
            DropEnemiesStandingOnPulledStep(target);
            CacheInitialPulledObjectOverlaps(target);
            isPullingGrappleObject = true;

            if (grappleLine != null)
            {
                grappleLine.enabled = grappleRopeSprite == null;
            }

            // Notify step it's been pulled (for breakable steps)
            StepMover2D stepMover = target.GetComponent<StepMover2D>();
            if (stepMover != null)
            {
                stepMover.OnStruck();

                // OnStruck may have destroyed the target
                if (target == null)
                {
                    StopPullGrappleObject();
                }
            }
        }

        private float GetCurrentAutoRunSpeed()
        {
            return isGrappleAiming || isStrikeAiming ? autoRunSpeed * grappleAimMoveSpeedMultiplier : autoRunSpeed;
        }

        private float GetCurrentHorizontalSpeed()
        {
            return isWallJumpControlling ? wallJumpHorizontalVelocity : GetCurrentAutoRunSpeed();
        }

        private void WallJump()
        {
            StopGrapple();
            StopPullGrappleObject();
            StopStrikeObject();
            StopClimb();

            int jumpAwayDirection = wallSide == 0 ? -1 : -wallSide;
            wallJumpHorizontalVelocity = jumpAwayDirection * wallJumpHorizontalSpeed;
            isWallJumpControlling = true;
            wallJumpStartWallSide = wallSide;
            isForcedWallSliding = false;
            isWallSliding = false;
            isGrounded = false;
            ApplyWallSlideGravity();
            body.velocity = new Vector2(wallJumpHorizontalVelocity, wallJumpVerticalSpeed);

            PlaySound(wallJumpClip);

            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }
        }

        private bool ShouldEndWallJumpControl(bool grounded)
        {
            if (!isWallJumpControlling)
            {
                return false;
            }

            return grounded;
        }

        private bool TryCatchOppositeWallAfterWallJump(bool hasWallBeside, int detectedWallSide, Collider2D detectedWall)
        {
            if (!isWallJumpControlling || !hasWallBeside || detectedWallSide == 0 || detectedWallSide == wallJumpStartWallSide)
            {
                return false;
            }

            if (detectedWall != null)
            {
                PlaceBesideWall(detectedWall, detectedWallSide > 0);
            }

            wallSide = detectedWallSide;
            isWallJumpControlling = false;
            isForcedWallSliding = true;
            isWallSliding = true;
            body.velocity = new Vector2(0f, Mathf.Min(body.velocity.y, -0.1f));
            return true;
        }

        private void SnapWallSlideToWall(bool hasWallBeside, int detectedWallSide, Collider2D detectedWall)
        {
            if (!isWallSliding || !hasWallBeside || detectedWallSide == 0 || detectedWall == null)
            {
                return;
            }

            PlaceBesideWall(detectedWall, detectedWallSide > 0);
        }

        private bool IsTimeStopAiming()
        {
            return isGrappleAiming || isStrikeAiming;
        }

        private bool TryGetMouseAimDirection(Vector2 origin, out Vector2 direction)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                direction = default;
                return false;
            }

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 toMouse = new Vector2(mouseWorld.x, mouseWorld.y) - origin;
            if (toMouse.sqrMagnitude <= 0.0001f)
            {
                direction = default;
                return false;
            }

            direction = toMouse.normalized;
            return true;
        }

        private RaycastHit2D GetFirstGrappleRayHit(Vector2 origin, Vector2 direction, float radius)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, radius, grappleMask);
            RaycastHit2D bestHit = default;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null || hit.collider.isTrigger || hit.collider == bodyCollider || IsIgnoredGrappleTarget(hit.collider))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestHit = hit;
                    bestDistance = hit.distance;
                }
            }

            return bestHit;
        }

        private bool IsIgnoredGrappleTarget(Collider2D target)
        {
            return target.gameObject.tag == GroundTag;
        }

        private RaycastHit2D GetFirstStrikeRayHit(Vector2 origin, Vector2 direction)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, strikeAimRadius, grappleMask);
            RaycastHit2D bestHit = default;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null
                    || hit.collider.isTrigger
                    || hit.collider == bodyCollider
                    || IsIgnoredGrappleTarget(hit.collider)
                    || IsGrappleWallTarget(hit.collider))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestHit = hit;
                    bestDistance = hit.distance;
                }
            }

            return bestHit;
        }

        private void ApplyGrappleHighlight(Collider2D target)
        {
            SpriteRenderer targetRenderer = target.GetComponent<SpriteRenderer>();
            if (targetRenderer == highlightedRenderer)
            {
                return;
            }

            ClearGrappleHighlight();

            if (targetRenderer == null)
            {
                return;
            }

            highlightedRenderer = targetRenderer;
            highlightedOriginalColor = targetRenderer.color;
            targetRenderer.color = grappleHighlightColor;
        }

        private void ClearGrappleHighlight()
        {
            if (highlightedRenderer == null)
            {
                return;
            }

            highlightedRenderer.color = highlightedOriginalColor;
            highlightedRenderer = null;
        }

        private void UpdateGrappleAimRay(Vector2 origin, Vector2 end)
        {
            if (grappleRopeSprite != null)
            {
                if (grappleAimRayLine != null)
                {
                    grappleAimRayLine.enabled = false;
                }

                UpdateRopeImageLine(origin, end, grappleAimRopeSegments, ref grappleAimRopeRoot, "Grapple Aim Chain", 19);
                return;
            }

            SetRopeSegmentsActive(grappleAimRopeSegments, false);
            if (grappleAimRayLine == null)
            {
                return;
            }

            grappleAimRayLine.SetPosition(0, origin);
            grappleAimRayLine.SetPosition(1, end);
        }

        private void UpdateGrappleAimCircle(Vector2 origin, float radius)
        {
            if (grappleAimCircleLine == null)
            {
                return;
            }

            int segmentCount = Mathf.Max(12, grappleAimCircleSegments);
            if (grappleAimCircleLine.positionCount != segmentCount)
            {
                grappleAimCircleLine.positionCount = segmentCount;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segmentCount;
                Vector3 position = new Vector3(
                    origin.x + Mathf.Cos(angle) * radius,
                    origin.y + Mathf.Sin(angle) * radius,
                    transform.position.z);
                grappleAimCircleLine.SetPosition(i, position);
            }
        }

        private void CreateGrappleAimLines()
        {
            Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
            Material rayMaterial = grappleAimRayMaterial != null ? grappleAimRayMaterial : lineMaterial;
            grappleAimCircleLine = CreateGrappleGuideLine("Grapple Aim Radius", lineMaterial, 0.035f, grappleAimCircleColor, true);
            grappleAimRayLine = CreateGrappleGuideLine("Grapple Aim Ray", rayMaterial, grappleRopeWidth, Color.white, false);
            grappleAimRayLine.textureMode = LineTextureMode.Tile;
        }

        private void ApplyGrappleRopeWidth()
        {
            float width = Mathf.Max(0.001f, grappleRopeWidth);
            if (grappleLine != null)
            {
                grappleLine.startWidth = width;
                grappleLine.endWidth = width;
            }

            if (grappleAimRayLine != null)
            {
                grappleAimRayLine.startWidth = width;
                grappleAimRayLine.endWidth = width;
            }
        }

        private LineRenderer CreateGrappleGuideLine(string name, Material material, float width, Color color, bool loop)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = loop ? Mathf.Max(12, grappleAimCircleSegments) : 2;
            line.loop = loop;
            line.enabled = false;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = loop ? 0 : 2;
            line.alignment = LineAlignment.View;
            line.sortingOrder = 19;
            line.material = material;
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        private void UpdateRopeImageLine(Vector2 origin, Vector2 end, List<SpriteRenderer> segments, ref Transform root, string rootName, int sortingOrder)
        {
            if (grappleRopeSprite == null)
            {
                SetRopeSegmentsActive(segments, false);
                return;
            }

            Vector2 delta = end - origin;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
            {
                SetRopeSegmentsActive(segments, false);
                return;
            }

            if (root == null)
            {
                GameObject rootObject = new GameObject(rootName);
                rootObject.transform.SetParent(transform, false);
                root = rootObject.transform;
            }

            Vector2 direction = delta / distance;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.01f, grappleRopeSegmentLength)));
            float segmentLength = distance / segmentCount;
            Bounds spriteBounds = grappleRopeSprite.bounds;
            float spriteWidth = Mathf.Max(0.001f, spriteBounds.size.x);
            float spriteHeight = Mathf.Max(0.001f, spriteBounds.size.y);
            float ropeWidth = Mathf.Max(0.001f, grappleRopeWidth);

            for (int i = 0; i < segmentCount; i++)
            {
                SpriteRenderer segment = GetOrCreateRopeSegment(segments, root, rootName, i, sortingOrder);
                segment.gameObject.SetActive(true);
                segment.sprite = grappleRopeSprite;
                segment.sortingOrder = sortingOrder;
                segment.color = Color.white;

                Vector2 segmentCenter = origin + direction * (segmentLength * (i + 0.5f));
                segment.transform.position = new Vector3(segmentCenter.x, segmentCenter.y, transform.position.z);
                segment.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                segment.transform.localScale = new Vector3(segmentLength / spriteWidth, ropeWidth / spriteHeight, 1f);
            }

            for (int i = segmentCount; i < segments.Count; i++)
            {
                segments[i].gameObject.SetActive(false);
            }
        }

        private SpriteRenderer GetOrCreateRopeSegment(List<SpriteRenderer> segments, Transform root, string rootName, int index, int sortingOrder)
        {
            while (segments.Count <= index)
            {
                GameObject segmentObject = new GameObject($"{rootName} Segment {segments.Count + 1:00}");
                segmentObject.transform.SetParent(root, false);
                SpriteRenderer renderer = segmentObject.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = sortingOrder;
                segments.Add(renderer);
            }

            return segments[index];
        }

        private static void SetRopeSegmentsActive(List<SpriteRenderer> segments, bool active)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                {
                    segments[i].gameObject.SetActive(active);
                }
            }
        }

        private void PullTowardGrapplePoint()
        {
            if (!TryGetCurrentGrapplePoint(out Vector2 targetPoint))
            {
                StopGrapple();
                return;
            }

            Vector2 position = body.position;
            Vector2 toTarget = targetPoint - position;

            if (bodyCollider.IsTouching(grappleTarget) || toTarget.magnitude <= grappleStopDistance)
            {
                ResolveGrappleContact(grappleTarget, grappleStartedFromLeft, grappleStartedAboveTarget, targetPoint);
                return;
            }

            body.velocity = toTarget.normalized * grapplePullSpeed;
        }

        private void PullGrappleObjectTowardPlayer()
        {
            if (!TryGetCurrentPulledObjectPoint(out Vector2 anchorPoint))
            {
                StopPullGrappleObject();
                return;
            }

            Vector2 toPlayer = GetGrappleOrigin() - anchorPoint;
            if (toPlayer.magnitude <= grappleStopDistance || HasPulledObjectHitNewCollider())
            {
                StopPullGrappleObject();
                return;
            }

            Vector2 movement = toPlayer.normalized * grappleObjectPullSpeed * Time.fixedDeltaTime;
            if (movement.sqrMagnitude > toPlayer.sqrMagnitude)
            {
                movement = toPlayer;
            }

            bool willHitCollider = TryClipPulledObjectMovement(movement, out movement);
            if (movement.sqrMagnitude <= 0.000001f)
            {
                StopPullGrappleObject();
                return;
            }

            pulledGrappleTarget.transform.position += (Vector3)movement;
            Physics2D.SyncTransforms();

            if (willHitCollider || HasPulledObjectHitNewCollider())
            {
                StopPullGrappleObject();
            }
        }

        private void StartStrikeObject(Collider2D target, Vector2 direction)
        {
            if (target == null)
            {
                return;
            }

            StopGrapple();
            StopPullGrappleObject();
            struckTarget = target;
            struckDirection = direction.normalized;
            CacheInitialStruckObjectOverlaps(target);
            CacheEnemiesCarriedByStruckStep(target);
            isStrikingObject = true;

            // Notify the step that it's been struck (for breakable steps)
            StepMover2D stepMover = target.GetComponent<StepMover2D>();
            if (stepMover != null)
            {
                stepMover.OnStruck();

                // OnStruck may have destroyed the target
                if (target == null)
                {
                    StopStrikeObject();
                }
            }
        }

        private void MoveStruckObject()
        {
            if (struckTarget == null)
            {
                StopStrikeObject();
                return;
            }

            Vector2 movement = struckDirection * strikeObjectSpeed * Time.fixedDeltaTime;
            bool willHitCollider = TryClipStruckObjectMovement(movement, out movement);
            if (movement.sqrMagnitude <= 0.000001f)
            {
                StopStrikeObject();
                return;
            }

            struckTarget.transform.position += (Vector3)movement;
            MoveEnemiesCarriedByStruckStep(movement);
            Physics2D.SyncTransforms();

            if (willHitCollider || HasStruckObjectHitNewCollider())
            {
                PlaySound(strikeImpactClip);
                StopStrikeObject();
            }
        }

        private void StartStrikeAim()
        {
            if (isStrikeAiming || isGrappleAiming || isClimbing)
            {
                return;
            }

            if (isGrappling)
            {
                StopGrapple();
            }

            if (isPullingGrappleObject)
            {
                StopPullGrappleObject();
            }

            if (isStrikingObject)
            {
                StopStrikeObject();
            }

            isStrikeAiming = true;
            aimedStrikeTarget = null;
            jumpRequested = false;
            body.velocity *= grappleAimMoveSpeedMultiplier;
            ApplyWallSlideGravity();
            EnterAimVisual(strikeAimSprite != null ? strikeAimSprite : grappleAimSprite);

            if (grappleAimCircleLine != null)
            {
                grappleAimCircleLine.enabled = true;
            }

            if (grappleAimRayLine != null)
            {
                grappleAimRayLine.enabled = grappleRopeSprite == null;
            }

            PlaySound(strikeAimStartClip);
            CreateAnchorIndicator();
            UpdateStrikeAim();
        }

        private void UpdateStrikeAim()
        {
            if (!isStrikeAiming)
            {
                return;
            }

            Vector2 origin = GetGrappleOrigin();
            UpdateGrappleAimCircle(origin, strikeAimRadius);

            if (!TryGetMouseAimDirection(origin, out Vector2 direction))
            {
                ClearGrappleHighlight();
                UpdateGrappleAimRay(origin, origin);
                return;
            }

            UpdateGrappleAimFacing(direction);

            RaycastHit2D hit = GetFirstStrikeRayHit(origin, direction);
            if (hit.collider != null)
            {
                aimedStrikeTarget = hit.collider;
                aimedStrikePoint = hit.point;
                ApplyGrappleHighlight(aimedStrikeTarget);
                UpdateGrappleAimRay(origin, aimedStrikePoint);
                UpdateAnchorIndicator(aimedStrikePoint, direction);
                return;
            }

            aimedStrikeTarget = null;
            ClearGrappleHighlight();
            UpdateGrappleAimRay(origin, origin + direction * strikeAimRadius);
            UpdateAnchorIndicator(origin + direction * strikeAimRadius, direction);
        }

        private void ReleaseStrikeAim()
        {
            bool wasAiming = isStrikeAiming;
            Collider2D target = aimedStrikeTarget;
            Vector2 targetPoint = aimedStrikePoint;
            Vector2 origin = GetGrappleOrigin();
            StopStrikeAim();

            if (wasAiming)
            {
                Attack(false);
            }

            if (target != null && !target.isTrigger && !IsGrappleWallTarget(target))
            {
                PlaySound(strikeFireClip);
                Vector2 direction = targetPoint - origin;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = (Vector2)target.bounds.center - origin;
                }

                if (direction.sqrMagnitude > 0.0001f)
                {
                    StartStrikeObject(target, direction.normalized);
                }
            }
        }

        private void StopStrikeAim()
        {
            bool wasAiming = isStrikeAiming;
            isStrikeAiming = false;
            aimedStrikeTarget = null;
            ClearGrappleHighlight();

            if (wasAiming)
            {
                ApplyWallSlideGravity();
                ExitGrappleAimVisual();
            }

            if (grappleAimCircleLine != null)
            {
                grappleAimCircleLine.enabled = false;
            }

            if (grappleAimRayLine != null)
            {
                grappleAimRayLine.enabled = false;
            }

            SetRopeSegmentsActive(grappleAimRopeSegments, false);
            DestroyAnchorIndicator();
        }

        private void ResolveGrappleContact(Collider2D target, bool fromLeft, bool fromAbove, Vector2 landingPoint)
        {
            if (IsEnemyTarget(target))
            {
                StopGrapple();
                body.velocity = Vector2.zero;
                return;
            }

            if (IsGrappleWallTarget(target))
            {
                StartGrappleWallSlide(target, fromLeft);
                return;
            }

            if (fromAbove)
            {
                LandOnGrapplePoint(target, landingPoint);
                return;
            }

            StartGrappleClimb(target, fromLeft);
        }

        private bool IsGrappleWallTarget(Collider2D target)
        {
            return target != null && target.gameObject.tag == GrappleWallTag;
        }

        private bool IsGrappleStepTarget(Collider2D target)
        {
            return target != null && target.gameObject.tag == GrappleStepTag;
        }

        private bool IsEnemyTarget(Collider2D target)
        {
            return target != null && target.CompareTag(EnemyTag);
        }

        private void DropEnemiesStandingOnPulledStep(Collider2D target)
        {
            if (!IsGrappleStepTarget(target))
            {
                return;
            }

            int count = FindEnemiesStandingOnPlatform(target);
            for (int i = 0; i < count; i++)
            {
                Enemy2D enemy = enemyPlatformResults[i];
                if (enemy != null)
                {
                    enemy.DropFromSupport(target);
                }
            }
        }

        private void CacheEnemiesCarriedByStruckStep(Collider2D target)
        {
            EndEnemiesCarriedByStruckStep();
            if (!IsGrappleStepTarget(target))
            {
                return;
            }

            int count = FindEnemiesStandingOnPlatform(target);
            for (int i = 0; i < count; i++)
            {
                Enemy2D enemy = enemyPlatformResults[i];
                if (enemy == null || carriedEnemiesOnStruckStep.Contains(enemy))
                {
                    continue;
                }

                enemy.BeginPlatformCarry();
                carriedEnemiesOnStruckStep.Add(enemy);
            }
        }

        private void MoveEnemiesCarriedByStruckStep(Vector2 movement)
        {
            for (int i = carriedEnemiesOnStruckStep.Count - 1; i >= 0; i--)
            {
                Enemy2D enemy = carriedEnemiesOnStruckStep[i];
                if (enemy == null)
                {
                    carriedEnemiesOnStruckStep.RemoveAt(i);
                    continue;
                }

                enemy.MoveWithPlatform(movement);
            }
        }

        private void EndEnemiesCarriedByStruckStep()
        {
            for (int i = carriedEnemiesOnStruckStep.Count - 1; i >= 0; i--)
            {
                Enemy2D enemy = carriedEnemiesOnStruckStep[i];
                if (enemy != null)
                {
                    enemy.EndPlatformCarry();
                }
            }

            carriedEnemiesOnStruckStep.Clear();
        }

        private int FindEnemiesStandingOnPlatform(Collider2D platform)
        {
            enemyPlatformResults.Clear();
            Enemy2D[] enemies = FindObjectsByType<Enemy2D>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy2D enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                if (!enemy.IsStandingOn(platform))
                {
                    continue;
                }

                enemyPlatformResults.Add(enemy);
            }

            return enemyPlatformResults.Count;
        }

        private static Enemy2D GetEnemyFromCollider(Collider2D collider)
        {
            if (collider == null || !collider.CompareTag(EnemyTag))
            {
                return null;
            }

            Enemy2D enemy = collider.GetComponent<Enemy2D>();
            return enemy != null ? enemy : collider.GetComponentInParent<Enemy2D>();
        }

        private bool ShouldIgnoreStepPassengerCollider(Collider2D movingTarget, Collider2D other)
        {
            return IsGrappleStepTarget(movingTarget) && GetEnemyFromCollider(other) != null;
        }

        private void StopGrapple()
        {
            isGrappling = false;
            grappleTarget = null;

            if (grappleLine != null && !isPullingGrappleObject)
            {
                grappleLine.enabled = false;
            }

            if (!isPullingGrappleObject)
            {
                SetRopeSegmentsActive(grappleActiveRopeSegments, false);
            }
        }

        private void StopPullGrappleObject()
        {
            isPullingGrappleObject = false;
            pulledGrappleTarget = null;
            initialPulledObjectOverlaps.Clear();

            if (grappleLine != null && !isGrappling)
            {
                grappleLine.enabled = false;
            }

            if (!isGrappling)
            {
                SetRopeSegmentsActive(grappleActiveRopeSegments, false);
            }
        }

        private void StopStrikeObject()
        {
            EndEnemiesCarriedByStruckStep();
            isStrikingObject = false;
            struckTarget = null;
            struckDirection = Vector2.zero;
            initialStruckObjectOverlaps.Clear();
        }

        private void StartGrappleWallSlide(Collider2D target, bool fromLeft)
        {
            if (target == null)
            {
                StopGrapple();
                return;
            }

            StopGrapple();
            StopGrappleAim();
            StopClimb();
            PlaceBesideWall(target, fromLeft);
            wallSide = fromLeft ? 1 : -1;
            isWallJumpControlling = false;
            isForcedWallSliding = true;
            isWallSliding = true;
            isGrounded = false;
            jumpRequested = false;
            body.velocity = new Vector2(0f, -0.1f);
            ApplyWallSlideGravity();
            UpdateAnimator();
        }

        private void StartGrappleClimb(Collider2D target, bool fromLeft)
        {
            if (target == null)
            {
                StopGrapple();
                return;
            }

            StopGrapple();
            StopGrappleAim();

            if (climbRoutine != null)
            {
                StopCoroutine(climbRoutine);
            }

            PlaySound(grappleClimbClip);
            climbRoutine = StartCoroutine(ClimbOntoGrappleTarget(target, fromLeft));
        }

        private IEnumerator ClimbOntoGrappleTarget(Collider2D target, bool fromLeft)
        {
            isClimbing = true;
            isWallSliding = false;
            jumpRequested = false;
            body.gravityScale = 0f;
            body.velocity = Vector2.zero;

            if (animator != null)
            {
                animator.SetBool(WallSlideHash, false);
                animator.SetTrigger(BlockHash);
            }

            yield return new WaitForSeconds(grappleClimbAnimationDuration);

            if (!isDead && target != null)
            {
                PlaceOnTopEdge(target, fromLeft);
            }

            body.gravityScale = defaultGravityScale;
            isClimbing = false;
            climbRoutine = null;
        }

        private void StopClimb()
        {
            if (climbRoutine != null)
            {
                StopCoroutine(climbRoutine);
                climbRoutine = null;
            }

            isClimbing = false;
            if (body != null)
            {
                body.gravityScale = defaultGravityScale;
            }
        }

        private void PlaceBesideWall(Collider2D target, bool fromLeft)
        {
            Bounds targetBounds = target.bounds;
            Bounds playerBounds = bodyCollider.bounds;
            float playerCenterOffsetX = playerBounds.center.x - body.position.x;
            float targetX = fromLeft
                ? targetBounds.min.x - playerBounds.extents.x - playerCenterOffsetX - 0.02f
                : targetBounds.max.x + playerBounds.extents.x - playerCenterOffsetX + 0.02f;

            body.position = new Vector2(targetX, body.position.y);
            Physics2D.SyncTransforms();
        }

        private void LandOnGrapplePoint(Collider2D target, Vector2 landingPoint)
        {
            if (target == null)
            {
                StopGrapple();
                return;
            }

            StopGrapple();
            StopGrappleAim();
            StopClimb();
            PlaceOnTopAtX(target, landingPoint.x);
            isGrounded = true;
            isWallSliding = false;
            isForcedWallSliding = false;
            body.gravityScale = defaultGravityScale;
            body.velocity = Vector2.zero;

            PlaySound(grappleLandClip);
            UpdateAnimator();
        }

        private void PlaceOnTopEdge(Collider2D target, bool fromLeft)
        {
            Bounds targetBounds = target.bounds;
            Bounds playerBounds = bodyCollider.bounds;
            float playerCenterOffsetY = playerBounds.center.y - body.position.y;
            float edgeX = fromLeft
                ? targetBounds.min.x + playerBounds.extents.x
                : targetBounds.max.x - playerBounds.extents.x;

            PlaceOnTopAtX(target, edgeX);
        }

        private void PlaceOnTopAtX(Collider2D target, float worldX)
        {
            Bounds targetBounds = target.bounds;
            Bounds playerBounds = bodyCollider.bounds;
            float playerCenterOffsetY = playerBounds.center.y - body.position.y;
            float minX = targetBounds.min.x + playerBounds.extents.x;
            float maxX = targetBounds.max.x - playerBounds.extents.x;
            float targetX = maxX >= minX
                ? Mathf.Clamp(worldX, minX, maxX)
                : targetBounds.center.x;
            float targetY = targetBounds.max.y + playerBounds.extents.y - playerCenterOffsetY + 0.02f;

            body.position = new Vector2(targetX, targetY);
            body.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private void UpdateGrappleLine()
        {
            if (!isGrappling && !isPullingGrappleObject)
            {
                return;
            }

            if (!TryGetVisibleGrappleEndPoint(out Vector2 targetPoint))
            {
                StopGrapple();
                StopPullGrappleObject();
                return;
            }

            Vector2 origin = GetGrappleOrigin();
            if (grappleRopeSprite != null)
            {
                if (grappleLine != null)
                {
                    grappleLine.enabled = false;
                }

                UpdateRopeImageLine(origin, targetPoint, grappleActiveRopeSegments, ref grappleActiveRopeRoot, "Grapple Chain", 20);
                return;
            }

            SetRopeSegmentsActive(grappleActiveRopeSegments, false);
            if (grappleLine == null)
            {
                return;
            }

            grappleLine.SetPosition(0, origin);
            grappleLine.SetPosition(1, targetPoint);
        }

        private Vector2 GetGrappleOrigin()
        {
            Vector2 baseOrigin = bodyCollider != null ? bodyCollider.bounds.center : transform.position;
            Vector2 offset = grappleOriginOffset;
            if (spriteRenderer != null && spriteRenderer.flipX)
            {
                offset.x *= -1f;
            }

            return baseOrigin + (Vector2)transform.TransformVector(offset);
        }

        private bool TryGetCurrentGrapplePoint(out Vector2 point)
        {
            if (grappleTarget == null)
            {
                point = default;
                return false;
            }

            point = grappleTarget.transform.TransformPoint(grappleLocalPoint);
            return true;
        }

        private bool TryGetCurrentPulledObjectPoint(out Vector2 point)
        {
            if (pulledGrappleTarget == null)
            {
                point = default;
                return false;
            }

            point = pulledGrappleTarget.transform.TransformPoint(pulledGrappleLocalPoint);
            return true;
        }

        private bool TryGetVisibleGrappleEndPoint(out Vector2 point)
        {
            if (isPullingGrappleObject)
            {
                return TryGetCurrentPulledObjectPoint(out point);
            }

            return TryGetCurrentGrapplePoint(out point);
        }

        private bool CheckGrounded()
        {
            Bounds bounds = bodyCollider.bounds;
            float inset = bounds.size.x * 0.12f;
            float left = bounds.min.x + inset;
            float center = bounds.center.x;
            float right = bounds.max.x - inset;
            float y = bounds.min.y + 0.02f;

            return IsGroundBelow(new Vector2(left, y))
                || IsGroundBelow(new Vector2(center, y))
                || IsGroundBelow(new Vector2(right, y));
        }

        private bool IsGroundBelow(Vector2 origin)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance + 0.04f, groundMask);
            return hit.collider != null && hit.normal.y >= groundNormalThreshold;
        }

        private bool CheckWallBeside()
        {
            return TryGetWallSide(out int _);
        }

        private bool TryGetWallSide(out int side)
        {
            return TryGetWallBeside(out side, out Collider2D _);
        }

        private bool TryGetWallBeside(out int side, out Collider2D wall)
        {
            Bounds bounds = bodyCollider.bounds;
            float rightX = bounds.max.x - 0.02f;
            float leftX = bounds.min.x + 0.02f;
            float bottom = bounds.min.y + bounds.size.y * 0.2f;
            float center = bounds.center.y;
            float top = bounds.max.y - bounds.size.y * 0.1f;

            if (TryGetWallBesideFrom(new Vector2(rightX, bottom), Vector2.right, out wall)
                || TryGetWallBesideFrom(new Vector2(rightX, center), Vector2.right, out wall)
                || TryGetWallBesideFrom(new Vector2(rightX, top), Vector2.right, out wall))
            {
                side = 1;
                return true;
            }

            if (TryGetWallBesideFrom(new Vector2(leftX, bottom), Vector2.left, out wall)
                || TryGetWallBesideFrom(new Vector2(leftX, center), Vector2.left, out wall)
                || TryGetWallBesideFrom(new Vector2(leftX, top), Vector2.left, out wall))
            {
                side = -1;
                return true;
            }

            side = 0;
            wall = null;
            return false;
        }

        private bool IsWallBeside(Vector2 origin, Vector2 direction)
        {
            return TryGetWallBesideFrom(origin, direction, out Collider2D _);
        }

        private bool TryGetWallBesideFrom(Vector2 origin, Vector2 direction, out Collider2D wall)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, wallCheckDistance + 0.04f, groundMask);
            if (hit.collider == null)
            {
                wall = null;
                return false;
            }

            bool isWall = direction.x > 0f
                ? hit.normal.x <= -groundNormalThreshold
                : hit.normal.x >= groundNormalThreshold;
            wall = isWall ? hit.collider : null;
            return isWall;
        }

        private bool IsRunBlockedAhead()
        {
            return TryGetRunBlocker(out Collider2D _);
        }

        private bool TryGetRunBlocker(out Collider2D blocker)
        {
            blocker = null;
            Bounds bounds = bodyCollider.bounds;
            float rightX = bounds.max.x - 0.02f;
            float bottom = bounds.min.y + bounds.size.y * 0.2f;
            float center = bounds.center.y;
            float top = bounds.max.y - bounds.size.y * 0.1f;

            bool hasBlocker = false;
            float bestDistance = float.PositiveInfinity;
            if (TryGetRunBlockerFrom(new Vector2(rightX, bottom), out RaycastHit2D bottomHit) && bottomHit.distance < bestDistance)
            {
                blocker = bottomHit.collider;
                bestDistance = bottomHit.distance;
                hasBlocker = true;
            }

            if (TryGetRunBlockerFrom(new Vector2(rightX, center), out RaycastHit2D centerHit) && centerHit.distance < bestDistance)
            {
                blocker = centerHit.collider;
                bestDistance = centerHit.distance;
                hasBlocker = true;
            }

            if (TryGetRunBlockerFrom(new Vector2(rightX, top), out RaycastHit2D topHit) && topHit.distance < bestDistance)
            {
                blocker = topHit.collider;
                hasBlocker = true;
            }

            return hasBlocker;
        }

        private bool TryGetRunBlockerFrom(Vector2 origin, out RaycastHit2D blockerHit)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right, runBlockedCheckDistance, grappleMask);
            RaycastHit2D bestHit = default;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null || hit.collider == bodyCollider || hit.collider.isTrigger || IsIgnoredGrappleTarget(hit.collider))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestHit = hit;
                    bestDistance = hit.distance;
                }
            }

            bool isBlocked = bestHit.collider != null && bestHit.normal.x <= -groundNormalThreshold;
            blockerHit = isBlocked ? bestHit : default;
            return isBlocked;
        }

        private bool IsTargetOnSameSideAsBlocker(Collider2D target, Collider2D blocker)
        {
            if (target == null || blocker == null)
            {
                return false;
            }

            float playerX = GetGrappleOrigin().x;
            float targetOffset = target.bounds.center.x - playerX;
            float blockerOffset = blocker.bounds.center.x - playerX;
            if (Mathf.Abs(targetOffset) <= 0.01f || Mathf.Abs(blockerOffset) <= 0.01f)
            {
                return false;
            }

            return Mathf.Sign(targetOffset) == Mathf.Sign(blockerOffset);
        }

        private void WrapAtMapEdges()
        {
            if (!wrapAtMapEdges || body.position.x < wrapRightX)
            {
                return;
            }

            StopGrapple();
            StopPullGrappleObject();
            StopStrikeObject();
            StopGrappleAim();
            StopStrikeAim();
            StopClimb();
            isForcedWallSliding = false;
            isWallJumpControlling = false;
            Vector2 velocity = body.velocity;
            body.position = new Vector2(wrapLeftX, body.position.y);
            body.velocity = velocity;
            Physics2D.SyncTransforms();
            UpdateGrappleLine();
        }

        private void OnValidate()
        {
            if (wrapRightX <= wrapLeftX)
            {
                wrapRightX = wrapLeftX + 1f;
            }

            wallCheckDistance = Mathf.Max(0.01f, wallCheckDistance);
            wallSlideFallSpeedMultiplier = Mathf.Clamp(wallSlideFallSpeedMultiplier, 0.05f, 1f);
            wallSlideMaxFallSpeed = Mathf.Max(0.1f, wallSlideMaxFallSpeed);
            wallJumpHorizontalSpeed = Mathf.Max(0.1f, wallJumpHorizontalSpeed);
            wallJumpVerticalSpeed = Mathf.Max(0.1f, wallJumpVerticalSpeed);
            attackHitSize.x = Mathf.Max(0.1f, attackHitSize.x);
            attackHitSize.y = Mathf.Max(0.1f, attackHitSize.y);
            grappleAimRadius = Mathf.Max(0.1f, grappleAimRadius);
            grappleRopeWidth = Mathf.Max(0.001f, grappleRopeWidth);
            grappleRopeSegmentLength = Mathf.Max(0.01f, grappleRopeSegmentLength);
            grappleAimMoveSpeedMultiplier = Mathf.Clamp(grappleAimMoveSpeedMultiplier, 0.01f, 1f);
            grappleObjectPullSpeed = Mathf.Max(0.1f, grappleObjectPullSpeed);
            grappleClimbAnimationDuration = Mathf.Max(0f, grappleClimbAnimationDuration);
            strikeAimRadius = Mathf.Max(0.1f, strikeAimRadius);
            strikeObjectSpeed = Mathf.Max(0.1f, strikeObjectSpeed);
            anchorScale = Mathf.Max(0.001f, anchorScale);
            runBlockedCheckDistance = Mathf.Max(0.01f, runBlockedCheckDistance);
            grappleAimCircleSegments = Mathf.Max(12, grappleAimCircleSegments);
        }

        private void CacheInitialPulledObjectOverlaps(Collider2D target)
        {
            initialPulledObjectOverlaps.Clear();
            int overlapCount = target.OverlapCollider(CreatePulledObjectContactFilter(), pulledObjectOverlapResults);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D overlap = pulledObjectOverlapResults[i];
                if (overlap != null && overlap != target && overlap != bodyCollider)
                {
                    initialPulledObjectOverlaps.Add(overlap);
                }
            }
        }

        private bool HasPulledObjectHitNewCollider()
        {
            if (pulledGrappleTarget == null)
            {
                return true;
            }

            int overlapCount = pulledGrappleTarget.OverlapCollider(CreatePulledObjectContactFilter(), pulledObjectOverlapResults);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D overlap = pulledObjectOverlapResults[i];
                if (overlap == null || overlap == pulledGrappleTarget)
                {
                    continue;
                }

                if (ShouldIgnoreStepPassengerCollider(pulledGrappleTarget, overlap))
                {
                    continue;
                }

                if (overlap != bodyCollider && initialPulledObjectOverlaps.Contains(overlap))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool TryClipPulledObjectMovement(Vector2 requestedMovement, out Vector2 clippedMovement)
        {
            clippedMovement = requestedMovement;
            if (pulledGrappleTarget == null)
            {
                return true;
            }

            float requestedDistance = requestedMovement.magnitude;
            if (requestedDistance <= 0.0001f)
            {
                clippedMovement = Vector2.zero;
                return false;
            }

            Vector2 direction = requestedMovement / requestedDistance;
            if (IsMovingIntoInitialPulledObjectOverlap(direction))
            {
                clippedMovement = Vector2.zero;
                return true;
            }

            int hitCount = pulledGrappleTarget.Cast(
                direction,
                CreatePulledObjectContactFilter(),
                pulledObjectCastResults,
                requestedDistance + PulledObjectSkinWidth);

            float nearestDistance = float.PositiveInfinity;
            bool hasBlockingHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = pulledObjectCastResults[i];
                if (hit.collider == null || hit.collider == pulledGrappleTarget)
                {
                    continue;
                }

                if (ShouldIgnoreStepPassengerCollider(pulledGrappleTarget, hit.collider))
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    hasBlockingHit = true;
                }
            }

            if (!hasBlockingHit)
            {
                return false;
            }

            float safeDistance = Mathf.Max(0f, nearestDistance - PulledObjectSkinWidth);
            clippedMovement = direction * Mathf.Min(requestedDistance, safeDistance);
            return true;
        }

        private bool IsMovingIntoInitialPulledObjectOverlap(Vector2 direction)
        {
            if (pulledGrappleTarget == null)
            {
                return true;
            }

            Bounds pulledBounds = pulledGrappleTarget.bounds;
            Vector2 pulledCenter = pulledBounds.center;
            for (int i = initialPulledObjectOverlaps.Count - 1; i >= 0; i--)
            {
                Collider2D overlap = initialPulledObjectOverlaps[i];
                if (overlap == null)
                {
                    initialPulledObjectOverlaps.RemoveAt(i);
                    continue;
                }

                if (ShouldIgnoreStepPassengerCollider(pulledGrappleTarget, overlap))
                {
                    initialPulledObjectOverlaps.RemoveAt(i);
                    continue;
                }

                ColliderDistance2D distance = pulledGrappleTarget.Distance(overlap);
                if (!distance.isOverlapped && distance.distance > PulledObjectSkinWidth * 2f)
                {
                    initialPulledObjectOverlaps.RemoveAt(i);
                    continue;
                }

                Vector2 closestPoint = overlap.ClosestPoint(pulledCenter);
                Vector2 toOverlap = closestPoint - pulledCenter;
                if (toOverlap.sqrMagnitude <= 0.0001f)
                {
                    toOverlap = (Vector2)overlap.bounds.center - pulledCenter;
                }

                if (toOverlap.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                if (Vector2.Dot(direction, toOverlap.normalized) > 0.05f)
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheInitialStruckObjectOverlaps(Collider2D target)
        {
            initialStruckObjectOverlaps.Clear();
            int overlapCount = target.OverlapCollider(CreatePulledObjectContactFilter(), pulledObjectOverlapResults);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D overlap = pulledObjectOverlapResults[i];
                if (overlap != null && overlap != target && overlap != bodyCollider)
                {
                    initialStruckObjectOverlaps.Add(overlap);
                }
            }
        }

        private bool HasStruckObjectHitNewCollider()
        {
            if (struckTarget == null)
            {
                return true;
            }

            int overlapCount = struckTarget.OverlapCollider(CreatePulledObjectContactFilter(), pulledObjectOverlapResults);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D overlap = pulledObjectOverlapResults[i];
                if (overlap == null || overlap == struckTarget)
                {
                    continue;
                }

                if (ShouldIgnoreStepPassengerCollider(struckTarget, overlap))
                {
                    continue;
                }

                if (TryDamageBossWithStruckStep(overlap))
                {
                    return true;
                }

                if (overlap != bodyCollider && initialStruckObjectOverlaps.Contains(overlap))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool TryClipStruckObjectMovement(Vector2 requestedMovement, out Vector2 clippedMovement)
        {
            clippedMovement = requestedMovement;
            if (struckTarget == null)
            {
                return true;
            }

            float requestedDistance = requestedMovement.magnitude;
            if (requestedDistance <= 0.0001f)
            {
                clippedMovement = Vector2.zero;
                return false;
            }

            Vector2 direction = requestedMovement / requestedDistance;
            if (IsMovingIntoInitialStruckObjectOverlap(direction))
            {
                clippedMovement = Vector2.zero;
                return true;
            }

            int hitCount = struckTarget.Cast(
                direction,
                CreatePulledObjectContactFilter(),
                pulledObjectCastResults,
                requestedDistance + PulledObjectSkinWidth);

            float nearestDistance = float.PositiveInfinity;
            bool hasBlockingHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = pulledObjectCastResults[i];
                if (hit.collider == null || hit.collider == struckTarget)
                {
                    continue;
                }

                if (ShouldIgnoreStepPassengerCollider(struckTarget, hit.collider))
                {
                    continue;
                }

                if (TryDamageBossWithStruckStep(hit.collider))
                {
                    nearestDistance = Mathf.Min(nearestDistance, hit.distance);
                    hasBlockingHit = true;
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    hasBlockingHit = true;
                }
            }

            if (!hasBlockingHit)
            {
                return false;
            }

            float safeDistance = Mathf.Max(0f, nearestDistance - PulledObjectSkinWidth);
            clippedMovement = direction * Mathf.Min(requestedDistance, safeDistance);
            return true;
        }

        private bool IsMovingIntoInitialStruckObjectOverlap(Vector2 direction)
        {
            if (struckTarget == null)
            {
                return true;
            }

            Bounds struckBounds = struckTarget.bounds;
            Vector2 struckCenter = struckBounds.center;
            for (int i = initialStruckObjectOverlaps.Count - 1; i >= 0; i--)
            {
                Collider2D overlap = initialStruckObjectOverlaps[i];
                if (overlap == null)
                {
                    initialStruckObjectOverlaps.RemoveAt(i);
                    continue;
                }

                if (ShouldIgnoreStepPassengerCollider(struckTarget, overlap))
                {
                    initialStruckObjectOverlaps.RemoveAt(i);
                    continue;
                }

                ColliderDistance2D distance = struckTarget.Distance(overlap);
                if (!distance.isOverlapped && distance.distance > PulledObjectSkinWidth * 2f)
                {
                    initialStruckObjectOverlaps.RemoveAt(i);
                    continue;
                }

                Vector2 closestPoint = overlap.ClosestPoint(struckCenter);
                Vector2 toOverlap = closestPoint - struckCenter;
                if (toOverlap.sqrMagnitude <= 0.0001f)
                {
                    toOverlap = (Vector2)overlap.bounds.center - struckCenter;
                }

                if (toOverlap.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                if (Vector2.Dot(direction, toOverlap.normalized) > 0.05f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryDamageBossWithStruckStep(Collider2D hitCollider)
        {
            if (!IsGrappleStepTarget(struckTarget) || hitCollider == null || hitCollider == bodyCollider)
            {
                return false;
            }

            BossController2D boss = hitCollider.GetComponentInParent<BossController2D>();
            if (boss == null)
            {
                return false;
            }

            boss.TakeStruckStepDamage();
            return true;
        }

        private static ContactFilter2D CreatePulledObjectContactFilter()
        {
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = true
            };
            return filter;
        }
    }
}
