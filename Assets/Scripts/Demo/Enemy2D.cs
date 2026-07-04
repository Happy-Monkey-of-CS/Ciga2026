using UnityEngine;

namespace Ciga.Demo
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class Enemy2D : MonoBehaviour
    {
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
        [SerializeField] private EnemyMovementStep[] movementPlan;

        private bool isDefeated;
        private Collider2D bodyCollider;
        private int currentStepIndex;
        private float currentStepElapsed;
        private bool movementPlanCompleted;

        private void Awake()
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            currentStepIndex = 0;
            currentStepElapsed = 0f;
            movementPlanCompleted = false;
        }

        private void Update()
        {
            UpdateMovementPlan();
        }

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            KillPlayerIfTouched(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            KillPlayerIfTouched(other);
        }

        public void Defeat()
        {
            if (isDefeated)
            {
                return;
            }

            isDefeated = true;
            Destroy(gameObject);
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

        private void UpdateMovementPlan()
        {
            if (isDefeated || movementPlanCompleted || movementPlan == null || movementPlan.Length == 0)
            {
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
            currentStepElapsed += Time.deltaTime;
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
            float distance = direction * moveSpeed * speedMultiplier * Time.deltaTime;
            transform.position += new Vector3(distance, 0f, 0f);
        }

        private bool HasGroundAhead(float direction)
        {
            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            Bounds bounds = bodyCollider.bounds;
            float x = direction < 0f
                ? bounds.min.x - edgeCheckForwardOffset
                : bounds.max.x + edgeCheckForwardOffset;
            Vector2 origin = new Vector2(x, bounds.min.y + 0.05f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, edgeCheckDistance, groundMask);
            return hit.collider != null && hit.normal.y > 0.5f;
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
            edgeCheckForwardOffset = Mathf.Max(0f, edgeCheckForwardOffset);
            edgeCheckDistance = Mathf.Max(0.01f, edgeCheckDistance);
        }
    }
}
