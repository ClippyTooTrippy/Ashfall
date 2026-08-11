#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using SoulsLike.Enemy;
using SoulsLike.Systems;
using SoulsLike.Player;

namespace SoulsLike.EditorTools
{
    /// <summary>
    /// "no wolf.unity" (the active gameplay/testing scene) was deliberately built without an
    /// enemy while the player/weapon systems were being worked on. A full, working wolf setup
    /// already exists in Assets/ENEMY PROTO.unity though - the real glTF model with WolfAI,
    /// a CapsuleCollider, a populated Animator Controller (real Idle/Run clips), NavMeshAgent,
    /// and Health, all already tuned. This brings that same setup into "no wolf.unity" instead
    /// of building anything from scratch, and bakes the NavMesh on Ground (already has a
    /// NavMeshSurface component sitting there unbaked - m_NavMeshData was {fileID: 0}).
    /// </summary>
    public static class EnemySetupTool
    {
        private const string TargetScenePath = "Assets/no wolf.unity";
        private const string PrototypeScenePath = "Assets/ENEMY PROTO.unity";
        private const string WolfModelPath = "Assets/Models/Wolf/gltf/Wolf-Blender-2.82a.gltf";
        private const string WolfAnimatorControllerPath = "Assets/Scripts/WolfAnimator.controller";

        [MenuItem("Tools/Souls-Like Horror/Add Wolf Enemy To Scene")]
        public static void AddWolfEnemyToScene()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError($"[EnemySetupTool] No GameObject tagged 'Player' found in {TargetScenePath}");
                return;
            }

            GameObject existing = GameObject.Find("Wolf");
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject wolfModel = AssetDatabase.LoadAssetAtPath<GameObject>(WolfModelPath);
            if (wolfModel == null)
            {
                Debug.LogError($"[EnemySetupTool] Could not find wolf model at {WolfModelPath}");
                return;
            }

            GameObject wolf = (GameObject)PrefabUtility.InstantiatePrefab(wolfModel);
            wolf.name = "Wolf";
            wolf.tag = "Enemy"; // LockOnSystem.FindBestTarget() only finds objects tagged this way
            wolf.transform.position = player.transform.position + player.transform.forward * 6f;
            wolf.transform.rotation = Quaternion.LookRotation(-player.transform.forward, Vector3.up);

            var collider = wolf.AddComponent<CapsuleCollider>();
            collider.radius = 0.5f;
            collider.height = 2f;
            collider.center = new Vector3(0f, 1f, 0f);

