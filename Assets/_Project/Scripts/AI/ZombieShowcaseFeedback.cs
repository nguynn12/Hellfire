using Hellfire.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Hellfire.AI
{
    // Visual-only companion. Existing server AI and damage logic remain unchanged.
    public sealed class ZombieShowcaseFeedback : MonoBehaviour
    {
        public bool Ranged;
        public float AttackInterval = 1.2f;
        public int HitsReceived { get; private set; }
        public bool DeathObserved { get; private set; }
        private Health health;
        private Animator animator;
        private EnemyStateMachine ai;
        private NavMeshAgent agent;
        private float nextAttack;
        private void Awake()
        {
            health = GetComponent<Health>(); animator = GetComponentInChildren<Animator>();
            ai = GetComponent<EnemyStateMachine>(); agent = GetComponent<NavMeshAgent>();
        }
        private void OnEnable() { health.OnDamageTaken += Hit; health.OnDied += Die; }
        private void OnDisable() { health.OnDamageTaken -= Hit; health.OnDied -= Die; }
        private void Update()
        {
            if (DeathObserved || animator == null) return;
            animator.SetFloat("Speed", agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0);
            if (!Ranged && ai != null && ai.enabled && ai.CurrentState == EnemyState.Attack && Time.time >= nextAttack)
            {
                animator.SetTrigger("Attack"); nextAttack = Time.time + AttackInterval;
            }
        }
        private void Hit(float damage, HitboxType hitbox, ulong attacker)
        {
            HitsReceived++;
            if (!DeathObserved && animator != null) animator.SetTrigger("Hit");
        }
        private void Die(ulong attacker)
        {
            DeathObserved = true;
            if (animator != null) { animator.ResetTrigger("Hit"); animator.SetTrigger("Die"); }
        }
    }
}
