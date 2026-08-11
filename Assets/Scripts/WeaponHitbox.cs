using UnityEngine;

namespace SoulsLike.Player
{
    /// <summary>
    /// Simple trigger detector for weapon hitboxes.
    /// Attached to weapon colliders to detect hits and notify WeaponSystem.
    /// </summary>
    public class WeaponHitbox : MonoBehaviour
    {
        private WeaponSystem weaponSystem;

        private void Awake()
        {
            // Find WeaponSystem in parent hierarchy
            weaponSystem = GetComponentInParent<WeaponSystem>();
            if (weaponSystem == null)
            {
                Debug.LogError("WeaponHitbox: Cannot find WeaponSystem in parent hierarchy!");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (weaponSystem != null)
            {
                weaponSystem.OnWeaponHit(other);
            }
        }
    }
}