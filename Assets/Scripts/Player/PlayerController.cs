using UnityEngine;
using SoulsLike.Systems;
using SoulsLike.CameraSystem;

namespace SoulsLike.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Stamina))]
    public class PlayerController : MonoBehaviour
    {
        public enum ActionState { Free, Rolling, Attacking, Staggered }

        [Header("References")]
        public Transform cameraTransform;
        public LockOnSystem lockOn;

        [Header("Movement")]
        public float walkSpeed = 2.2f;
        public float runSpeed = 5.2f;
        public float rotationSpeed = 12f;
        public float gravity = -18f;

        [Header("Roll")]
        public float rollDuration = 0.55f;
        public float rollSpeed = 7.5f;
        public float rollStaminaCost = 24f;
        public float rollInvulnStart = 0.05f;   // i-frames begin slightly after roll starts
        public float rollInvulnEnd = 0.35f;     // and end before the recovery frames

        [Header("Attacks")]
        public float lightAttackStaminaCost = 16f;
        public float heavyAttackStaminaCost = 30f;
        public float lightAttackDuration = 0.45f;
        public float heavyAttackDuration = 0.8f;
        public float attackRange = 1.6f;
        public float lightAttackDamage = 18f;
        public float heavyAttackDamage = 34f;
        public LayerMask hittableLayers;

        [Header("Parry")]
        public KeyCode parryKey = KeyCode.Q;
        public float parryWindowDuration = 0.25f;
        public float parryStaminaCost = 10f;
        public float parryCooldown = 0.6f;

        public ActionState State { get; private set; } = ActionState.Free;

        // Public properties for weapon system to check attack state and timing
        public bool IsAttacking => State == ActionState.Attacking;
        public bool IsInAttackDamageWindow => damageWindowOpen && actionTimer > 0f && actionTimer <= attackDuration;
        public float GetAttackDamage() => pendingAttackDamage;

        private CharacterController controller;
        private Health health;
        private Stamina stamina;
        private Animator animator;
        private WeaponSystem weaponSystem;

        private Vector3 velocity;
        private float actionTimer;
        private Vector3 rollDirection;
        private bool damageWindowOpen;
        private float pendingAttackDamage;

        private bool parryWindowActive;
        private float parryTimer;
        private float lastParryTime = -999f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            stamina = GetComponent<Stamina>();
            animator = GetComponentInChildren<Animator>();
            weaponSystem = GetComponentInChildren<WeaponSystem>();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (health.IsDead)
            {
                controller.enabled = false;
                return;
            }

            TickActionTimer();

            Vector3 horizontalMove = Vector3.zero;

            switch (State)
            {
                case ActionState.Free:
                    horizontalMove = GetFreeMovement();
                    HandleActionInput();
                    break;
                case ActionState.Rolling:
                    horizontalMove = GetRollMotion();
                    break;
                case ActionState.Attacking:
                    // Root motion is faked with a small forward nudge; replace with animation root motion when available.
                    HandleAttackDamageWindow();
                    break;
            }

            if (animator != null)
            {
                float targetSpeed = State == ActionState.Free ? horizontalMove.magnitude : 0f;
                animator.SetFloat("Speed", targetSpeed, 0.08f, Time.deltaTime);
            }

            ApplyGravity();
            Vector3 totalMove = horizontalMove + velocity;
            controller.Move(totalMove * Time.deltaTime);
        }

        // ---------- Movement ----------

        private Vector3 GetFreeMovement()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool isRunning = Input.GetKey(KeyCode.LeftShift) && input.sqrMagnitude > 0.01f;

            Vector3 camForward = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRight = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : Vector3.right;
            Vector3 moveDir = (camForward * input.y + camRight * input.x);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            float speed = isRunning ? runSpeed : walkSpeed;
            Vector3 horizontalMove = moveDir * speed;

            bool locked = lockOn != null && lockOn.HasTarget;
            if (locked)
            {
                Vector3 toTarget = lockOn.CurrentTarget.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                    FaceDirection(toTarget.normalized);
            }
            else if (moveDir.sqrMagnitude > 0.001f)
            {
                FaceDirection(moveDir);
            }

            return horizontalMove;
        }

        private void FaceDirection(Vector3 dir)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (controller.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
            else
            {
                velocity.y += gravity * Time.deltaTime;
            }
        }

        // ---------- Actions ----------

        private void HandleActionInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryStartRoll();
                return;
            }
            if (Input.GetKeyDown(parryKey))
            {
                TryStartParry();
                return;
            }
            if (Input.GetMouseButtonDown(0))
            {
                TryStartAttack(lightAttackDamage, lightAttackStaminaCost, lightAttackDuration);
                return;
            }
            if (Input.GetMouseButtonDown(1))
            {
                TryStartAttack(heavyAttackDamage, heavyAttackStaminaCost, heavyAttackDuration);
                return;
            }
        }

        private void TryStartParry()
        {
            if (Time.time - lastParryTime < parryCooldown) return;
            if (!stamina.TrySpend(parryStaminaCost)) return;

            parryWindowActive = true;
            parryTimer = parryWindowDuration;
            lastParryTime = Time.time;
        }

        // Called by an attacker before applying damage - consumes an active parry window and
        // returns true if the hit should be negated instead of landing.
        public bool TryConsumeParry(GameObject attacker)
        {
            if (!parryWindowActive) return false;
            parryWindowActive = false;
            return true;
        }

        private void TryStartRoll()
        {
            if (!stamina.TrySpend(rollStaminaCost)) return;

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Vector3 camForward = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRight = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : Vector3.right;

            Vector3 dir = camForward * input.y + camRight * input.x;
            rollDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;

            if (rollDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(rollDirection, Vector3.up);

            State = ActionState.Rolling;
            actionTimer = 0f;

            if (animator != null)
                animator.SetTrigger("Roll");
        }

        private Vector3 GetRollMotion()
        {
            actionTimer += Time.deltaTime;

            bool inIFrameWindow = actionTimer >= rollInvulnStart && actionTimer <= rollInvulnEnd;
            health.SetInvulnerable(inIFrameWindow);

            // Speed curve: fast burst, tapering toward the end of the roll.
            // rollDuration is now just a fallback/tuning reference for the curve
            // shape - the actual state exit is driven by ActionStateNotifier,
            // which fires OnAnimatorActionComplete() when the Animator's own
            // Roll -> Locomotion exit transition kicks in.
            float t = Mathf.Clamp01(actionTimer / rollDuration);
            float speedMul = Mathf.Lerp(1f, 0.2f, t);
            Vector3 move = rollDirection * rollSpeed * speedMul;

            return move;
        }

        private void TryStartAttack(float damage, float staminaCost, float duration)
        {
            if (!stamina.TrySpend(staminaCost)) return;

            State = ActionState.Attacking;
            actionTimer = 0f;
            pendingAttackDamage = damage;
            damageWindowOpen = false;
            attackDuration = duration;

            if (animator != null)
                animator.SetTrigger("Attack");
        }

        private float attackDuration;

        private void HandleAttackDamageWindow()
        {
            actionTimer += Time.deltaTime;

            // Damage lands in the middle third of the swing - tune per animation later.
            // (Consider replacing this with an Animation Event on the clip itself
            // once you're happy with the timing, for frame-perfect accuracy.)
            float windowStart = attackDuration * 0.35f;
            float windowEnd = attackDuration * 0.55f;

            if (!damageWindowOpen && actionTimer >= windowStart && actionTimer <= windowEnd)
            {
                damageWindowOpen = true;
                // Enable weapon collider for hit detection during attack window
                if (weaponSystem != null)
                {
                    // WeaponSystem will handle enabling its own collider based on attack state
                }
            }

            // State exit is now driven by ActionStateNotifier calling
            // OnAnimatorActionComplete() when the Attack -> Locomotion
            // transition begins in the Animator, instead of a hardcoded timer.
        }

        /// <summary>
        /// Called by ActionStateNotifier (a StateMachineBehaviour attached to the
        /// Roll and Attack states in the Animator) the instant that state's exit
        /// transition actually begins. This replaces guessing durations in code -
        /// the Animator is now the single source of truth for how long an action lasts.
        /// </summary>
        public void OnAnimatorActionComplete()
        {
            if (State == ActionState.Rolling)
            {
                health.SetInvulnerable(false);
            }

            if (State == ActionState.Rolling || State == ActionState.Attacking)
            {
                State = ActionState.Free;
            }
        }

        private void DealDamageInFront()
        {
            Vector3 origin = transform.position + Vector3.up * 1f;
            Collider[] hits = Physics.OverlapSphere(origin + transform.forward * (attackRange * 0.5f), attackRange * 0.5f, hittableLayers);

            foreach (var hit in hits)
            {
                if (hit.attachedRigidbody != null && hit.attachedRigidbody.gameObject == gameObject) continue;
                if (hit.gameObject == gameObject) continue;

                Health targetHealth = hit.GetComponentInParent<Health>();
                if (targetHealth != null)
                    targetHealth.ApplyDamage(pendingAttackDamage);
            }
        }

        private void TickActionTimer()
        {
            if (parryWindowActive)
            {
                parryTimer -= Time.deltaTime;
                if (parryTimer <= 0f) parryWindowActive = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 origin = transform.position + Vector3.up * 1f + transform.forward * (attackRange * 0.5f);
            Gizmos.DrawWireSphere(origin, attackRange * 0.5f);
        }
    }
}