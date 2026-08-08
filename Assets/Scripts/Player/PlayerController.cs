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

        public ActionState State { get; private set; } = ActionState.Free;

        private CharacterController controller;
        private Health health;
        private Stamina stamina;

        private Vector3 velocity;
        private float actionTimer;
        private Vector3 rollDirection;
        private bool damageWindowOpen;
        private float pendingAttackDamage;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            stamina = GetComponent<Stamina>();

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

            switch (State)
            {
                case ActionState.Free:
                    HandleFreeMovement();
                    HandleActionInput();
                    break;
                case ActionState.Rolling:
                    ApplyRollMotion();
                    break;
                case ActionState.Attacking:
                    // Root motion is faked with a small forward nudge; replace with animation root motion when available.
                    HandleAttackDamageWindow();
                    break;
            }

            ApplyGravity();
        }

        // ---------- Movement ----------

        private void HandleFreeMovement()
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool isRunning = Input.GetKey(KeyCode.LeftShift) && input.sqrMagnitude > 0.01f;

            Vector3 camForward = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRight = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : Vector3.right;
            Vector3 moveDir = (camForward * input.y + camRight * input.x);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            float speed = isRunning ? runSpeed : walkSpeed;
            Vector3 horizontalMove = moveDir * speed;

            controller.Move(horizontalMove * Time.deltaTime);

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
        }

        private void FaceDirection(Vector3 dir)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (controller.isGrounded && velocity.y < 0)
                velocity.y = -2f;
            else
                velocity.y += gravity * Time.deltaTime;

            controller.Move(new Vector3(0f, velocity.y, 0f) * Time.deltaTime);
        }

        // ---------- Actions ----------

        private void HandleActionInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryStartRoll();
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
        }

        private void ApplyRollMotion()
        {
            actionTimer += Time.deltaTime;

            bool inIFrameWindow = actionTimer >= rollInvulnStart && actionTimer <= rollInvulnEnd;
            health.SetInvulnerable(inIFrameWindow);

            // Speed curve: fast burst, tapering toward the end of the roll.
            float t = actionTimer / rollDuration;
            float speedMul = Mathf.Lerp(1f, 0.2f, t);
            controller.Move(rollDirection * rollSpeed * speedMul * Time.deltaTime);

            if (actionTimer >= rollDuration)
            {
                health.SetInvulnerable(false);
                State = ActionState.Free;
            }
        }

        private void TryStartAttack(float damage, float staminaCost, float duration)
        {
            if (!stamina.TrySpend(staminaCost)) return;

            State = ActionState.Attacking;
            actionTimer = 0f;
            pendingAttackDamage = damage;
            damageWindowOpen = false;
            attackDuration = duration;
        }

        private float attackDuration;

        private void HandleAttackDamageWindow()
        {
            actionTimer += Time.deltaTime;

            // Damage lands in the middle third of the swing - tune per animation later.
            float windowStart = attackDuration * 0.35f;
            float windowEnd = attackDuration * 0.55f;

            if (!damageWindowOpen && actionTimer >= windowStart && actionTimer <= windowEnd)
            {
                damageWindowOpen = true;
                DealDamageInFront();
            }

            if (actionTimer >= attackDuration)
                State = ActionState.Free;
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
            // Reserved for future hit-stun / stagger handling.
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 origin = transform.position + Vector3.up * 1f + transform.forward * (attackRange * 0.5f);
            Gizmos.DrawWireSphere(origin, attackRange * 0.5f);
        }
    }
}
