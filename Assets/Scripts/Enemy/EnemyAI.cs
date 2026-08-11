using UnityEngine;
using UnityEngine.AI;
using SoulsLike.Systems;
using SoulsLike.Player;

namespace SoulsLike.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        public enum State { Idle, Patrol, Chase, Attack, Stagger, Dead }

        [Header("References")]
        public Transform player;
        public Animator animator;

        [Header("Detection")]
        public float sightRange = 10f;
        public float sightAngle = 100f;
        public float loseSightRange = 16f;

        [Header("Patrol")]
        public Transform[] patrolPoints;
        public float patrolWaitTime = 2f;

        [Header("Combat")]
        public float attackRange = 1.8f;
        public float attackCooldown = 1.6f;
        public float attackDamage = 14f;
        public float attackWindup = 0.5f;
        public LayerMask hittableLayers;

        [Header("Stagger")]
        public float staggerDuration = 0.6f;

        [Header("Parry / Riposte")]
        [Tooltip("How long this enemy stays exposed (extended stagger) after having an attack " +
                 "parried by the player - WeaponSystem checks IsRiposteVulnerable during this " +
                 "window to award bonus damage.")]
        public float riposteVulnerableDuration = 2.5f;
        public bool IsRiposteVulnerable { get; private set; }

        public State CurrentState { get; private set; } = State.Idle;

        private NavMeshAgent agent;
        private Health health;
        private int patrolIndex;
        private float stateTimer;
        private float lastAttackTime = -999f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int HitTrigger = Animator.StringToHash("Hit");
        private static readonly int DeathTrigger = Animator.StringToHash("Death");

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (player == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) player = playerObj.transform;
            }
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
                case State.Patrol: TickPatrol(); break;
                case State.Chase: TickChase(); break;
                case State.Attack: TickAttack(); break;
                case State.Stagger: TickStagger(); break;
            }

            UpdateAnimator();
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            float speed = CurrentState == State.Chase ? agent.speed
                        : CurrentState == State.Patrol ? agent.speed * 0.5f
                        : 0f;
            animator.SetFloat(SpeedParam, speed);
        }

        // ---------- States ----------

        private void TickIdle()
        {
            agent.isStopped = true;
            if (CanSeePlayer())
            {
                ChangeState(State.Chase);
                return;
            }
            if (patrolPoints != null && patrolPoints.Length > 0)
                ChangeState(State.Patrol);
        }

        private void TickPatrol()
        {
            if (CanSeePlayer())
            {
                ChangeState(State.Chase);
                return;
            }

            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                ChangeState(State.Idle);
                return;
            }

            agent.isStopped = false;
            agent.SetDestination(patrolPoints[patrolIndex].position);

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                stateTimer += Time.deltaTime;
                if (stateTimer >= patrolWaitTime)
                {
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    stateTimer = 0f;
                }
            }
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
                ChangeState(State.Attack);
                return;
            }

            agent.isStopped = false;
            agent.SetDestination(player.position);
        }

        private void TickAttack()
        {
            agent.isStopped = true;
            FaceTarget(player.position);

            stateTimer += Time.deltaTime;
            if (stateTimer >= attackWindup)
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
            float duration = IsRiposteVulnerable ? riposteVulnerableDuration : staggerDuration;
            if (stateTimer >= duration)
            {
                IsRiposteVulnerable = false;
                ChangeState(State.Chase);
            }
        }

        // ---------- Helpers ----------

        private bool CanSeePlayer()
        {
            if (player == null) return false;
            Vector3 toPlayer = player.position - transform.position;
            float dist = toPlayer.magnitude;
            if (dist > sightRange) return false;

            float angle = Vector3.Angle(transform.forward, toPlayer);
            if (angle > sightAngle * 0.5f) return false;

            // Raise the ray origin above the enemy's own collider so it doesn't
            // hit itself, and ignore a self-hit outright if it still happens.
            Vector3 eyeOrigin = transform.position + Vector3.up * 1.6f;
            Vector3 dirToPlayer = (player.position + Vector3.up - eyeOrigin).normalized;

            if (Physics.Raycast(eyeOrigin, dirToPlayer, out RaycastHit hit, sightRange))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    return true; // self-hit ignored, treat line of sight as clear

                return hit.transform == player || hit.transform.IsChildOf(player);
            }
            return true; // nothing obstructing at all = clear line of sight
        }

        private void FaceTarget(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        }

        private void DealDamageIfInRange()
        {
            if (player == null) return;
            if (Vector3.Distance(transform.position, player.position) > attackRange * 1.2f) return;

            // Give the player a chance to parry before any damage lands - a successful parry
            // negates the hit entirely and leaves this enemy exposed instead.
            var playerController = player.GetComponentInParent<PlayerController>();
            if (playerController != null && playerController.TryConsumeParry(gameObject))
            {
                GetParried();
                return;
            }

            Health playerHealth = player.GetComponentInParent<Health>();
            playerHealth?.ApplyDamage(attackDamage);
        }

        /// <summary>Called when the player successfully parries this enemy's attack - opens an
        /// extended vulnerability window (see IsRiposteVulnerable) instead of dealing damage.</summary>
        public void GetParried()
        {
            IsRiposteVulnerable = true;
            ChangeState(State.Stagger);
        }

        private void HandleDamaged(float amount)
        {
            if (CurrentState == State.Dead) return;
            ChangeState(State.Stagger);
        }

        private void HandleDeath()
        {
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

            if (animator != null)
            {
                if (next == State.Attack) animator.SetTrigger(AttackTrigger);
                else if (next == State.Stagger) animator.SetTrigger(HitTrigger);
                else if (next == State.Dead) animator.SetTrigger(DeathTrigger);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}