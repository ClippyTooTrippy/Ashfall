using UnityEngine;
using SoulsLike.Systems;

namespace SoulsLike.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class WeaponSystem : MonoBehaviour
    {
        [Header("Weapon Setup")]
        public GameObject weaponPrefab;
        public Transform rightHandSocket;
        public Transform backSocket;

        [Header("Weapon Fit (drawn / in-hand)")]
        public Vector3 weaponPositionOffset = Vector3.zero;
        public Vector3 weaponRotationOffset = Vector3.zero;

        [Header("Weapon Fit (sheathed / on back)")]
        public Vector3 sheathedPositionOffset = Vector3.zero;
        public Vector3 sheathedRotationOffset = Vector3.zero;

        [Header("Sheathe Toggle")]
        public KeyCode toggleSheatheKey = KeyCode.R;
        private bool isDrawn = true;

        [Header("Hit Detection")]
        public Transform weaponHitbox;
        public float hitboxRadius = 0.5f;
        public float riposteDamageMultiplier = 2.5f;

        [Header("Grip Point")]
        // Local-space point on the mesh that gets placed at the socket (the pommel end of
        // the handle) - the mesh's own pivot is off in empty space, so centering the whole
        // bounding box instead puts the grip mid-blade.
        public Vector3 gripAnchorLocal = new Vector3(0.3841f, 0.9597f, 0.5940f);

        private PlayerController playerController;
        private Transform weaponInstance;
        private Collider weaponCollider;
        private WeaponHitbox weaponHitboxComponent;
        private Renderer[] weaponRenderers;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
        }

        private void Start()
        {
            if (weaponPrefab != null)
            {
                Equip(weaponPrefab);
                SetDrawn(false);
            }
        }

        public void Equip(GameObject newWeaponPrefab)
        {
            if (newWeaponPrefab == null || rightHandSocket == null)
            {
                Debug.LogWarning("[WeaponSystem] Equip called with a missing weapon prefab or right hand socket!");
                return;
            }

            Unequip();
            weaponPrefab = newWeaponPrefab;

            GameObject weaponObj = Instantiate(weaponPrefab, rightHandSocket);
            weaponObj.transform.localPosition = Vector3.zero;
            weaponObj.transform.localRotation = Quaternion.identity;

            // Counter-scale against the socket so the weapon's world size stays fixed
            // regardless of scale baked into the rig above it.
            Vector3 parentLossyScale = rightHandSocket.lossyScale;
            weaponObj.transform.localScale = new Vector3(
                parentLossyScale.x != 0 ? 1f / parentLossyScale.x : 1f,
                parentLossyScale.y != 0 ? 1f / parentLossyScale.y : 1f,
                parentLossyScale.z != 0 ? 1f / parentLossyScale.z : 1f);

            weaponInstance = weaponObj.transform;
            isDrawn = true;

            weaponRenderers = weaponInstance.GetComponentsInChildren<Renderer>(true);
            SetSwordVisible(true);
            SetupWeaponHitbox();
        }

        public void Unequip()
        {
            if (weaponHitboxComponent != null)
                Destroy(weaponHitboxComponent);
            weaponCollider = null;
            weaponHitboxComponent = null;
            weaponRenderers = null;

            if (weaponInstance != null)
                Destroy(weaponInstance.gameObject);
            weaponInstance = null;
        }

        public void EnableSword() => SetSwordVisible(true);
        public void DisableSword() => SetSwordVisible(false);

        private void SetSwordVisible(bool visible)
        {
            if (weaponRenderers == null) return;
            for (int i = 0; i < weaponRenderers.Length; i++)
            {
                if (weaponRenderers[i] != null)
                    weaponRenderers[i].enabled = visible;
            }
        }

        private void SetupWeaponHitbox()
        {
            if (weaponInstance == null) return;

            Transform hitboxTransform = weaponHitbox != null ? weaponHitbox : weaponInstance;

            weaponCollider = hitboxTransform.GetComponent<Collider>();
            if (weaponCollider == null)
            {
                var sphereCollider = hitboxTransform.gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = hitboxRadius;

                // Center on the mesh geometry rather than the transform origin - the raw
                // mesh pivot sits far from the blade.
                if (hitboxTransform == weaponInstance && weaponRenderers != null && weaponRenderers.Length > 0)
                {
                    Bounds combined = weaponRenderers[0].bounds;
                    for (int i = 1; i < weaponRenderers.Length; i++)
                        combined.Encapsulate(weaponRenderers[i].bounds);
                    sphereCollider.center = weaponInstance.InverseTransformPoint(combined.center);
                }

                weaponCollider = sphereCollider;
            }
            else
            {
                weaponCollider.isTrigger = true;
            }

            weaponHitboxComponent = weaponCollider.gameObject.AddComponent<WeaponHitbox>();
        }

        private void LateUpdate()
        {
            if (playerController == null) return;

            if (weaponInstance != null && backSocket != null && Input.GetKeyDown(toggleSheatheKey))
                SetDrawn(!isDrawn);

            // Must run after the Animator updates bones for the frame (LateUpdate, not
            // Update), otherwise the socket position read here trails a frame behind.
            if (weaponInstance != null)
            {
                Transform activeSocket = (isDrawn || backSocket == null) ? rightHandSocket : backSocket;
                Vector3 positionOffset = isDrawn ? weaponPositionOffset : sheathedPositionOffset;
                Vector3 rotationOffset = isDrawn ? weaponRotationOffset : sheathedRotationOffset;

                weaponInstance.localRotation = Quaternion.Euler(rotationOffset);
                RecenterOnSocket(activeSocket);
                weaponInstance.position += activeSocket.TransformDirection(positionOffset);
            }

            SetSwordVisible(true);

            if (weaponCollider != null)
                weaponCollider.enabled = isDrawn && IsAttackDamageWindowOpen();
        }

        private void SetDrawn(bool drawn)
        {
            isDrawn = drawn;
            if (weaponInstance == null) return;

            Transform targetSocket = drawn ? rightHandSocket : backSocket;
            if (targetSocket == null) return;

            weaponInstance.SetParent(targetSocket, false);
            Vector3 parentLossyScale = targetSocket.lossyScale;
            weaponInstance.localScale = new Vector3(
                parentLossyScale.x != 0 ? 1f / parentLossyScale.x : 1f,
                parentLossyScale.y != 0 ? 1f / parentLossyScale.y : 1f,
                parentLossyScale.z != 0 ? 1f / parentLossyScale.z : 1f);
        }

        private void RecenterOnSocket(Transform socket)
        {
            if (weaponInstance == null || socket == null)
                return;

            Vector3 gripWorld = weaponInstance.TransformPoint(gripAnchorLocal);
            weaponInstance.position += socket.position - gripWorld;
        }

        private bool IsAttackDamageWindowOpen()
        {
            if (playerController == null || !playerController.IsAttacking)
                return false;

            return playerController.IsInAttackDamageWindow;
        }

        public void OnWeaponHit(Collider other)
        {
            if (other.attachedRigidbody != null &&
                other.attachedRigidbody.gameObject == playerController.gameObject)
                return;

            if (other.gameObject == playerController.gameObject)
                return;

            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth != null)
            {
                float damageAmount = GetCurrentAttackDamage();

                var enemyAI = other.GetComponentInParent<SoulsLike.Enemy.EnemyAI>();
                if (enemyAI != null && enemyAI.IsRiposteVulnerable)
                    damageAmount *= riposteDamageMultiplier;

                targetHealth.ApplyDamage(damageAmount);
            }
        }

        private float GetCurrentAttackDamage()
        {
            if (playerController != null)
                return playerController.GetAttackDamage();
            return 18f;
        }
    }
}