            // Unity won't fire OnTriggerEnter between two colliders unless at least one side
            // has a Rigidbody - the sword's hitbox (trigger, no Rigidbody) vs this CapsuleCollider
            // (no Rigidbody either) would otherwise never register a hit at all. Kinematic so it
            // doesn't fight NavMeshAgent's direct transform control with physics forces/gravity.
            var rb = wolf.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var animator = wolf.GetComponent<Animator>();
            if (animator == null) animator = wolf.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WolfAnimatorControllerPath);
            animator.runtimeAnimatorController = controller;

            var navAgent = wolf.AddComponent<NavMeshAgent>();
            navAgent.radius = 0.5f;
            navAgent.speed = 3.5f;
            navAgent.acceleration = 8f;
            navAgent.avoidancePriority = 50;
            navAgent.angularSpeed = 120f;
            navAgent.height = 2f;

            var health = wolf.AddComponent<Health>();
            health.maxHealth = 100f;

            var wolfAI = wolf.AddComponent<WolfAI>();
            wolfAI.player = player.transform;
            wolfAI.detectionRange = 10f;
            wolfAI.attackRange = 2f;
            wolfAI.loseSightRange = 20f;
            wolfAI.walkSpeed = 2.5f;
            wolfAI.runSpeed = 4f;
            wolfAI.turnSpeed = 8f;
            wolfAI.attackDamage = 15f;
            wolfAI.attackCooldown = 1.6f;
            wolfAI.telegraphDuration = 0.5f;
            wolfAI.lungeSpeed = 8f;
            wolfAI.lungeDuration = 0.25f;
            wolfAI.telegraphColor = Color.red;
            wolfAI.telegraphScalePunch = 1.15f;
            wolfAI.staggerDuration = 0.5f;

            // Bake the NavMesh so the wolf can actually path to the player - Ground already
            // has a NavMeshSurface component (matching ENEMY PROTO.unity's settings) but it
            // was never baked in this scene (m_NavMeshData was {fileID: 0}).
            GameObject ground = GameObject.Find("Ground");
            if (ground != null)
            {
                var surface = ground.GetComponent<NavMeshSurface>();
                if (surface != null)
                {
                    surface.BuildNavMesh();
                    Debug.Log("[EnemySetupTool] NavMesh baked on Ground.");
                }
                else
                {
                    Debug.LogWarning("[EnemySetupTool] Ground has no NavMeshSurface component - the wolf won't be able to path.");
                }
            }
            else
            {
                Debug.LogWarning("[EnemySetupTool] No 'Ground' object found - couldn't bake a NavMesh.");
            }

            EditorUtility.SetDirty(wolf);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[EnemySetupTool] Wolf added to {TargetScenePath} at {wolf.transform.position:F2}, " +
                      "6m in front of Player. Approach it in Play Mode to trigger detection/chase/attack.");
        }

        /// <summary>
        /// Baked NavMesh data embeds a raw binary blob directly in the scene file even under
        /// Force Text serialization - normal Unity behavior, not corruption, but after the
        /// earlier Player.prefab disaster in this project (duplicate-ownership YAML that hung
        /// the Editor on load) it's worth Unity itself confirming a round-trip load actually
        /// works before trusting a freshly-saved scene, rather than assuming from the outside.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Verify Scene Loads Cleanly")]
        public static void VerifySceneLoadsCleanly()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();

            Debug.Log($"[EnemySetupTool] Scene '{scene.name}' loaded with {roots.Length} root GameObject(s):");
            foreach (var root in roots)
                Debug.Log($"[EnemySetupTool]   - {root.name}");

            GameObject player = GameObject.FindWithTag("Player");
            GameObject wolf = GameObject.Find("Wolf");
            GameObject ground = GameObject.Find("Ground");
            GameObject hud = GameObject.Find("GameHUD");

            Debug.Log($"[EnemySetupTool] Player found: {player != null} | Wolf found: {wolf != null} | " +
                      $"Ground found: {ground != null} | GameHUD found: {hud != null}");

            if (wolf != null)
            {
                var wolfAI = wolf.GetComponent<WolfAI>();
                var navAgent = wolf.GetComponent<NavMeshAgent>();
                var wolfHealth = wolf.GetComponent<Health>();
                var animator = wolf.GetComponent<Animator>();
                Debug.Log($"[EnemySetupTool] Wolf.tag={wolf.tag} WolfAI={wolfAI != null} " +
                          $"NavMeshAgent={navAgent != null} Health={wolfHealth != null} " +
                          $"Animator.controller={(animator != null ? animator.runtimeAnimatorController?.name : "null")} " +
                          $"WolfAI.player={(wolfAI != null && wolfAI.player != null ? wolfAI.player.name : "null")}");
            }

            if (ground != null)
            {
                var surface = ground.GetComponent<NavMeshSurface>();
                Debug.Log($"[EnemySetupTool] Ground NavMeshSurface baked: {(surface != null && surface.navMeshData != null)}");
            }

            Debug.Log("[EnemySetupTool] VERIFY DONE - scene round-tripped without hanging or throwing.");
        }

        /// <summary>
        /// AddWolfEnemyToScene never set an explicit localScale on the wolf - it inherited
        /// whatever the raw model's default import scale is. ENEMY PROTO.unity's wolf reads
        /// correctly sized there, which means it very likely has a non-default scale applied
        /// (an override buried in its prefab-instance modification list that a partial read
        /// missed earlier). Rather than guess a multiplier, this reads ENEMY PROTO's wolf's
        /// ACTUAL resolved lossyScale directly and applies that same value here - real
        /// measured data, not another guess. Also cleans up any stray root-level
        /// "DarkMoonGreatsword" objects (same leak class as before - a probe method's
        /// DestroyImmediate not running, most likely from a Play Mode session that didn't
        /// fully revert before Unity closed).
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Fix Wolf Size And Cleanup")]
        public static void FixWolfSizeAndCleanup()
        {
            EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            GameObject protoWolf = GameObject.Find("Wolf");
            if (protoWolf == null)
            {
                Debug.LogError($"[EnemySetupTool] No 'Wolf' object found in {PrototypeScenePath} to measure scale from.");
                return;
            }
            Vector3 correctScale = protoWolf.transform.lossyScale;
            Renderer[] protoRenderers = protoWolf.GetComponentsInChildren<Renderer>(true);
            Bounds protoBounds = default;
            bool hasBounds = protoRenderers.Length > 0;
            if (hasBounds)
            {
                protoBounds = protoRenderers[0].bounds;
                for (int i = 1; i < protoRenderers.Length; i++) protoBounds.Encapsulate(protoRenderers[i].bounds);
            }
            Debug.Log($"[EnemySetupTool] ENEMY PROTO wolf lossyScale={correctScale:F3}" +
                      (hasBounds ? $" renderer bounds.size={protoBounds.size:F3}" : " (no renderers found)"));

            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("DarkMoonGreatsword") && root.name != "DarkMoonGreatsword_Pickup")
                {
                    Debug.Log($"[EnemySetupTool] Removing stray '{root.name}' at {root.transform.position:F3}");
                    Object.DestroyImmediate(root);
                    removed++;
                }
            }

            GameObject wolf = GameObject.Find("Wolf");
            if (wolf == null)
            {
                Debug.LogError($"[EnemySetupTool] No 'Wolf' object found in {TargetScenePath}. Run Add Wolf Enemy To Scene first.");
                return;
            }

            Vector3 beforeScale = wolf.transform.localScale;
            wolf.transform.localScale = correctScale; // wolf is a root object (no parent), so localScale == lossyScale here

            Renderer[] renderers = wolf.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                Debug.Log($"[EnemySetupTool] Wolf scale {beforeScale:F3} -> {correctScale:F3}, new renderer bounds.size={b.size:F3}");
            }

            EditorUtility.SetDirty(wolf);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[EnemySetupTool] Done - removed {removed} stray sword object(s), wolf rescaled. Saved to {TargetScenePath}.");
        }

        /// <summary>
        /// FixWolfSizeAndCleanup rescaled the wolf's transform to 3x but the CapsuleCollider
        /// still used the same radius/height/center values as before scaling (0.5/2/(0,1,0),
        /// copied straight from ENEMY PROTO) - Unity DOES scale a CapsuleCollider by its
        /// transform's scale automatically, so at 3x those numbers become radius 1.5, height 6,
        /// center 3m up. The actual visual mesh only measures ~2.15m tall, so the collider's
        /// center sits roughly 3m up - about a meter above the TOP of the visible wolf. That's
        /// a real, measurable mismatch, and a very plausible cause for "hitting over it" - the
        /// hit-detection volume floats well above where the wolf actually looks like it is.
        /// Refits the capsule from the wolf's own real measured bounds instead of reusing
        /// unscaled numbers. Also equips the sword for real (calling the actual public Equip())
        /// and logs both colliders' world-space vertical ranges side by side so the vertical
        /// alignment can be checked directly instead of guessed at.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Fix Wolf Collider Fit And Check Sword Overlap")]
        public static void FixWolfColliderFitAndCheckSwordOverlap()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject wolf = GameObject.Find("Wolf");
            if (wolf == null)
            {
                Debug.LogError($"[EnemySetupTool] No 'Wolf' object found in {TargetScenePath}.");
                return;
            }

            Renderer[] wolfRenderers = wolf.GetComponentsInChildren<Renderer>(true);
            if (wolfRenderers.Length == 0)
            {
                Debug.LogError("[EnemySetupTool] Wolf has no renderers to measure.");
                return;
            }
            Bounds wolfBounds = wolfRenderers[0].bounds;
            for (int i = 1; i < wolfRenderers.Length; i++) wolfBounds.Encapsulate(wolfRenderers[i].bounds);

            var capsule = wolf.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                Debug.LogError("[EnemySetupTool] Wolf has no CapsuleCollider.");
                return;
            }

            Vector3 beforeCenter = capsule.center;
            float beforeRadius = capsule.radius;
            float beforeHeight = capsule.height;
            Vector3 beforeWorldMin = wolf.transform.TransformPoint(beforeCenter + Vector3.down * (beforeHeight / 2f));
            Vector3 beforeWorldMax = wolf.transform.TransformPoint(beforeCenter + Vector3.up * (beforeHeight / 2f));

            // Rebuild the capsule from the wolf's OWN measured bounds instead of unscaled
            // hand-me-down numbers. Bounds height is measured directly (real), local-space
            // center/radius are derived by converting the world bounds through the wolf's own
            // inverse transform so they're correct regardless of its current scale.
            Vector3 localMin = wolf.transform.InverseTransformPoint(new Vector3(wolfBounds.center.x, wolfBounds.min.y, wolfBounds.center.z));
            Vector3 localMax = wolf.transform.InverseTransformPoint(new Vector3(wolfBounds.center.x, wolfBounds.max.y, wolfBounds.center.z));
            float localHeight = Mathf.Abs(localMax.y - localMin.y);
            Vector3 localCenter = (localMin + localMax) * 0.5f;

            capsule.height = localHeight;
            capsule.center = localCenter;
            capsule.radius = localHeight * 0.2f; // reasonable torso-width fraction of body height

            Vector3 afterWorldMin = wolf.transform.TransformPoint(capsule.center + Vector3.down * (capsule.height / 2f));
            Vector3 afterWorldMax = wolf.transform.TransformPoint(capsule.center + Vector3.up * (capsule.height / 2f));

            Debug.Log($"[EnemySetupTool] Wolf visual bounds (world): min.y={wolfBounds.min.y:F3} max.y={wolfBounds.max.y:F3}");
            Debug.Log($"[EnemySetupTool] Capsule BEFORE: center={beforeCenter:F3} radius={beforeRadius:F3} height={beforeHeight:F3} " +
                      $"-> world vertical range [{beforeWorldMin.y:F3}, {beforeWorldMax.y:F3}]");
            Debug.Log($"[EnemySetupTool] Capsule AFTER:  center={capsule.center:F3} radius={capsule.radius:F3} height={capsule.height:F3} " +
                      $"-> world vertical range [{afterWorldMin.y:F3}, {afterWorldMax.y:F3}]");

            EditorUtility.SetDirty(wolf);

            // Now equip the sword for real via the actual public Equip() method and measure
            // where its hitbox collider actually sits in world space, so the two can be
            // compared directly instead of guessed at.
            GameObject player = GameObject.FindWithTag("Player");
            var weaponSystem = player != null ? player.GetComponent<WeaponSystem>() : null;
            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DarkMoonGreatsword.prefab");
            if (weaponSystem != null && swordPrefab != null)
            {
                weaponSystem.Equip(swordPrefab);
                var swordCollider = weaponSystem.GetComponentInChildren<SphereCollider>();
                if (swordCollider != null)
                {
                    Vector3 worldCenter = swordCollider.transform.TransformPoint(swordCollider.center);
                    float worldRadius = swordCollider.radius * Mathf.Max(
                        swordCollider.transform.lossyScale.x, swordCollider.transform.lossyScale.y, swordCollider.transform.lossyScale.z);
                    Debug.Log($"[EnemySetupTool] Sword hitbox (drawn, at hand): world center={worldCenter:F3} " +
                              $"world radius~={worldRadius:F3} -> vertical range [{worldCenter.y - worldRadius:F3}, {worldCenter.y + worldRadius:F3}]");
                    Debug.Log($"[EnemySetupTool] Compare against wolf capsule AFTER world vertical range " +
                              $"[{afterWorldMin.y:F3}, {afterWorldMax.y:F3}] above - overlap means a swing at hand height can actually connect.");
                }
                weaponSystem.Unequip();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[EnemySetupTool] Saved to {TargetScenePath}.");
        }

        /// <summary>
        /// FixWolfColliderFitAndCheckSwordOverlap called the real public Equip()/Unequip() to
        /// measure the sword's hitbox for real - but Unequip() calls Destroy(), which is a
        /// Play Mode API that doesn't reliably execute in edit mode (Unity queues it for end of
        /// frame, which never comes in a synchronous batch-mode method before the scene got
        /// saved). Equip() also resets weaponPrefab to non-null as a side effect, which would
        /// silently undo the earlier "start unarmed, only equip via pickup" fix. This repairs
        /// both: force-destroys any leftover weapon instance still parented under the hand
        /// socket with DestroyImmediate (not Destroy), clears weaponPrefab back to null, cleans
        /// up any stray root-level sword object, and re-verifies the wolf's transform/collider
        /// weren't touched by any of this (they shouldn't have been, but confirming beats assuming).
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Repair Equip Test Side Effects")]
        public static void RepairEquipTestSideEffects()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            var weaponSystem = player != null ? player.GetComponent<WeaponSystem>() : null;
            if (weaponSystem == null)
            {
                Debug.LogError("[EnemySetupTool] No WeaponSystem found on Player.");
                return;
            }

            bool hadPrefab = weaponSystem.weaponPrefab != null;
            weaponSystem.weaponPrefab = null;
            EditorUtility.SetDirty(player);
            Debug.Log($"[EnemySetupTool] weaponPrefab was {(hadPrefab ? "SET (bug - would auto-equip on Start)" : "already null")} -> cleared.");

            int destroyedChildren = 0;
            if (weaponSystem.rightHandSocket != null)
            {
                // Iterate a copy - destroying children while enumerating the live Transform
                // would skip entries.
                var children = new System.Collections.Generic.List<GameObject>();
                foreach (Transform child in weaponSystem.rightHandSocket)
                    children.Add(child.gameObject);

                foreach (var child in children)
                {
                    Debug.Log($"[EnemySetupTool] Force-destroying leftover '{child.name}' under RightHandSocket.");
                    Object.DestroyImmediate(child);
                    destroyedChildren++;
                }
            }

            if (weaponSystem.backSocket != null)
            {
                var children = new System.Collections.Generic.List<GameObject>();
                foreach (Transform child in weaponSystem.backSocket)
                    children.Add(child.gameObject);
                foreach (var child in children)
                {
                    Debug.Log($"[EnemySetupTool] Force-destroying leftover '{child.name}' under BackSocket.");
                    Object.DestroyImmediate(child);
                    destroyedChildren++;
                }
            }

            int removedStray = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("DarkMoonGreatsword") && root.name != "DarkMoonGreatsword_Pickup")
                {
                    Debug.Log($"[EnemySetupTool] Removing stray root '{root.name}'.");
                    Object.DestroyImmediate(root);
                    removedStray++;
                }
            }

            // Re-verify the wolf wasn't touched - it shouldn't have been (nothing in the
            // faulty script wrote to it), but confirming directly beats assuming after a bug.
            GameObject wolf = GameObject.Find("Wolf");
            if (wolf != null)
            {
                var capsule = wolf.GetComponent<CapsuleCollider>();
                Debug.Log($"[EnemySetupTool] Wolf check - scale={wolf.transform.localScale:F3} " +
                          $"capsule.height={(capsule != null ? capsule.height.ToString("F3") : "null")} " +
                          $"capsule.center={(capsule != null ? capsule.center.ToString("F3") : "null")}");
            }
            else
            {
                Debug.LogWarning("[EnemySetupTool] Wolf not found at all - this would be a real problem.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[EnemySetupTool] Repair done - destroyed {destroyedChildren} leftover weapon child object(s), " +
                      $"removed {removedStray} stray root object(s). Saved to {TargetScenePath}.");
        }
    }
}
#endif
