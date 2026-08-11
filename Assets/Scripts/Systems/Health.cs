using System;
using UnityEngine;

namespace SoulsLike.Systems
{
    /// <summary>
    /// Generic health component. Handles damage, death, and a brief
    /// invulnerability window (used for dodge-roll i-frames and hit-stun).
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("Health")]
        public float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        [Header("State")]
        public bool isInvulnerable = false;
        public bool IsDead { get; private set; }

        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<float> OnDamaged;               // (amount)
        public event Action OnDeath;

        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth <= 0 ? 0 : currentHealth / maxHealth;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        /// <summary>
        /// Apply damage. Returns true if the damage actually landed
        /// (false if blocked by invulnerability or already dead).
        /// </summary>
        public bool ApplyDamage(float amount)
        {
            if (IsDead || isInvulnerable || amount <= 0f)
                return false;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnDamaged?.Invoke(amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
                Die();

            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetInvulnerable(bool value)
        {
            isInvulnerable = value;
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            OnDeath?.Invoke();
        }

        public void ResetHealth()
        {
            IsDead = false;
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
