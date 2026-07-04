using UnityEngine;

namespace Ciga.Demo
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class StepMover2D : MonoBehaviour
    {
        public enum StepMovementAction
        {
            MoveLeftForDuration,
            MoveRightForDuration,
            MoveUpForDuration,
            MoveDownForDuration,
            StopForDuration,
        }

        [System.Serializable]
        public sealed class StepMovementStep
        {
            [SerializeField] private StepMovementAction action = StepMovementAction.StopForDuration;
            [SerializeField] private float duration = 1f;
            [SerializeField] private float speedMultiplier = 1f;

            public StepMovementAction Action => action;
            public float Duration => Mathf.Max(0f, duration);
            public float SpeedMultiplier => Mathf.Max(0f, speedMultiplier);
        }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private bool loopMovementPlan = true;
        [SerializeField] private StepMovementStep[] movementPlan;

        [Header("Passenger Carry")]
        [SerializeField] private bool carryPlayer = true;
        [SerializeField] private bool carryEnemies = true;
        [SerializeField] private float passengerCheckHeight = 0.65f;

        [Header("Breakable")]
        [Tooltip("How many times this step must be struck before breaking. 0 = never breaks.")]
        [SerializeField] private int maxStrikeCount = 3;
        [Tooltip("Optional prefab (particles, debris) spawned at this object's position on break.")]
        [SerializeField] private GameObject breakEffect;
        [SerializeField] private AudioClip breakClip;

        [Header("Bounce")]
        [Tooltip("When enabled, this block reverses direction on hitting DemoGround.")]
        [SerializeField] private bool bounceOnGround = true;
        [SerializeField] private float bounceCheckDistance = 0.05f;
        [SerializeField] private string groundBounceTag = "DemoGround";

        private Collider2D bodyCollider;
        private int currentStrikeCount;
        private Rigidbody2D body;
        private int currentStepIndex;
        private float currentStepElapsed;
        private bool movementPlanCompleted;
        private bool externalMovementLocked;

        private void Awake()
        {
            bodyCollider = GetComponent<Collider2D>();
            body = GetComponent<Rigidbody2D>();
            ConfigureBody();
        }

        private void OnEnable()
        {
            currentStepIndex = 0;
            currentStepElapsed = 0f;
            movementPlanCompleted = false;
        }

        private void Reset()
        {
            bodyCollider = GetComponent<Collider2D>();
            body = GetComponent<Rigidbody2D>();
            ConfigureBody();
        }

        private void FixedUpdate()
        {
            if (externalMovementLocked)
            {
                return;
            }

            UpdateMovementPlan();
        }

        /// <summary>Called when a player or boss strikes (launches) this step.</summary>
        public void OnStruck()
        {
            if (maxStrikeCount <= 0) return;

            currentStrikeCount++;
            if (currentStrikeCount >= maxStrikeCount)
            {
                Break();
            }
        }

        private void Break()
        {
            if (breakEffect != null)
            {
                Instantiate(breakEffect, transform.position, Quaternion.identity);
            }

            if (breakClip != null)
            {
                AudioManager2D manager = AudioManager2D.Instance;
                if (manager != null)
                {
                    manager.PlayOneShotAt(breakClip, transform.position);
                }
            }

            Destroy(gameObject);
        }

        public void BeginExternalMovement()
        {
            externalMovementLocked = true;
        }

        public void EndExternalMovement()
        {
            externalMovementLocked = false;
        }

        private void UpdateMovementPlan()
        {
            if (movementPlanCompleted || movementPlan == null || movementPlan.Length == 0)
            {
                return;
            }

            StepMovementStep step = movementPlan[Mathf.Clamp(currentStepIndex, 0, movementPlan.Length - 1)];
            switch (step.Action)
            {
                case StepMovementAction.MoveLeftForDuration:
                    MoveHorizontal(-1f, step.SpeedMultiplier);
                    TickTimedStep(step.Duration);
                    break;

                case StepMovementAction.MoveRightForDuration:
                    Move(Vector2.right, step.SpeedMultiplier);
                    TickTimedStep(step.Duration);
                    break;

                case StepMovementAction.MoveUpForDuration:
                    Move(Vector2.up, step.SpeedMultiplier);
                    TickTimedStep(step.Duration);
                    break;

                case StepMovementAction.MoveDownForDuration:
                    Move(Vector2.down, step.SpeedMultiplier);
                    TickTimedStep(step.Duration);
                    break;

                case StepMovementAction.StopForDuration:
                    TickTimedStep(step.Duration);
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

        private void MoveHorizontal(float direction, float speedMultiplier)
        {
            Move(Vector2.right * direction, speedMultiplier);
        }

        private void Move(Vector2 direction, float speedMultiplier)
        {
            if (direction.sqrMagnitude <= 0.0001f || bodyCollider == null)
            {
                return;
            }

            Vector2 normalizedDir = direction.normalized;
            Vector2 movement = normalizedDir * (moveSpeed * speedMultiplier * Time.fixedDeltaTime);

            // Horizontal bounce only — reverse X when hitting a DemoGround wall
            if (bounceOnGround && Mathf.Abs(movement.x) > 0.0001f)
            {
                Bounds bounds = bodyCollider.bounds;
                float sign = Mathf.Sign(movement.x);
                Vector2 origin = new Vector2(
                    sign > 0 ? bounds.max.x : bounds.min.x,
                    bounds.center.y);
                Vector2 size = new Vector2(0.02f, bounds.size.y * 0.7f);
                float dist = Mathf.Abs(movement.x) + bounceCheckDistance;

                RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.right * sign, dist);
                if (hit.collider != null && hit.collider.CompareTag(groundBounceTag))
                {
                    movement.x = -movement.x;
                }
            }

            MovePassengers(movement);
            body.MovePosition(body.position + movement);
        }

        private void MovePassengers(Vector2 movement)
        {
            if (carryEnemies)
            {
                Enemy2D[] enemies = FindObjectsByType<Enemy2D>(FindObjectsSortMode.None);
                for (int i = 0; i < enemies.Length; i++)
                {
                    Enemy2D enemy = enemies[i];
                    if (enemy == null)
                    {
                        continue;
                    }

                    Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                    if (enemyCollider != null && IsStandingOnStep(enemyCollider))
                    {
                        enemy.CarryByMovingStep(movement);
                    }
                }
            }

            if (!carryPlayer)
            {
                return;
            }

            PlayerController2D[] players = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null)
                {
                    continue;
                }

                Collider2D playerCollider = player.GetComponent<Collider2D>();
                if (playerCollider != null && IsStandingOnStep(playerCollider))
                {
                    player.CarryByMovingStep(movement);
                }
            }
        }

        private bool IsStandingOnStep(Collider2D collider)
        {
            Bounds stepBounds = bodyCollider.bounds;
            Bounds passengerBounds = collider.bounds;
            bool horizontallyOverlaps = passengerBounds.max.x > stepBounds.min.x + 0.02f
                && passengerBounds.min.x < stepBounds.max.x - 0.02f;
            bool isNearTop = passengerBounds.min.y >= stepBounds.max.y - 0.15f
                && passengerBounds.min.y <= stepBounds.max.y + passengerCheckHeight + 0.08f;
            return horizontallyOverlaps && isNearTop;
        }

        private void ConfigureBody()
        {
            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
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
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            passengerCheckHeight = Mathf.Max(0.01f, passengerCheckHeight);
        }
    }
}
