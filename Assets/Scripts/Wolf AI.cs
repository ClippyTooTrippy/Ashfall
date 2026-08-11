using UnityEngine;
using UnityEngine.AI;
using SoulsLike.Systems;

namespace SoulsLike.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Animator))]
    public class WolfAI : MonoBehaviour
    {
        public enum State { Idle, Patrol, Chase, Telegraph, Lunge, Stagger, Dead }

        [Header("Target & Ranges")]
        public Transform player;
        public float detectionRange = 15f;
        public float attackRange = 2.5f;
        public float loseSightRange = 20f;

        [Header("Movement Speeds")]
        public float walkSpeed = 2.5f;
        public float runSpeed = 6f;
        public float turnSpeed = 10f;

        [Header("Combat")]
        public float attackDamage = 15f;
        public float attackCooldown = 1.6f;
        public float telegraphDuration = 0.5f;
        public float lungeSpeed = 12f;
        public float lungeDuration = 0.25f;

        [Header("Attack Telegraph (visual warning before the lunge)")]
        public Color telegraphColor = Color.red;
        public float telegraphScalePunch = 1.15f;

        [Header("Stagger")]
        public float staggerDuration = 0.5f;

        public State CurrentState { get; private set; } = State.Idle;

        private NavMeshAgent agent;
        private Health health;
        private Animator animator;
        private Renderer bodyRenderer;
        private Color originalColor;
        private Vector3 originalScale;

        private float stateTimer;
        private float lastAttackTime = -999f;
        private Vector3 lungeDirection;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            animator = GetComponent<Animator>();

            if (player == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) player = playerObj.transform;
            }

            bodyRenderer = GetComponentInChildren<Renderer>();
            if (bodyRenderer != null) originalColor = bodyRenderer.material.color;
            originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            health.OnDamaged += HandleDamaged;
            health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            if (CurrentState == State.Dead) return;

            switch (CurrentState)
            {
                case State.Idle: TickIdle(); break;
                case State.Chase: TickChase(); break;
                case State.Telegraph: TickTelegraph(); break;
                case State.Lunge: TickLunge(); break;
                case State.Stagger: TickStagger(); break;
            }

            UpdateAnimator();
        }

        // ---------- States ----------

        private void TickIdle()
        {
            agent.isStopped = true;
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= detectionRange)
                ChangeState(State.Chase);
        }

        private void TickChase()
        {
            if (player == null) { ChangeState(State.Idle); return; }

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > loseSightRange)
            {
                ChangeState(State.Idle);
                return;
            }

            if (dist <= attackRange && Time.time - lastAttackTime >= attackCooldown)
            {
                ChangeState(State.Telegraph);
                return;
            }

            agent.isStopped = false;
            agent.speed = runSpeed;
            agent.SetDestination(player.position);
            FaceTarget(player.position);
        }

        private void TickTelegraph()
        {
            agent.isStopped = true;
            if (player != null) FaceTarget(player.position);

            stateTimer += Time.deltaTime;
            float t = Mathf.Clamp01(stateTimer / telegraphDuration);

            if (bodyRenderer != null)
                bodyRenderer.material.color = Color.Lerp(originalColor, telegraphColor, t);
            transform.localScale = Vector3.Lerp(originalScale, originalScale * telegraphScalePunch, t);

            if (stateTimer >= telegraphDuration)
            {
                ResetTelegraph();
                lungeDirection = player != null ? (player.position - transform.position).normalized : transform.forward;
                lungeDirection.y = 0f;
                ChangeState(State.Lunge);
            }
        }

        private void TickLunge()
        {
            agent.isStopped = true; // move manually during the lunge for a punchy, precise burst
            stateTimer += Time.deltaTime;

            transform.position += lungeDirection * lungeSpeed * Time.deltaTime;

            if (stateTimer >= lungeDuration)
            {
                DealDamageIfInRange();
                lastAttackTime = Time.time;
                ChangeState(State.Chase);
            }
        }

        private void TickStagger()
        {
            agent.isStopped = true;
            stateTimer += Time.deltaTime;
            if (stateTimer >= staggerDuration)
                ChangeState(State.Chase);
        }

        // ---------- Helpers ----------

        private void FaceTarget(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
        }

        private void DealDamageIfInRange()
        {
            if (player == null) return;
            if (Vector3.Distance(transform.position, player.position) > attackRange * 1.3f) return;

            Health playerHealth = player.GetComponentInParent<Health>();
            playerHealth?.ApplyDamage(attackDamage);
        }

        private void ResetTelegraph()
        {
            if (bodyRenderer != null) bodyRenderer.material.color = originalColor;
            transform.localScale = originalScale;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            // Drives the Walk/Run blend tree (or Idle/Walk/Run states) purely off actual movement speed,
            // so no separate attack clip is needed - Lunge just briefly overrides normal locomotion.
            float speed = CurrentState == State.Chase ? runSpeed
                        : CurrentState == State.Lunge ? lungeSpeed
                        : 0f;
            animator.SetFloat(SpeedParam, speed);
        }

        private void HandleDamaged(float amount)
        {
            if (CurrentState == State.Dead) return;
            ResetTelegraph();
            ChangeState(State.Stagger);
        }

        private void HandleDeath()
        {
            ResetTelegraph();
            ChangeState(State.Dead);
            agent.isStopped = true;
            agent.enabled = false;

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Destroy(gameObject, 3f);
        }

        private void ChangeState(State next)
        {
            CurrentState = next;
            stateTimer = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}