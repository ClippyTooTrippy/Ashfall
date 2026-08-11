using UnityEngine;
using SoulsLike.Systems;

namespace SoulsLike.Player
{
    /// <summary>
    /// Handles weapon attachment, hit detection, and damage for the player's sword.
    /// Works with existing PlayerController and ActionStateNotifier systems.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class WeaponSystem : MonoBehaviour
    {
        [Header("Weapon Setup")]
        public GameObject weaponPrefab; // Assign DarkMoonGreatsword prefab
        public Transform rightHandSocket; // Assign right hand bone/socket
        public Transform backSocket; // Optional - assign to enable sheathing to the back

        [Header("Weapon Fit (drawn / in-hand)")]
        [Tooltip("Local offset from the socket, applied after auto-recentering the mesh bounds. " +
                 "Tune live in Play Mode (select Player > WeaponSystem while attacking) until the " +
                 "blade sits correctly in the hand.")]
        public Vector3 weaponPositionOffset = Vector3.zero;
        [Tooltip("Local rotation offset from the socket, in degrees. Tune live the same way.")]
        public Vector3 weaponRotationOffset = Vector3.zero;

        [Header("Weapon Fit (sheathed / on back)")]
        public Vector3 sheathedPositionOffset = Vector3.zero;
        public Vector3 sheathedRotationOffset = Vector3.zero;

        [Header("Sheathe Toggle")]
        [Tooltip("Requires backSocket to be assigned. Sword stays visible either way - this " +
                 "just swaps it between your hand and your back.")]
        public KeyCode toggleSheatheKey = KeyCode.R;
        private bool isDrawn = true;

        [Header("Hit Detection")]
        public Transform weaponHitbox; // Optional: specific hitbox transform
        public float hitboxRadius = 0.5f;
        [Tooltip("Damage multiplier when hitting an enemy left exposed by a successful parry.")]
        public float riposteDamageMultiplier = 2.5f;

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
            // Auto-equip a starting weapon if one is assigned in the Inspector, sheathed on
            // the back rather than drawn - the player already has it, just not ready to swing
            // until they press the sheathe-toggle key. Leave weaponPrefab empty to start
            // unarmed entirely and equip only via pickups.
            if (weaponPrefab != null)
            {
                Equip(weaponPrefab);
                SetDrawn(false);
            }
        }

        /// <summary>
        /// Attaches a weapon to the hand socket, replacing whatever is currently equipped.
        /// Called automatically on Start if weaponPrefab is assigned; call directly from a
        /// WeaponPickup (or any other source) to equip mid-game.
        /// </summary>
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

            // Counter-scale against whatever the socket inherits from the bone hierarchy above
            // it, so the weapon's WORLD-space size is always 1x1x1 (matching the sword mesh's
            // real ~1m dimensions) regardless of any scale baked into the character rig's bones -
            // a common source of "massive sword" bugs since localScale=1 only guarantees the
            // weapon matches its parent's scale, not any particular real-world size.
            Vector3 parentLossyScale = rightHandSocket.lossyScale;
            weaponObj.transform.localScale = new Vector3(
                parentLossyScale.x != 0 ? 1f / parentLossyScale.x : 1f,
                parentLossyScale.y != 0 ? 1f / parentLossyScale.y : 1f,
                parentLossyScale.z != 0 ? 1f / parentLossyScale.z : 1f);

            weaponInstance = weaponObj.transform;
            isDrawn = true;

            // Get all renderers (mesh may be split across child parts) for visibility control
            weaponRenderers = weaponInstance.GetComponentsInChildren<Renderer>(true);

            // Visible immediately - once equipped it's always shown, either in-hand (drawn)
            // or on the back (sheathed via toggleSheatheKey), not just during attacks.
            SetSwordVisible(true);

