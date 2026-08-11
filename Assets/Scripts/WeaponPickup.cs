using UnityEngine;

namespace SoulsLike.Player
{
    /// <summary>
    /// Place on a weapon lying in the world. Give its Collider "Is Trigger" checked
    /// (any shape works). Walking into it equips weaponPrefab on the player's
    /// WeaponSystem and removes the pickup from the world.
    /// </summary>
    public class WeaponPickup : MonoBehaviour
    {
        [Tooltip("Weapon prefab to equip on pickup (e.g. DarkMoonGreatsword).")]
        public GameObject weaponPrefab;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col == null)
                Debug.LogWarning($"[WeaponPickup] {name} has no Collider - add one and check 'Is Trigger'.", this);
            else if (!col.isTrigger)
                Debug.LogWarning($"[WeaponPickup] {name}'s Collider isn't a trigger - check 'Is Trigger' in the Inspector.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            var weaponSystem = other.GetComponentInParent<WeaponSystem>();
            if (weaponSystem == null || weaponPrefab == null) return;

            Debug.Log($"[WeaponPickup] Equipping {weaponPrefab.name}.");
            weaponSystem.Equip(weaponPrefab);
            Destroy(gameObject);
        }
    }
}
