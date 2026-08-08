using System;
using UnityEngine;

namespace SoulsLike.Systems
{
    /// <summary>
    /// Souls-like stamina: drains on roll/attack, regenerates after a delay,
    /// and pauses regen while the player keeps spending it.
    /// </summary>
    public class Stamina : MonoBehaviour
    {
        [Header("Stamina")]
        public float maxStamina = 100f;
        public float regenPerSecond = 22f;
        public float regenDelayAfterUse = 0.7f;

        private float current;
        private float regenTimer;

        public event Action<float, float> OnStaminaChanged;

        public float Current => current;
        public float Normalized => maxStamina <= 0 ? 0 : current / maxStamina;

        private void Awake()
        {
            current = maxStamina;
        }

        private void Update()
        {
            if (regenTimer > 0f)
            {
                regenTimer -= Time.deltaTime;
                return;
            }

            if (current < maxStamina)
            {
                current = Mathf.Min(maxStamina, current + regenPerSecond * Time.deltaTime);
                OnStaminaChanged?.Invoke(current, maxStamina);
            }
        }

        public bool HasEnough(float amount) => current >= amount;

        /// <summary>Spend stamina. Returns false if not enough was available.</summary>
        public bool TrySpend(float amount)
        {
            if (current < amount) return false;
            current -= amount;
            regenTimer = regenDelayAfterUse;
            OnStaminaChanged?.Invoke(current, maxStamina);
            return true;
        }
    }
}
