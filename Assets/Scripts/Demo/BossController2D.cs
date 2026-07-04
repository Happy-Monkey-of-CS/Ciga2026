using System.Collections;
using UnityEngine;

namespace Ciga.Demo
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossController2D : MonoBehaviour
    {
        private const string StepTag = "GrappleStep";
        private const string WallTag = "GrappleWall";
        private const string EnemyTag = "Enemy";
        [Header("Target")]
        [SerializeField] private PlayerController2D player;
        [SerializeField] private LayerMask bossAbilityMask = 1;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2.3f;
        [SerializeField] private float jumpForce = 11.5f;
        [SerializeField] private float gravityScale = 3f;
        [SerializeField] private float maxFallSpeed = 12f;
        [SerializeField] private float decisionInterval = 1.2f;
        [SerializeField] private float groundCheckDistance = 0.12f;
        [SerializeField] private float obstacleCheckDistance = 0.25f;
        [SerializeField] private float ceilingCheckDistance = 0.08f;
        [SerializeField] private float ceilingRecoveryDownVelocity = 2.5f;
        [SerializeField] private float ceilingRecoveryHorizontalDamping = 0.25f;
        [SerializeField] private float jumpHeightThreshold = 0.65f;
        [SerializeField] private float jumpCooldown = 0.45f;

        [Header("Runner Chase")]
        [SerializeField] private float playerRunSpeedEstimate = 4f;
        [SerializeField] private float desiredChaseDistance = 3.5f;
        [SerializeField] private float chaseDistanceDeadZone = 0.45f;
        [SerializeField] private float catchUpDistance = 7f;
        [SerializeField] private float catchUpSpeedBonus = 3f;
        [SerializeField] private float maxChaseSpeed = 9f;
        [SerializeField] private float burstCatchUpDistance = 10f;
        [SerializeField] private float burstSpeed = 11f;
        [SerializeField] private float burstDuration = 0.55f;
        [SerializeField] private float burstCooldown = 2.2f;
        [SerializeField] private float emergencyRepositionGap = 15f;
        [SerializeField] private float emergencyRepositionDistance = 5.5f;
        [SerializeField] private float emergencyVerticalGap = 7f;
        [SerializeField] private float repositionYOffset = 0.8f;

        [Header("Melee")]
        [SerializeField] private float meleeRange = 1.35f;
        [SerializeField] private float meleeWindup = 0.35f;
        [SerializeField] private float meleeCooldown = 1.4f;
        [SerializeField] private float contactAttackRecovery = 0.25f;

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float playerAttackDamage = 20f;
        [SerializeField] private float struckStepDamage = 35f;
        [SerializeField] private float hitFlashDuration = 0.12f;
        [SerializeField] private float deathDestroyDelay = 1.2f;

        [Header("Grapple")]
        [SerializeField] private float grappleRange = 8f;
        [SerializeField] private float grappleMoveSpeed = 11f;
        [SerializeField] private float grappleStopDistance = 0.75f;
        [SerializeField] private float grappleStepLandingSkin = 0.04f;
        [SerializeField] private float grappleCooldown = 2f;

        [Header("Object Abilities")]
        [SerializeField] private float objectAbilityRange = 7f;
        [SerializeField] private float pullObjectSpeed = 6f;
        [SerializeField] private float strikeObjectSpeed = 10f;
        [SerializeField] private float objectMoveMaxDuration = 0.9f;
        [SerializeField] private float objectAbilityCooldown = 2.2f;

        [Header("Visuals")]
        [SerializeField] private Color grappleColor = new Color(0.9f, 0.2f, 1f, 0.95f);
        [SerializeField] private Color pullColor = new Color(0.25f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color strikeColor = new Color(1f, 0.35f, 0.15f, 0.95f);

        private static readonly int BossIsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int BossAttackHash = Animator.StringToHash("Attack");
        private static readonly int BossDeathHash = Animator.StringToHash("Death");

        private readonly RaycastHit2D[] objectCastResults = new RaycastHit2D[12];
        private readonly Collider2D[] targetOverlapResults = new Collider2D[24];
        private readonly Collider2D[] ignoredGrappleStepColliders = new Collider2D[96];

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private LineRenderer abilityLine;
        private bool isBusy;
        private bool isGrounded;
        private bool isBurstCatchingUp;
        private bool isContactAttacking;
        private bool isDefeated;
        private float currentHealth;
        private Coroutine hitFlashRoutine;
        private Color originalColor = Color.white;
        private float nextDecisionTime;
        private float nextJumpTime;
        private float nextMeleeTime;
        private float nextGrappleTime;
        private float nextBurstTime;
        private float nextObjectAbilityTime;
        private float nextEnemyCollisionRefreshTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            abilityLine = GetComponent<LineRenderer>();

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            if (abilityLine != null)
            {
                abilityLine.enabled = false;
                abilityLine.positionCount = 2;
                abilityLine.useWorldSpace = true;
            }

            currentHealth = maxHealth;
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
        }

        private void Start()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController2D>();
            }

            RefreshIgnoredEnemyCollisions();
        }

        private void Update()
        {
            if (player == null || isBusy)
            {
                UpdateAnimator(false);
                return;
            }

            float playerDeltaX = player.transform.position.x - transform.position.x;
            if (Mathf.Abs(playerDeltaX) <= desiredChaseDistance + 0.5f)
            {
                spriteRenderer.flipX = playerDeltaX < 0f;
            }
            else
            {
                spriteRenderer.flipX = false;
            }

            if (Time.time < nextDecisionTime)
            {
                bool shouldChase = body != null && Mathf.Abs(body.velocity.x) > 0.1f;
                UpdateAnimator(shouldChase);
                return;
            }

            nextDecisionTime = Time.time + decisionInterval;
            DecideNextAction();
        }

        private void FixedUpdate()
        {
            UpdateGrounded();
            ClampFallSpeed();
            ResolveCeilingHit();
            RefreshIgnoredEnemyCollisionsIfNeeded();

            if (player == null || isBusy)
            {
                if (isBusy && isGrounded)
                {
                    body.velocity = new Vector2(0f, body.velocity.y);
                }

                return;
            }

            Vector2 toPlayer = player.transform.position - transform.position;
            if (ShouldEmergencyReposition(toPlayer))
            {
                RepositionBehindPlayer();
                return;
            }

            body.velocity = new Vector2(CalculateChaseSpeed(toPlayer.x), body.velocity.y);

            if (ShouldJumpForTraversal(1f, toPlayer))
            {
                Jump();
            }

            UpdateAnimator(Mathf.Abs(body.velocity.x) > 0.1f);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            AttackPlayerIfTouched(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            AttackPlayerIfTouched(collision.collider);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            AttackPlayerIfTouched(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            AttackPlayerIfTouched(other);
        }

        private void DecideNextAction()
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);
            float horizontalGap = player.transform.position.x - transform.position.x;
            if (horizontalGap >= -0.25f && horizontalGap <= meleeRange + 0.35f && distanceToPlayer <= meleeRange + 0.7f && Time.time >= nextMeleeTime)
            {
                StartCoroutine(MeleeAttack());
                return;
            }

            if (Time.time >= nextObjectAbilityTime && TryGetStepTarget(out Collider2D objectTarget))
            {
                bool shouldPull = player.transform.position.y > transform.position.y + 1.2f || Random.value < 0.45f;
                StartCoroutine(shouldPull ? PullObject(objectTarget) : StrikeObject(objectTarget));
                return;
            }

            if (Time.time >= nextGrappleTime && ShouldUseGrappleCatchUp(horizontalGap) && TryGetGrappleTarget(out Collider2D grappleTarget))
            {
                StartCoroutine(GrappleTo(grappleTarget));
                return;
            }

            if (Time.time >= nextBurstTime && horizontalGap > burstCatchUpDistance)
            {
                StartCoroutine(BurstCatchUp());
            }
        }

        private IEnumerator MeleeAttack()
        {
            isBusy = true;
            nextMeleeTime = Time.time + meleeCooldown;
            body.velocity = new Vector2(0f, body.velocity.y);
            UpdateAnimator(false);
            TriggerAttackAnimation();

            yield return new WaitForSeconds(meleeWindup);

            if (player != null && Vector2.Distance(transform.position, player.transform.position) <= meleeRange + 0.35f)
            {
                player.Kill();
            }

            yield return new WaitForSeconds(0.25f);
            isBusy = false;
        }

        private IEnumerator BurstCatchUp()
        {
            isBusy = true;
            isBurstCatchingUp = true;
            nextBurstTime = Time.time + burstCooldown;
            float elapsed = 0f;

            while (elapsed < burstDuration && player != null)
            {
                UpdateGrounded();
                ClampFallSpeed();
                Vector2 toPlayer = player.transform.position - transform.position;
                if (ShouldEmergencyReposition(toPlayer))
                {
                    RepositionBehindPlayer();
                    break;
                }

                body.velocity = new Vector2(burstSpeed, body.velocity.y);
                if (ShouldJumpForTraversal(1f, toPlayer))
                {
                    Jump();
                }

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            isBurstCatchingUp = false;
            isBusy = false;
        }

        private IEnumerator GrappleTo(Collider2D target)
        {
            isBusy = true;
            nextGrappleTime = Time.time + grappleCooldown;
            SetLine(grappleColor);
            float previousGravityScale = body.gravityScale;
            body.gravityScale = 0f;
            body.velocity = Vector2.zero;
            int ignoredStepCount = BeginIgnoreStepCollisionsForGrapple();

            while (target != null)
            {
                Vector2 anchor = GetGrappleAnchor(target);
                Vector2 destination = GetGrappleBodyDestination(target);
                Vector2 toAnchor = destination - body.position;
                DrawLine(transform.position, anchor);
                if (toAnchor.magnitude <= grappleStopDistance)
                {
                    break;
                }

                Vector2 movement = toAnchor.normalized * grappleMoveSpeed * Time.fixedDeltaTime;
                if (movement.magnitude > toAnchor.magnitude)
                {
                    movement = toAnchor;
                }

                body.MovePosition(body.position + movement);
                yield return new WaitForFixedUpdate();
            }

            body.velocity = Vector2.zero;
            if (target != null && target.CompareTag(StepTag))
            {
                body.position = GetStepTopBodyPosition(target);
                Physics2D.SyncTransforms();
            }

            EndIgnoreStepCollisionsForGrapple(ignoredStepCount);

            body.gravityScale = previousGravityScale;
            HideLine();
            isBusy = false;
        }

        private int BeginIgnoreStepCollisionsForGrapple()
        {
            int ignoredCount = 0;
            GameObject[] stepObjects = GameObject.FindGameObjectsWithTag(StepTag);
            for (int i = 0; i < stepObjects.Length && ignoredCount < ignoredGrappleStepColliders.Length; i++)
            {
                GameObject stepObject = stepObjects[i];
                if (stepObject == null)
                {
                    continue;
                }

                Collider2D[] colliders = stepObject.GetComponentsInChildren<Collider2D>();
                for (int j = 0; j < colliders.Length && ignoredCount < ignoredGrappleStepColliders.Length; j++)
                {
                    Collider2D stepCollider = colliders[j];
                    if (stepCollider == null || stepCollider == bodyCollider)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(bodyCollider, stepCollider, true);
                    ignoredGrappleStepColliders[ignoredCount] = stepCollider;
                    ignoredCount++;
                }
            }

            return ignoredCount;
        }

        private void EndIgnoreStepCollisionsForGrapple(int ignoredCount)
        {
            for (int i = 0; i < ignoredCount && i < ignoredGrappleStepColliders.Length; i++)
            {
                Collider2D stepCollider = ignoredGrappleStepColliders[i];
                if (stepCollider != null)
                {
                    Physics2D.IgnoreCollision(bodyCollider, stepCollider, false);
                }

                ignoredGrappleStepColliders[i] = null;
            }
        }

        private void RefreshIgnoredEnemyCollisionsIfNeeded()
        {
            if (Time.time < nextEnemyCollisionRefreshTime)
            {
                return;
            }

            nextEnemyCollisionRefreshTime = Time.time + 0.5f;
            RefreshIgnoredEnemyCollisions();
        }

        private void RefreshIgnoredEnemyCollisions()
        {
            GameObject[] enemyObjects = GameObject.FindGameObjectsWithTag(EnemyTag);
            for (int i = 0; i < enemyObjects.Length; i++)
            {
                GameObject enemyObject = enemyObjects[i];
                if (enemyObject == null)
                {
                    continue;
                }

                Collider2D[] colliders = enemyObject.GetComponentsInChildren<Collider2D>();
                for (int j = 0; j < colliders.Length; j++)
                {
                    Collider2D enemyCollider = colliders[j];
                    if (enemyCollider == null || enemyCollider == bodyCollider)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(bodyCollider, enemyCollider, true);
                }
            }
        }

        private IEnumerator PullObject(Collider2D target)
        {
            isBusy = true;
            nextObjectAbilityTime = Time.time + objectAbilityCooldown;
            SetLine(pullColor);
            StepMover2D lockedStepMover = target != null ? target.GetComponent<StepMover2D>() : null;
            if (lockedStepMover != null)
            {
                lockedStepMover.BeginExternalMovement();
            }

            float elapsed = 0f;
            while (target != null && elapsed < objectMoveMaxDuration)
            {
                DrawLine(transform.position, target.bounds.center);
                Vector2 direction = ((Vector2)transform.position - (Vector2)target.bounds.center).normalized;
                if (!MoveObjectWithFallback(target, direction, pullObjectSpeed * Time.fixedDeltaTime, false))
                {
                    break;
                }

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (lockedStepMover != null)
            {
                lockedStepMover.EndExternalMovement();
            }

            HideLine();
            isBusy = false;
        }

        private IEnumerator StrikeObject(Collider2D target)
        {
            isBusy = true;
            nextObjectAbilityTime = Time.time + objectAbilityCooldown;
            SetLine(strikeColor);
            TriggerAttackAnimation();
            StepMover2D lockedStepMover = target != null ? target.GetComponent<StepMover2D>() : null;
            if (lockedStepMover != null)
            {
                lockedStepMover.BeginExternalMovement();
            }

            Vector2 direction = ((Vector2)target.bounds.center - (Vector2)transform.position).normalized;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = spriteRenderer.flipX ? Vector2.left : Vector2.right;
            }

            float elapsed = 0f;
            while (target != null && elapsed < objectMoveMaxDuration)
            {
                DrawLine(transform.position, target.bounds.center);
                if (!MoveObject(target, direction.normalized * strikeObjectSpeed * Time.fixedDeltaTime, true))
                {
                    break;
                }

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (lockedStepMover != null)
            {
                lockedStepMover.EndExternalMovement();
            }

            HideLine();
            isBusy = false;
        }

        private bool MoveObjectWithFallback(Collider2D target, Vector2 direction, float distance, bool carryPlayerHorizontally)
        {
            if (direction.sqrMagnitude <= 0.0001f || distance <= 0f)
            {
                return false;
            }

            Vector2 movement = direction.normalized * distance;
            if (MoveObject(target, movement, carryPlayerHorizontally))
            {
                return true;
            }

            Vector2 horizontalMovement = new Vector2(Mathf.Sign(direction.x) * Mathf.Abs(movement.x), 0f);
            if (horizontalMovement.sqrMagnitude > 0.000001f && MoveObject(target, horizontalMovement, carryPlayerHorizontally))
            {
                return true;
            }

            Vector2 verticalMovement = new Vector2(0f, Mathf.Sign(direction.y) * Mathf.Abs(movement.y));
            return verticalMovement.sqrMagnitude > 0.000001f && MoveObject(target, verticalMovement, carryPlayerHorizontally);
        }

        private bool MoveObject(Collider2D target, Vector2 movement, bool carryPlayerHorizontally)
        {
            if (target == null || movement.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            Rigidbody2D targetBody = target.attachedRigidbody;
            if (targetBody == null)
            {
                return false;
            }

            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = bossAbilityMask,
            };

            Vector2 direction = movement.normalized;
            int hitCount = target.Cast(direction, filter, objectCastResults, movement.magnitude + 0.02f);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = objectCastResults[i].collider;
                if (hitCollider == null || hitCollider == target || hitCollider == bodyCollider)
                {
                    continue;
                }

                if (hitCollider.transform.IsChildOf(target.transform))
                {
                    continue;
                }

                if (IsPassengerOnMovedStep(hitCollider, target))
                {
                    continue;
                }

                return false;
            }

            CarryPassengersOnMovedStep(target, movement, carryPlayerHorizontally);
            if (target.CompareTag(StepTag))
            {
                target.transform.position += (Vector3)movement;
                Physics2D.SyncTransforms();
            }
            else
            {
                targetBody.MovePosition(targetBody.position + movement);
            }

            return true;
        }

        private bool IsPassengerOnMovedStep(Collider2D passengerCollider, Collider2D target)
        {
            if (target == null || !target.CompareTag(StepTag) || passengerCollider == null)
            {
                return false;
            }

            PlayerController2D passengerPlayer = passengerCollider.GetComponentInParent<PlayerController2D>();
            if (passengerPlayer != null)
            {
                Collider2D playerCollider = passengerPlayer.GetComponent<Collider2D>();
                return playerCollider != null && IsStandingOnMovedStep(playerCollider, target);
            }

            Enemy2D passengerEnemy = passengerCollider.GetComponentInParent<Enemy2D>();
            return passengerEnemy != null && passengerEnemy.IsStandingOn(target);
        }

        private void CarryPassengersOnMovedStep(Collider2D target, Vector2 movement, bool carryPlayerHorizontally)
        {
            if (target == null || !target.CompareTag(StepTag) || movement.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D passengerPlayer = players[i];
                if (passengerPlayer == null)
                {
                    continue;
                }

                Collider2D playerCollider = passengerPlayer.GetComponent<Collider2D>();
                if (playerCollider != null && IsStandingOnMovedStep(playerCollider, target))
                {
                    Vector2 playerCarry = carryPlayerHorizontally ? movement : new Vector2(0f, movement.y);
                    passengerPlayer.CarryByMovingStep(playerCarry);
                }
            }

            Enemy2D[] enemies = FindObjectsByType<Enemy2D>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy2D passengerEnemy = enemies[i];
                if (passengerEnemy != null && passengerEnemy.IsStandingOn(target))
                {
                    passengerEnemy.CarryByMovingStep(movement);
                }
            }
        }

        private bool IsStandingOnMovedStep(Collider2D passenger, Collider2D step)
        {
            Bounds passengerBounds = passenger.bounds;
            Bounds stepBounds = step.bounds;
            bool horizontallyOverlaps = passengerBounds.max.x > stepBounds.min.x + 0.02f
                && passengerBounds.min.x < stepBounds.max.x - 0.02f;
            bool isNearPlatformTop = passengerBounds.min.y >= stepBounds.max.y - 0.25f
                && passengerBounds.min.y <= stepBounds.max.y + 0.4f;
            return horizontallyOverlaps && isNearPlatformTop;
        }

        private bool TryGetGrappleTarget(out Collider2D target)
        {
            target = null;
            float bestScore = float.MinValue;
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, grappleRange, targetOverlapResults, bossAbilityMask);
            for (int i = 0; i < count; i++)
            {
                Collider2D candidate = targetOverlapResults[i];
                if (!IsGrappleTarget(candidate))
                {
                    continue;
                }

                float candidateX = candidate.bounds.center.x;
                if (candidateX < transform.position.x + 0.75f || candidateX > player.transform.position.x + 9f)
                {
                    continue;
                }

                float heightBonus = candidate.bounds.center.y - transform.position.y;
                float playerSideBonus = candidateX <= player.transform.position.x + 1.5f ? 1.25f : 0f;
                float catchUpBonus = player.transform.position.x - transform.position.x > catchUpDistance ? 2f : 0f;
                float distancePenalty = Vector2.Distance(transform.position, candidate.bounds.center) * 0.2f;
                float score = heightBonus + playerSideBonus + catchUpBonus - distancePenalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    target = candidate;
                }
            }

            return target != null;
        }

        private bool TryGetStepTarget(out Collider2D target)
        {
            target = null;
            float bestScore = float.MaxValue;
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, objectAbilityRange, targetOverlapResults, bossAbilityMask);
            for (int i = 0; i < count; i++)
            {
                Collider2D candidate = targetOverlapResults[i];
                if (candidate == null || !candidate.CompareTag(StepTag))
                {
                    continue;
                }

                float candidateX = candidate.bounds.center.x;
                if (candidateX < player.transform.position.x - 1.5f || candidateX > player.transform.position.x + 8.5f)
                {
                    continue;
                }

                float distanceToPlayer = Vector2.Distance(candidate.bounds.center, player.transform.position);
                float distanceToBoss = Vector2.Distance(candidate.bounds.center, transform.position);
                float score = distanceToPlayer + distanceToBoss * 0.35f;
                if (score < bestScore)
                {
                    bestScore = score;
                    target = candidate;
                }
            }

            return target != null;
        }

        private float CalculateChaseSpeed(float horizontalGap)
        {
            if (horizontalGap < desiredChaseDistance - chaseDistanceDeadZone)
            {
                float nearT = Mathf.InverseLerp(-1f, desiredChaseDistance - chaseDistanceDeadZone, horizontalGap);
                return Mathf.Lerp(playerRunSpeedEstimate * 0.35f, playerRunSpeedEstimate * 0.85f, nearT);
            }

            float speed = Mathf.Max(moveSpeed, playerRunSpeedEstimate);
            if (horizontalGap > catchUpDistance)
            {
                float catchUpT = Mathf.InverseLerp(catchUpDistance, emergencyRepositionGap, horizontalGap);
                speed += Mathf.Lerp(catchUpSpeedBonus * 0.35f, catchUpSpeedBonus, catchUpT);
            }

            return Mathf.Min(speed, maxChaseSpeed);
        }

        private bool ShouldUseGrappleCatchUp(float horizontalGap)
        {
            return horizontalGap > catchUpDistance || player.transform.position.y > transform.position.y + 1.4f;
        }

        private bool ShouldEmergencyReposition(Vector2 toPlayer)
        {
            return toPlayer.x > emergencyRepositionGap || Mathf.Abs(toPlayer.y) > emergencyVerticalGap;
        }

        private void RepositionBehindPlayer()
        {
            Vector2 playerPosition = player.transform.position;
            body.position = new Vector2(playerPosition.x - emergencyRepositionDistance, playerPosition.y + repositionYOffset);
            body.velocity = new Vector2(playerRunSpeedEstimate, 0f);
            body.gravityScale = gravityScale;
            Physics2D.SyncTransforms();
            nextGrappleTime = Time.time + 0.6f;
            nextBurstTime = Time.time + 0.8f;
            nextJumpTime = Time.time + 0.25f;
        }

        private bool IsGrappleTarget(Collider2D candidate)
        {
            return candidate != null && (candidate.CompareTag(StepTag) || candidate.CompareTag(WallTag));
        }

        private Vector2 GetGrappleAnchor(Collider2D target)
        {
            if (target != null && target.CompareTag(StepTag))
            {
                Vector2 topBodyPosition = GetStepTopBodyPosition(target);
                return new Vector2(topBodyPosition.x, target.bounds.max.y);
            }

            return target != null ? target.ClosestPoint(body.position) : body.position;
        }

        private Vector2 GetGrappleBodyDestination(Collider2D target)
        {
            if (target != null && target.CompareTag(StepTag))
            {
                return GetStepTopBodyPosition(target);
            }

            return target != null ? target.ClosestPoint(body.position) : body.position;
        }

        private Vector2 GetStepTopBodyPosition(Collider2D step)
        {
            Bounds stepBounds = step.bounds;
            Bounds bossBounds = bodyCollider.bounds;
            float halfWidth = bossBounds.extents.x;
            float minX = stepBounds.min.x + halfWidth + grappleStepLandingSkin;
            float maxX = stepBounds.max.x - halfWidth - grappleStepLandingSkin;
            float desiredX = minX <= maxX
                ? Mathf.Clamp(body.position.x, minX, maxX)
                : stepBounds.center.x;
            float desiredY = body.position.y + (stepBounds.max.y + grappleStepLandingSkin - bossBounds.min.y);
            return new Vector2(desiredX, desiredY);
        }

        private void UpdateGrounded()
        {
            Bounds bounds = bodyCollider.bounds;
            float inset = bounds.size.x * 0.12f;
            float left = bounds.min.x + inset;
            float center = bounds.center.x;
            float right = bounds.max.x - inset;
            float y = bounds.min.y + 0.02f;
            isGrounded = IsGroundBelow(new Vector2(left, y))
                || IsGroundBelow(new Vector2(center, y))
                || IsGroundBelow(new Vector2(right, y));
        }

        private bool IsGroundBelow(Vector2 origin)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance + 0.04f, bossAbilityMask);
            return hit.collider != null && hit.collider != bodyCollider && !hit.collider.isTrigger && hit.normal.y >= 0.65f;
        }

        private bool ShouldJumpForTraversal(float direction, Vector2 toPlayer)
        {
            if (!isGrounded || Time.time < nextJumpTime)
            {
                return false;
            }

            if (IsCeilingBlocked())
            {
                return false;
            }

            if (toPlayer.y > jumpHeightThreshold && Mathf.Abs(toPlayer.x) > 0.45f)
            {
                return true;
            }

            Bounds bounds = bodyCollider.bounds;
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + bounds.size.y * 0.45f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * direction, bounds.extents.x + obstacleCheckDistance, bossAbilityMask);
            return hit.collider != null
                && hit.collider != bodyCollider
                && !hit.collider.isTrigger
                && !hit.collider.GetComponent<PlayerController2D>();
        }

        private void ResolveCeilingHit()
        {
            if (isGrounded || !IsCeilingBlocked())
            {
                return;
            }

            nextJumpTime = Time.time + jumpCooldown;
            float horizontalVelocity = body.velocity.x * Mathf.Clamp01(ceilingRecoveryHorizontalDamping);
            float verticalVelocity = Mathf.Min(body.velocity.y, -ceilingRecoveryDownVelocity);
            body.velocity = new Vector2(horizontalVelocity, verticalVelocity);
        }

        private bool IsCeilingBlocked()
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 size = new Vector2(Mathf.Max(0.05f, bounds.size.x * 0.75f), 0.05f);
            Vector2 origin = new Vector2(bounds.center.x, bounds.max.y - 0.02f);
            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.up, ceilingCheckDistance, bossAbilityMask);
            return hit.collider != null && hit.collider != bodyCollider && !hit.collider.isTrigger;
        }

        private void Jump()
        {
            nextJumpTime = Time.time + jumpCooldown;
            body.velocity = new Vector2(body.velocity.x, jumpForce);
            isGrounded = false;
            if (animator != null)
            {
                animator.SetTrigger(Animator.StringToHash("Jump"));
            }
        }

        private void ClampFallSpeed()
        {
            if (body.velocity.y < -maxFallSpeed)
            {
                body.velocity = new Vector2(body.velocity.x, -maxFallSpeed);
            }
        }

        private void AttackPlayerIfTouched(Collider2D other)
        {
            if (other == null || player == null || isContactAttacking || Time.time < nextMeleeTime || other.GetComponentInParent<PlayerController2D>() == null)
            {
                return;
            }

            StartCoroutine(ContactAttackPlayer());
        }

        private IEnumerator ContactAttackPlayer()
        {
            isBusy = true;
            isContactAttacking = true;
            nextMeleeTime = Time.time + meleeCooldown;
            body.velocity = new Vector2(0f, body.velocity.y);
            UpdateAnimator(false);
            TriggerAttackAnimation();

            yield return new WaitForSeconds(meleeWindup);

            if (player != null && Vector2.Distance(transform.position, player.transform.position) <= meleeRange + 0.9f)
            {
                player.Kill();
            }

            yield return new WaitForSeconds(contactAttackRecovery);
            isContactAttacking = false;
            isBusy = false;
        }

        public float PlayerAttackDamage => playerAttackDamage;
        public float StruckStepDamage => struckStepDamage;

        public void TakePlayerAttackDamage()
        {
            TakeDamage(playerAttackDamage);
        }

        public void TakeStruckStepDamage()
        {
            TakeDamage(struckStepDamage);
        }

        public void TakeDamage(float damage)
        {
            if (isDefeated || damage <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            if (hitFlashRoutine != null)
            {
                StopCoroutine(hitFlashRoutine);
            }

            hitFlashRoutine = StartCoroutine(FlashHitColor());

            if (currentHealth <= 0f)
            {
                Defeat();
            }
        }

        private IEnumerator FlashHitColor()
        {
            if (spriteRenderer == null)
            {
                yield break;
            }

            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(hitFlashDuration);
            spriteRenderer.color = originalColor;
            hitFlashRoutine = null;
        }

        private void Defeat()
        {
            if (isDefeated)
            {
                return;
            }

            isDefeated = true;
            StopAllCoroutines();
            HideLine();
            body.velocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = false;

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (animator != null)
            {
                animator.SetBool(BossIsMovingHash, false);
                animator.ResetTrigger(BossAttackHash);
                animator.SetTrigger(BossDeathHash);
            }

            StartCoroutine(DestroyAfterDeathAnimation());
        }

        private IEnumerator DestroyAfterDeathAnimation()
        {
            yield return new WaitForSeconds(deathDestroyDelay);
            Destroy(gameObject);
        }

        private void TriggerAttackAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger(BossAttackHash);
            }
        }

        private void UpdateAnimator(bool isMoving)
        {
            if (animator == null)
            {
                return;
            }

            bool shouldMove = isMoving || isBurstCatchingUp || Mathf.Abs(body.velocity.x) > 0.1f;
            animator.SetBool(BossIsMovingHash, shouldMove);
        }

        private void SetLine(Color color)
        {
            if (abilityLine == null)
            {
                return;
            }

            abilityLine.enabled = true;
            abilityLine.startColor = color;
            abilityLine.endColor = color;
        }

        private void DrawLine(Vector3 start, Vector3 end)
        {
            if (abilityLine == null)
            {
                return;
            }

            abilityLine.SetPosition(0, start + Vector3.up * 0.65f);
            abilityLine.SetPosition(1, end);
        }

        private void HideLine()
        {
            if (abilityLine != null)
            {
                abilityLine.enabled = false;
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            jumpForce = Mathf.Max(0f, jumpForce);
            gravityScale = Mathf.Max(0f, gravityScale);
            maxFallSpeed = Mathf.Max(0.1f, maxFallSpeed);
            decisionInterval = Mathf.Max(0.1f, decisionInterval);
            groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
            obstacleCheckDistance = Mathf.Max(0.01f, obstacleCheckDistance);
            ceilingCheckDistance = Mathf.Max(0.01f, ceilingCheckDistance);
            ceilingRecoveryDownVelocity = Mathf.Max(0.1f, ceilingRecoveryDownVelocity);
            ceilingRecoveryHorizontalDamping = Mathf.Clamp01(ceilingRecoveryHorizontalDamping);
            jumpCooldown = Mathf.Max(0.01f, jumpCooldown);
            playerRunSpeedEstimate = Mathf.Max(0f, playerRunSpeedEstimate);
            desiredChaseDistance = Mathf.Max(0.1f, desiredChaseDistance);
            chaseDistanceDeadZone = Mathf.Max(0f, chaseDistanceDeadZone);
            catchUpDistance = Mathf.Max(desiredChaseDistance + 0.1f, catchUpDistance);
            catchUpSpeedBonus = Mathf.Max(0f, catchUpSpeedBonus);
            maxChaseSpeed = Mathf.Max(playerRunSpeedEstimate, maxChaseSpeed);
            burstCatchUpDistance = Mathf.Max(catchUpDistance + 0.1f, burstCatchUpDistance);
            burstSpeed = Mathf.Max(maxChaseSpeed, burstSpeed);
            burstDuration = Mathf.Max(0.05f, burstDuration);
            burstCooldown = Mathf.Max(0.1f, burstCooldown);
            emergencyRepositionGap = Mathf.Max(catchUpDistance + 0.1f, emergencyRepositionGap);
            emergencyRepositionDistance = Mathf.Max(desiredChaseDistance, emergencyRepositionDistance);
            emergencyVerticalGap = Mathf.Max(1f, emergencyVerticalGap);
            meleeRange = Mathf.Max(0.1f, meleeRange);
            contactAttackRecovery = Mathf.Max(0f, contactAttackRecovery);
            maxHealth = Mathf.Max(1f, maxHealth);
            playerAttackDamage = Mathf.Max(0f, playerAttackDamage);
            struckStepDamage = Mathf.Max(0f, struckStepDamage);
            hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
            deathDestroyDelay = Mathf.Max(0f, deathDestroyDelay);
            grappleRange = Mathf.Max(0.1f, grappleRange);
            grappleStepLandingSkin = Mathf.Max(0.005f, grappleStepLandingSkin);
            objectAbilityRange = Mathf.Max(0.1f, objectAbilityRange);
        }
    }
}
