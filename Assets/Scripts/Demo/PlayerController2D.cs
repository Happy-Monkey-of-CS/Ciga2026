using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.Demo
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        private const string GroundTag = "DemoGround";
        private const string GrappleWallTag = "GrappleWall";
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
        [SerializeField] private float attackComboResetTime = 1f;
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
        private Coroutine climbRoutine;
        private Collider2D aimedGrappleTarget;
        private Vector2 aimedGrapplePoint;
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
        private bool isGrappleAiming;
        private bool isClimbing;
        private bool isForcedWallSliding;
        private int currentAttack;
        private readonly RaycastHit2D[] pulledObjectCastResults = new RaycastHit2D[16];
        private readonly Collider2D[] pulledObjectOverlapResults = new Collider2D[16];
        private readonly List<Collider2D> initialPulledObjectOverlaps = new List<Collider2D>();

        private static readonly int AnimStateHash = Animator.StringToHash("AnimState");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int AirSpeedYHash = Animator.StringToHash("AirSpeedY");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int WallSlideHash = Animator.StringToHash("WallSlide");
        private static readonly int BlockHash = Animator.StringToHash("Block");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int NoBloodHash = Animator.StringToHash("noBlood");
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
            if (animator != null)
            {
                defaultAnimatorSpeed = animator.speed;
            }

            if (grappleLine != null)
            {
                grappleLine.positionCount = 2;
                grappleLine.enabled = false;
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
                return;
            }

            if (Input.GetButtonDown("Jump"))
            {
                jumpRequested = true;
            }

            if (Input.GetKeyDown(KeyCode.J))
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

            if (Input.GetKeyDown(KeyCode.E))
            {
                Kill();
            }

            if (spriteRenderer != null && !isGrappleAiming)
            {
                spriteRenderer.flipX = false;
            }
        }

        private void FixedUpdate()
        {
            isGrounded = CheckGrounded();
            if (isForcedWallSliding && (isGrounded || !CheckWallBeside()))
            {
                isForcedWallSliding = false;
            }

            bool naturalWallSliding = !isGrounded && !isGrappling && !isClimbing && body.velocity.y < 0f && CheckWallBeside();
            isWallSliding = isForcedWallSliding || naturalWallSliding;

            if (isDead)
            {
                StopGrapple();
                StopPullGrappleObject();
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
                body.gravityScale = 0f;
                body.velocity = Vector2.zero;
                UpdateAnimator();
                UpdateGrappleLine();
                jumpRequested = false;
                return;
            }

            ApplyWallSlideGravity();

            if (isPullingGrappleObject)
            {
                body.velocity = new Vector2(GetCurrentAutoRunSpeed(), body.velocity.y);
                PullGrappleObjectTowardPlayer();
            }
            else if (isGrappling)
            {
                PullTowardGrapplePoint();
            }
            else
            {
                body.velocity = new Vector2(GetCurrentAutoRunSpeed(), body.velocity.y);
            }

            if (jumpRequested && isGrounded)
            {
                StopGrapple();
                StopPullGrappleObject();
                isForcedWallSliding = false;
                isWallSliding = false;
                ApplyWallSlideGravity();
                body.velocity = new Vector2(body.velocity.x, jumpForce);
                isGrounded = false;

                if (animator != null)
                {
                    animator.SetTrigger(JumpHash);
                }
            }

            UpdateAnimator();
            UpdateGrappleLine();
            WrapAtMapEdges();
            jumpRequested = false;
        }

        private void LateUpdate()
        {
            if (isGrappleAiming)
            {
                UpdateGrappleAim();
            }

            UpdateGrappleLine();
        }

        private void OnDisable()
        {
            StopGrappleAim();
            StopGrapple();
            StopPullGrappleObject();
            StopClimb();
            isForcedWallSliding = false;
        }

        private void Attack()
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
        }

        public void Kill()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            jumpRequested = false;
            StopGrappleAim();
            StopGrapple();
            StopPullGrappleObject();
            StopClimb();
            isForcedWallSliding = false;
            body.velocity = Vector2.zero;

            if (animator != null)
            {
                animator.SetBool(NoBloodHash, false);
                animator.SetTrigger(DeathHash);
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
            animator.SetBool(WallSlideHash, isWallSliding);
            animator.SetFloat(AirSpeedYHash, body.velocity.y);
            animator.SetInteger(AnimStateHash, isDead ? 0 : 1);
        }

        private void ApplyWallSlideGravity()
        {
            float gravityScale = defaultGravityScale;
            if (isWallSliding)
            {
                gravityScale *= wallSlideFallSpeedMultiplier;
            }

            if (isGrappleAiming)
            {
                gravityScale *= grappleAimMoveSpeedMultiplier;
            }

            body.gravityScale = gravityScale;
        }

        private void StartGrappleAim()
        {
            if (isGrappleAiming || isClimbing)
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

            isGrappleAiming = true;
            aimedGrappleTarget = null;
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
                grappleAimRayLine.enabled = true;
            }

            UpdateGrappleAim();
        }

        private void UpdateGrappleAim()
        {
            if (!isGrappleAiming)
            {
                return;
            }

            Vector2 origin = GetGrappleOrigin();
            UpdateGrappleAimCircle(origin);

            if (!TryGetMouseAimDirection(origin, out Vector2 direction))
            {
                ClearGrappleHighlight();
                UpdateGrappleAimRay(origin, origin);
                return;
            }

            UpdateGrappleAimFacing(direction);

            RaycastHit2D hit = GetFirstGrappleRayHit(origin, direction);
            if (hit.collider != null)
            {
                aimedGrappleTarget = hit.collider;
                aimedGrapplePoint = hit.point;
                ApplyGrappleHighlight(aimedGrappleTarget);
                UpdateGrappleAimRay(origin, aimedGrapplePoint);
                return;
            }

            aimedGrappleTarget = null;
            ClearGrappleHighlight();
            UpdateGrappleAimRay(origin, origin + direction * grappleAimRadius);
        }

        private void ReleaseGrappleAim()
        {
            Collider2D target = aimedGrappleTarget;
            Vector2 targetPoint = aimedGrapplePoint;
            StopGrappleAim();

            if (target != null && !target.isTrigger)
            {
                if (IsRunBlockedAhead())
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
        }

        private void EnterGrappleAimVisual()
        {
            if (spriteRenderer == null || grappleAimSprite == null)
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

            spriteRenderer.sprite = grappleAimSprite;
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
            grappleTarget = target;
            grappleLocalPoint = target.transform.InverseTransformPoint(anchorPoint);
            grappleStartedFromLeft = body.position.x <= target.bounds.center.x;
            grappleStartedAboveTarget = bodyCollider.bounds.min.y >= target.bounds.max.y;
            isGrappling = true;

            if (grappleLine != null)
            {
                grappleLine.enabled = true;
            }
        }

        private void StartPullGrappleObject(Collider2D target, Vector2 anchorPoint)
        {
            if (target == null)
            {
                return;
            }

            StopGrapple();
            pulledGrappleTarget = target;
            pulledGrappleLocalPoint = target.transform.InverseTransformPoint(anchorPoint);
            CacheInitialPulledObjectOverlaps(target);
            isPullingGrappleObject = true;

            if (grappleLine != null)
            {
                grappleLine.enabled = true;
            }
        }

        private float GetCurrentAutoRunSpeed()
        {
            return isGrappleAiming ? autoRunSpeed * grappleAimMoveSpeedMultiplier : autoRunSpeed;
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

        private RaycastHit2D GetFirstGrappleRayHit(Vector2 origin, Vector2 direction)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, grappleAimRadius, grappleMask);
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
            if (grappleAimRayLine == null)
            {
                return;
            }

            grappleAimRayLine.SetPosition(0, origin);
            grappleAimRayLine.SetPosition(1, end);
        }

        private void UpdateGrappleAimCircle(Vector2 origin)
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
                    origin.x + Mathf.Cos(angle) * grappleAimRadius,
                    origin.y + Mathf.Sin(angle) * grappleAimRadius,
                    transform.position.z);
                grappleAimCircleLine.SetPosition(i, position);
            }
        }

        private void CreateGrappleAimLines()
        {
            Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
            grappleAimCircleLine = CreateGrappleGuideLine("Grapple Aim Radius", lineMaterial, 0.035f, grappleAimCircleColor, true);
            grappleAimRayLine = CreateGrappleGuideLine("Grapple Aim Ray", lineMaterial, 0.04f, grappleAimRayColor, false);
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

        private void ResolveGrappleContact(Collider2D target, bool fromLeft, bool fromAbove, Vector2 landingPoint)
        {
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

        private void StopGrapple()
        {
            isGrappling = false;
            grappleTarget = null;

            if (grappleLine != null && !isPullingGrappleObject)
            {
                grappleLine.enabled = false;
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
            if ((!isGrappling && !isPullingGrappleObject) || grappleLine == null)
            {
                return;
            }

            if (!TryGetVisibleGrappleEndPoint(out Vector2 targetPoint))
            {
                StopGrapple();
                StopPullGrappleObject();
                return;
            }

            grappleLine.SetPosition(0, GetGrappleOrigin());
            grappleLine.SetPosition(1, targetPoint);
        }

        private Vector2 GetGrappleOrigin()
        {
            return bodyCollider != null ? bodyCollider.bounds.center : transform.position;
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
            Bounds bounds = bodyCollider.bounds;
            float rightX = bounds.max.x - 0.02f;
            float leftX = bounds.min.x + 0.02f;
            float bottom = bounds.min.y + bounds.size.y * 0.2f;
            float center = bounds.center.y;
            float top = bounds.max.y - bounds.size.y * 0.1f;

            return IsWallBeside(new Vector2(rightX, bottom), Vector2.right)
                || IsWallBeside(new Vector2(rightX, center), Vector2.right)
                || IsWallBeside(new Vector2(rightX, top), Vector2.right)
                || IsWallBeside(new Vector2(leftX, bottom), Vector2.left)
                || IsWallBeside(new Vector2(leftX, center), Vector2.left)
                || IsWallBeside(new Vector2(leftX, top), Vector2.left);
        }

        private bool IsWallBeside(Vector2 origin, Vector2 direction)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, wallCheckDistance + 0.04f, groundMask);
            if (hit.collider == null)
            {
                return false;
            }

            return direction.x > 0f
                ? hit.normal.x <= -groundNormalThreshold
                : hit.normal.x >= groundNormalThreshold;
        }

        private bool IsRunBlockedAhead()
        {
            Bounds bounds = bodyCollider.bounds;
            float rightX = bounds.max.x - 0.02f;
            float bottom = bounds.min.y + bounds.size.y * 0.2f;
            float center = bounds.center.y;
            float top = bounds.max.y - bounds.size.y * 0.1f;

            return IsRunBlockedAheadFrom(new Vector2(rightX, bottom))
                || IsRunBlockedAheadFrom(new Vector2(rightX, center))
                || IsRunBlockedAheadFrom(new Vector2(rightX, top));
        }

        private bool IsRunBlockedAheadFrom(Vector2 origin)
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

            return bestHit.collider != null && bestHit.normal.x <= -groundNormalThreshold;
        }

        private void WrapAtMapEdges()
        {
            if (!wrapAtMapEdges || body.position.x < wrapRightX)
            {
                return;
            }

            StopGrapple();
            StopPullGrappleObject();
            StopGrappleAim();
            StopClimb();
            isForcedWallSliding = false;
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
            grappleAimRadius = Mathf.Max(0.1f, grappleAimRadius);
            grappleAimMoveSpeedMultiplier = Mathf.Clamp(grappleAimMoveSpeedMultiplier, 0.01f, 1f);
            grappleObjectPullSpeed = Mathf.Max(0.1f, grappleObjectPullSpeed);
            grappleClimbAnimationDuration = Mathf.Max(0f, grappleClimbAnimationDuration);
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
