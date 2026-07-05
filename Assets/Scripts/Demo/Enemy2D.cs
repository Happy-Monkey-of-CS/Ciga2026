using UnityEngine;

namespace Ciga.Demo
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Enemy2D : MonoBehaviour
    {
        private const string TrapTag = "Trap";

        public enum EnemyMovementAction
        {
            MoveLeftForDuration,
            MoveRightForDuration,
            StopForDuration,
            MoveLeftUntilEdge,
            MoveRightUntilEdge,
        }

        [System.Serializable]
        public sealed class EnemyMovementStep
        {
            [SerializeField] private EnemyMovementAction action = EnemyMovementAction.StopForDuration;
            [SerializeField] private float duration = 1f;
            [SerializeField] private float speedMultiplier = 1f;

            public EnemyMovementAction Action => action;
            public float Duration => Mathf.Max(0f, duration);
            public float SpeedMultiplier => Mathf.Max(0f, speedMultiplier);
        }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private bool loopMovementPlan = true;
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField] private float edgeCheckForwardOffset = 0.08f;
        [SerializeField] private float edgeCheckDistance = 0.35f;
        [SerializeField] private float fallGravityScale = 3f;
        [SerializeField] private float maxFallSpeed = 10f;
        [SerializeField] private EnemyMovementStep[] movementPlan;
        [Header("Audio")]
        [SerializeField] private AudioClip defeatClip;
        [SerializeField] private AudioClip fallClip;

        private static PhysicsMaterial2D runtimeNoFrictionMaterial;

        private bool isDefeated;
        private Collider2D bodyCollider;
        private Rigidbody2D body;
        private int currentStepIndex;
        private float currentStepElapsed;
        private bool movementPlanCompleted;
        private bool isFalling;
        private bool isExternallyCarried;
        private Collider2D ignoredFallingSupport;
        private Animator animator;
        private int facingDirection = 1;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int DirectionHash = Animator.StringToHash("Direction");

        private void Awake()
        {
            bodyCollider = GetComponent<Collider2D>();
            body = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            ConfigureBodyForScriptedMovement();
            UpdateAnimator(false, facingDirection);
        }

        private void OnEnable()
        {
            currentStepIndex = 0;
            currentStepElapsed = 0f;
            movementPlanCompleted = false;
            UpdateAnimator(false, facingDirection);
        }

        private void FixedUpdate()
        {
            if (isExternallyCarried)
            {
                return;
            }

            if (isFalling)
            {
                UpdatePhysicsFall();
                return;
            }

            UpdateMovementPlan();
        }

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = false;
            Rigidbody2D resetBody = GetComponent<Rigidbody2D>();
            resetBody.bodyType = RigidbodyType2D.Kinematic;
            resetBody.gravityScale = 0f;
            resetBody.freezeRotation = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            DefeatIfTouchedTrap(other);
            KillPlayerIfTouched(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            DefeatIfTouchedTrap(other);
            KillPlayerIfTouched(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            DefeatIfTouchedTrap(collision.collider);
            KillPlayerIfTouched(collision.collider);
            LandIfSupported(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            DefeatIfTouchedTrap(collision.collider);
            KillPlayerIfTouched(collision.collider);
            LandIfSupported(collision);
        }

        public void Defeat()
        {
            if (isDefeated)
            {
                return;
            }

            isDefeated = true;
            PlaySound(defeatClip);
            Destroy(gameObject);
        }

        private void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            AudioManager2D manager = AudioManager2D.Instance;
            if (manager != null)
            {
                manager.PlayOneShotAt(clip, transform.position, volume);
            }
        }

        public void DropFromSupport()
        {
            DropFromSupport(null);
        }

        public void DropFromSupport(Collider2D ignoredSupport)
        {
            if (isDefeated)
            {
                return;
            }

            transform.SetParent(null, true);
            transform.position += Vector3.down * 0.03f;
            if (ignoredSupport != null && bodyCollider != null)
            {
                Physics2D.IgnoreCollision(bodyCollider, ignoredSupport, true);
            }

            isExternallyCarried = false;
            isFalling = true;
            ignoredFallingSupport = ignoredSupport;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = fallGravityScale;
            body.velocity = new Vector2(0f, Mathf.Min(0f, body.velocity.y));

            PlaySound(fallClip);
        }

        public void BeginPlatformCarry()
        {
            if (isDefeated)
            {
                return;
            }

            isFalling = false;
            ClearIgnoredFallingSupport();
            ConfigureBodyForScriptedMovement();
            isExternallyCarried = true;
        }

        public void MoveWithPlatform(Vector2 movement)
        {
            if (isDefeated || !isExternallyCarried)
            {
                return;
            }

            transform.position += (Vector3)movement;
            Physics2D.SyncTransforms();
        }

        public void CarryByMovingStep(Vector2 movement)
        {
            if (isDefeated || isExternallyCarried || isFalling)
            {
                return;
            }

            transform.position += (Vector3)movement;
            Physics2D.SyncTransforms();
        }

        public void EndPlatformCarry()
        {
            isExternallyCarried = false;
            ConfigureBodyForScriptedMovement();
        }

        public bool IsStandingOn(Collider2D platform)
        {
            if (platform == null)
            {
                return false;
            }

            EnsureBodyCollider();
            Bounds enemyBounds = bodyCollider.bounds;
            Bounds platformBounds = platform.bounds;
            bool horizontallyOverlaps = enemyBounds.max.x > platformBounds.min.x + 0.02f
                && enemyBounds.min.x < platformBounds.max.x - 0.02f;
            bool isNearPlatformTop = enemyBounds.min.y >= platformBounds.max.y - 0.25f
                && enemyBounds.min.y <= platformBounds.max.y + 0.35f;
            return horizontallyOverlaps && isNearPlatformTop;
        }

        private void KillPlayerIfTouched(Collider2D other)
        {
            if (isDefeated)
            {
                return;
            }

            PlayerController2D player = other.GetComponent<PlayerController2D>();
            if (player == null)
            {
                player = other.GetComponentInParent<PlayerController2D>();
            }

            if (player != null)
            {
                player.Kill();
            }
        }

        private void DefeatIfTouchedTrap(Collider2D other)
        {
            if (isDefeated || other == null || !other.CompareTag(TrapTag))
            {
                return;
            }

            Defeat();
        }

        private void UpdateMovementPlan()
        {
            if (isDefeated || movementPlanCompleted || movementPlan == null || movementPlan.Length == 0)
            {
                UpdateAnimator(false, facingDirection);
                return;
            }

            EnemyMovementStep step = movementPlan[Mathf.Clamp(currentStepIndex, 0, movementPlan.Length - 1)];
            switch (step.Action)
            {
                case EnemyMovementAction.MoveLeftForDuration:
                    MoveHorizontal(-1f, step.SpeedMultiplier);
                    TickTimedStep(step.Duration);
                    break;

                case EnemyMovementAction.MoveRightForDuration:
                    MoveHorizontal(1f, step.SpeedMultiplier);
                    TickTimedStep(step.Duration);
                    break;

                case EnemyMovementAction.StopForDuration:
                    UpdateAnimator(false, facingDirection);
                    TickTimedStep(step.Duration);
                    break;

                case EnemyMovementAction.MoveLeftUntilEdge:
                    MoveUntilEdge(-1f, step.SpeedMultiplier);
                    break;

                case EnemyMovementAction.MoveRightUntilEdge:
                    MoveUntilEdge(1f, step.SpeedMultiplier);
                    break;
            }
        }

        private void TickTimedStep(float duration)
        {
            currentStepElapsed += Time.fixedDeltaTime;
            if (currentStepElapsed >= duration)
            {
                AdvanceMovementStep();
            }
        }

        private void MoveUntilEdge(float direction, float speedMultiplier)
        {
            if (!HasGroundAhead(direction))
            {
                AdvanceMovementStep();
                return;
            }

            MoveHorizontal(direction, speedMultiplier);
        }

        private void MoveHorizontal(float direction, float speedMultiplier)
        {
            if (Mathf.Abs(direction) > 0.01f)
            {
                facingDirection = direction < 0f ? -1 : 1;
            }

            UpdateAnimator(true, facingDirection);
            Vector2 targetPosition = body.position + Vector2.right * (direction * moveSpeed * speedMultiplier * Time.fixedDeltaTime);
            body.MovePosition(targetPosition);
        }

        private void UpdateAnimator(bool isMoving, int direction)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(IsMovingHash, isMoving);
            animator.SetInteger(DirectionHash, direction < 0 ? -1 : 1);
        }

        private void UpdatePhysicsFall()
        {
            if (body.velocity.y < -maxFallSpeed)
            {
                body.velocity = new Vector2(body.velocity.x, -maxFallSpeed);
            }
        }

        private bool HasGroundAhead(float direction)
        {
            EnsureBodyCollider();

            Bounds bounds = bodyCollider.bounds;
            float x = direction < 0f
                ? bounds.min.x - edgeCheckForwardOffset
                : bounds.max.x + edgeCheckForwardOffset;
            Vector2 origin = new Vector2(x, bounds.min.y + 0.05f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, edgeCheckDistance, groundMask);
            return hit.collider != null && hit.normal.y > 0.5f;
        }

        private void LandIfSupported(Collision2D collision)
        {
            if (!isFalling || collision.collider == ignoredFallingSupport || !IsGroundLayer(collision.collider.gameObject.layer))
            {
                return;
            }

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                if (contact.normal.y > 0.5f)
                {
                    isFalling = false;
                    ClearIgnoredFallingSupport();
                    ConfigureBodyForScriptedMovement();
                    return;
                }
            }
        }

        private void EnsureBodyCollider()
        {
            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }
        }

        private void ConfigureBodyForScriptedMovement()
        {
            EnsureBodyCollider();
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
                if (body == null)
                {
                    body = gameObject.AddComponent<Rigidbody2D>();
                }
            }

            bodyCollider.isTrigger = false;
            if (bodyCollider.sharedMaterial == null)
            {
                bodyCollider.sharedMaterial = GetRuntimeNoFrictionMaterial();
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private static PhysicsMaterial2D GetRuntimeNoFrictionMaterial()
        {
            if (runtimeNoFrictionMaterial == null)
            {
                runtimeNoFrictionMaterial = new PhysicsMaterial2D("EnemyRuntimeNoFriction2D")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }

            return runtimeNoFrictionMaterial;
        }

        private void ClearIgnoredFallingSupport()
        {
            if (ignoredFallingSupport != null && bodyCollider != null)
            {
                Physics2D.IgnoreCollision(bodyCollider, ignoredFallingSupport, false);
            }

            ignoredFallingSupport = null;
        }

        private bool IsGroundLayer(int layer)
        {
            return (groundMask.value & (1 << layer)) != 0;
        }

        private void AdvanceMovementStep()
        {
            currentStepElapsed = 0f;
            currentStepIndex++;
            if (currentStepIndex < movementPlan.Length)
            {
                return;
            }

            if (loopMovementPlan)
            {
                currentStepIndex = 0;
                return;
            }

            currentStepIndex = movementPlan.Length - 1;
            movementPlanCompleted = true;
            UpdateAnimator(false, facingDirection);
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            edgeCheckForwardOffset = Mathf.Max(0f, edgeCheckForwardOffset);
            edgeCheckDistance = Mathf.Max(0.01f, edgeCheckDistance);
            fallGravityScale = Mathf.Max(0.01f, fallGravityScale);
            maxFallSpeed = Mathf.Max(0.01f, maxFallSpeed);
        }
    }
}
