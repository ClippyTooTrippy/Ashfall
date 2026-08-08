using UnityEngine;
using UnityEngine.AI;
using SoulsLike.Systems;

namespace SoulsLike.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        public enum State { Idle, Patrol, Chase, Attack, Stagger, Dead }

        [Header("References")]
        public Transform player;

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

        public State CurrentState { get; private set; } = State.Idle;

        private NavMeshAgent agent;
        private Health health;
        private int patrolIndex;
        private float stateTimer;
        private float lastAttackTime = -999f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();

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
            if (stateTimer >= staggerDuration)
                ChangeState(State.Chase);
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

            Health playerHealth = player.GetComponentInParent<Health>();
            playerHealth?.ApplyDamage(attackDamage);
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