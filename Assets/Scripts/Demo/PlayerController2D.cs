using UnityEngine;

namespace Ciga.Demo
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float jumpForce = 13f;
        [SerializeField] private float groundCheckDistance = 0.08f;
        [SerializeField] private float groundNormalThreshold = 0.65f;
        [SerializeField] private float attackComboResetTime = 1f;
        [SerializeField] private LayerMask groundMask = 1;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private float horizontalInput;
        private float timeSinceAttack;
        private bool jumpRequested;
        private bool isGrounded;
        private bool isDead;
        private int currentAttack;

        private static readonly int AnimStateHash = Animator.StringToHash("AnimState");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int AirSpeedYHash = Animator.StringToHash("AirSpeedY");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
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
            body.freezeRotation = true;
        }

        private void Update()
        {
            timeSinceAttack += Time.deltaTime;

            if (isDead)
            {
                horizontalInput = 0f;
                jumpRequested = false;
                return;
            }

            horizontalInput = Input.GetAxisRaw("Horizontal");

            if (Input.GetButtonDown("Jump"))
            {
                jumpRequested = true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Attack();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                Kill();
            }

            if (spriteRenderer != null && Mathf.Abs(horizontalInput) > Mathf.Epsilon)
            {
                spriteRenderer.flipX = horizontalInput < 0f;
            }
        }

        private void FixedUpdate()
        {
            isGrounded = CheckGrounded();

            if (isDead)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
                UpdateAnimator();
                return;
            }

            body.velocity = new Vector2(horizontalInput * moveSpeed, body.velocity.y);

            if (jumpRequested && isGrounded)
            {
                body.velocity = new Vector2(body.velocity.x, jumpForce);
                isGrounded = false;

                if (animator != null)
                {
                    animator.SetTrigger(JumpHash);
                }
            }

            UpdateAnimator();
            jumpRequested = false;
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
            horizontalInput = 0f;
            jumpRequested = false;
            body.velocity = Vector2.zero;

            if (animator != null)
            {
                animator.SetBool(NoBloodHash, false);
                animator.SetTrigger(DeathHash);
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(GroundedHash, isGrounded);
            animator.SetFloat(AirSpeedYHash, body.velocity.y);
            animator.SetInteger(AnimStateHash, Mathf.Abs(horizontalInput) > Mathf.Epsilon ? 1 : 0);
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
    }
}