            // Setup hitbox - use weapon's collider or create one
            SetupWeaponHitbox();
        }

        /// <summary>Destroys the currently equipped weapon instance, if any, leaving the player unarmed.</summary>
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

        /// <summary>
        /// Enables the weapon renderer. Exposed for animation events / external callers -
        /// LateUpdate() already keeps it visible whenever a weapon is equipped, so this is
        /// only needed if something else has hidden it.
        /// </summary>
        public void EnableSword() => SetSwordVisible(true);

        /// <summary>
        /// Disables the weapon renderer. See EnableSword.
        /// </summary>
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
            if (weaponInstance != null)
            {
                // Use specific hitbox if provided, otherwise use weapon root
                Transform hitboxTransform = weaponHitbox != null ? weaponHitbox : weaponInstance;

                // Ensure we have a collider for hit detection
                weaponCollider = hitboxTransform.GetComponent<Collider>();
                if (weaponCollider == null)
                {
                    // Add sphere collider for hit detection
                    var sphereCollider = hitboxTransform.gameObject.AddComponent<SphereCollider>();
                    sphereCollider.isTrigger = true;
                    sphereCollider.radius = hitboxRadius;

                    // Default center (0,0,0 local) sits at the mesh's own transform origin,
                    // which for this weapon is over a meter from the actual blade (the whole
                    // reason gripAnchorLocal exists - see RecenterOnSocket) - a hitbox left at
                    // the default center would never overlap anything. Center it on the actual
                    // geometry instead. Rotation is still identity at this point in Equip() (set
                    // just above), so InverseTransformPoint here correctly yields the mesh's
                    // local-space center regardless of whatever rotation gets applied later -
                    // Collider.center lives in local space and rotates with the object.
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
                    // Make existing collider a trigger for hit detection
                    weaponCollider.isTrigger = true;
                }

                // Add the hitbox detection component
                weaponHitboxComponent = weaponCollider.gameObject.AddComponent<WeaponHitbox>();
            }
        }

        [Header("Debug Fit Tuning (keyboard-driven, works even with cursor locked)")]
        [Tooltip("Insert: toggle always-visible + tuning controls. Arrow keys: move X/Y. " +
                 "PageUp/PageDown: move Z. Hold Left Shift + those: rotate instead. " +
                 "Backspace: reset offsets to zero. Current values are logged to the Console.")]
        public bool debugFitTuningEnabled = false;

        private const float DebugMoveSpeed = 0.5f;   // units/sec
        private const float DebugRotateSpeed = 90f;  // degrees/sec
        private Vector3 lastLoggedPosition;
        private Vector3 lastLoggedRotation;

        private void LateUpdate()
        {
            if (playerController == null) return;

            HandleDebugFitTuning();

            if (weaponInstance != null && backSocket != null && Input.GetKeyDown(toggleSheatheKey))
                SetDrawn(!isDrawn);

            // Re-applied every frame (not just once in Equip) so the fit offsets can be
            // tuned live during Play Mode.
            //
            // Must run in LateUpdate, not Update: Unity evaluates the Animator (and moves
            // every bone, including the socket's parent) in its own internal phase that
            // runs AFTER Update() but BEFORE LateUpdate(). Reading socket.position from
            // Update() was reading last frame's bone position - invisible while standing
            // still, but a visible trailing lag during any fast animated motion (like a jump
            // attack), which is exactly the "sword floating near but not on the hand" look.
            if (weaponInstance != null)
            {
                Transform activeSocket = (isDrawn || backSocket == null) ? rightHandSocket : backSocket;
                Vector3 positionOffset = isDrawn ? weaponPositionOffset : sheathedPositionOffset;
                Vector3 rotationOffset = isDrawn ? weaponRotationOffset : sheathedRotationOffset;

                // Rotate FIRST, then recenter - the mesh's own pivot is nowhere near the
                // blade (it sits ~1m off in the raw model), so recentering before rotating
                // (the old approach) meant every rotation swung the whole sword away from
                // the socket around a point that wasn't even part of the visible mesh.
                weaponInstance.localRotation = Quaternion.Euler(rotationOffset);
                RecenterOnSocket(activeSocket);
                weaponInstance.position += activeSocket.TransformDirection(positionOffset);
            }

            // Always visible once equipped, in-hand or sheathed - debugFitTuningEnabled no
            // longer needs to force this on, it's already always true.
            SetSwordVisible(true);

            if (weaponCollider != null)
            {
                // Sheathed sword can't hit anything.
                weaponCollider.enabled = isDrawn && IsAttackDamageWindowOpen();
            }
        }

        /// <summary>Swaps the equipped weapon between the hand socket and the back socket.
        /// Re-parents (rather than just repositioning) so the counter-scale in Equip() gets
        /// recomputed against whichever socket's bone actually has it now - hand and back
        /// bones can inherit different scale from the rig.</summary>
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

        [Header("Grip Point")]
        [Tooltip("Local-space point on the weapon mesh that gets placed at the socket - i.e. " +
                 "where the hand actually grips. Centering the whole blade+hilt bounding box " +
                 "(the old behavior) puts the fist partway up the blade instead of on the " +
                 "handle. Default here was computed from DarkMoonGreatsword's actual mesh " +
                 "vertices: the centroid of the vertex cluster at the far end from the blade " +
                 "tip (the pommel/butt of the handle - always the correct end to grip, " +
                 "regardless of how blade/guard/grip blend together in a low-poly mesh with " +
                 "no separate materials to key off of). Re-measure this per-weapon if you " +
                 "swap in a different mesh.")]
        public Vector3 gripAnchorLocal = new Vector3(0.3841f, 0.9597f, 0.5940f);

        /// <summary>
        /// Translates the weapon so gripAnchorLocal (in its CURRENT orientation) sits on the
        /// socket. Must run after rotation is applied, not before - the anchor's world position
        /// shifts with rotation since it isn't at the mesh's own transform origin.
        /// </summary>
        private void RecenterOnSocket(Transform socket)
        {
            if (weaponInstance == null || socket == null)
                return;

            Vector3 gripWorld = weaponInstance.TransformPoint(gripAnchorLocal);
            weaponInstance.position += socket.position - gripWorld;
        }

        private void HandleDebugFitTuning()
        {
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                debugFitTuningEnabled = !debugFitTuningEnabled;
                Debug.Log($"[WeaponSystem] Debug fit tuning {(debugFitTuningEnabled ? "ON - sword always visible. Arrows/PageUp/PageDown move, hold Left Shift to rotate instead, Backspace resets." : "off")}");
            }

            if (!debugFitTuningEnabled) return;

            bool rotateMode = Input.GetKey(KeyCode.LeftShift);
            float moveStep = DebugMoveSpeed * Time.deltaTime;
            float rotateStep = DebugRotateSpeed * Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                weaponPositionOffset = Vector3.zero;
                weaponRotationOffset = Vector3.zero;
                Debug.Log("[WeaponSystem] Offsets reset to zero.");
            }

            if (rotateMode)
            {
                if (Input.GetKey(KeyCode.UpArrow)) weaponRotationOffset.x -= rotateStep;
                if (Input.GetKey(KeyCode.DownArrow)) weaponRotationOffset.x += rotateStep;
                if (Input.GetKey(KeyCode.LeftArrow)) weaponRotationOffset.y -= rotateStep;
                if (Input.GetKey(KeyCode.RightArrow)) weaponRotationOffset.y += rotateStep;
                if (Input.GetKey(KeyCode.PageUp)) weaponRotationOffset.z -= rotateStep;
                if (Input.GetKey(KeyCode.PageDown)) weaponRotationOffset.z += rotateStep;
            }
            else
            {
                if (Input.GetKey(KeyCode.UpArrow)) weaponPositionOffset.y += moveStep;
                if (Input.GetKey(KeyCode.DownArrow)) weaponPositionOffset.y -= moveStep;
                if (Input.GetKey(KeyCode.RightArrow)) weaponPositionOffset.x += moveStep;
                if (Input.GetKey(KeyCode.LeftArrow)) weaponPositionOffset.x -= moveStep;
                if (Input.GetKey(KeyCode.PageUp)) weaponPositionOffset.z += moveStep;
                if (Input.GetKey(KeyCode.PageDown)) weaponPositionOffset.z -= moveStep;
            }

            // Log only when something actually changed, and only a few times a second - not every frame.
            if (Vector3.Distance(lastLoggedPosition, weaponPositionOffset) > 0.02f ||
                Vector3.Distance(lastLoggedRotation, weaponRotationOffset) > 1f)
            {
                lastLoggedPosition = weaponPositionOffset;
                lastLoggedRotation = weaponRotationOffset;
                Debug.Log($"[WeaponSystem] weaponPositionOffset = {weaponPositionOffset:F3}   weaponRotationOffset = {weaponRotationOffset:F1}");
            }
        }

        private bool IsAttackDamageWindowOpen()
        {
            // Check if we're in an attack state and within the damage window
            if (playerController == null || !playerController.IsAttacking)
                return false;

            // Use public properties from PlayerController to check if we're in the damage window
            // The damage window is active when IsInAttackDamageWindow returns true
            return playerController.IsInAttackDamageWindow;
        }

        // Called by WeaponHitbox component when it triggers
        public void OnWeaponHit(Collider other)
        {
            // Prevent hitting self
            if (other.attachedRigidbody != null &&
                other.attachedRigidbody.gameObject == playerController.gameObject)
                return;

            if (other.gameObject == playerController.gameObject)
                return;

            // Apply damage using existing Health system
            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth != null)
            {
                // Determine damage based on which attack button was pressed
                float damageAmount = GetCurrentAttackDamage();

                // Riposte bonus - the target was left exposed by a successful parry
                // (see PlayerController.TryConsumeParry / EnemyAI.GetParried).
                var enemyAI = other.GetComponentInParent<SoulsLike.Enemy.EnemyAI>();
                if (enemyAI != null && enemyAI.IsRiposteVulnerable)
                    damageAmount *= riposteDamageMultiplier;

                targetHealth.ApplyDamage(damageAmount);
            }
        }

        private float GetCurrentAttackDamage()
        {
            // Get damage from PlayerController's public property
            if (playerController != null)
            {
                return playerController.GetAttackDamage();
            }
            return 18f; // Default fallback
        }
    }
}