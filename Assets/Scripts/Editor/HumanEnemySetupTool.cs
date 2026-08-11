#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using SoulsLike.Enemy;
using SoulsLike.Systems;
using SoulsLike.Player;

namespace SoulsLike.EditorTools
{
    /// <summary>
    /// Builds a human sword-fighting enemy from assets already sitting unused in the project:
    /// DoubleL's "Armature (1).prefab" (a fully-rigged Humanoid character - the exact rig the
    /// whole DoubleL animation pack was authored for, so no retargeting risk, unlike the
    /// separate "Female knight" model which imports as a Generic rig), a sword model
    /// (SM_Wep_Sword_03.fbx), and real standalone AnimationClip assets in
    /// Assets/DoubleL/Demo/Anim/ (Idle, Run, a clip literally named Enemy_Attack_1, and a hit
    /// reaction). Drives EnemyAI.cs (already built for exactly this kind of generic humanoid
    /// combat AI, as opposed to WolfAI.cs's wolf-specific lunge/telegraph behavior).
    /// </summary>
    public static class HumanEnemySetupTool
    {
        private const string TargetScenePath = "Assets/no wolf.unity";
        private const string ArmaturePrefabPath = "Assets/DoubleL/Model/Armature (1).prefab";
        private const string SwordModelPath = "Assets/DoubleL/Model/SM_Wep_Sword_03.fbx";
        private const string ControllerPath = "Assets/Animations/HumanEnemyAnimator.controller";

        private const string IdleClipPath = "Assets/DoubleL/Demo/Anim/OneHand_Up_Idle.anim";
        private const string RunClipPath = "Assets/DoubleL/Demo/Anim/OneHand_Up_Run_F_InPlace.anim";
        private const string AttackClipPath = "Assets/DoubleL/Demo/Anim/Enemy_Attack_1_InPlace.anim";
        private const string HitClipPath = "Assets/DoubleL/Demo/Anim/Hit_F_1_InPlace.anim";

        [MenuItem("Tools/Souls-Like Horror/Build Human Enemy Animator Controller")]
        public static void BuildHumanEnemyAnimatorController()
        {
            AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
            AnimationClip runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RunClipPath);
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
            AnimationClip hitClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HitClipPath);

            if (idleClip == null || runClip == null || attackClip == null || hitClip == null)
            {
                Debug.LogError($"[HumanEnemySetupTool] Missing clip(s) - idle={idleClip != null} " +
                                $"run={runClip != null} attack={attackClip != null} hit={hitClip != null}");
                return;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger); // no dedicated
            // death clip exists in this pack - parameter still added so EnemyAI's SetTrigger("Death")
            // doesn't spam a missing-parameter warning; a real death animation is a later polish item.

            var rootSM = controller.layers[0].stateMachine;

            var idleState = rootSM.AddState("Idle");
            idleState.motion = idleClip;
            rootSM.defaultState = idleState;

            var runState = rootSM.AddState("Run");
            runState.motion = runClip;

            var attackState = rootSM.AddState("Attack");
            attackState.motion = attackClip;

            var hitState = rootSM.AddState("Hit");
            hitState.motion = hitClip;

            var idleToRun = idleState.AddTransition(runState);
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0.1f;

            var runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.1f;

            var toAttack = rootSM.AddAnyStateTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;
            toAttack.canTransitionToSelf = false;

            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.85f;
            attackToIdle.duration = 0.15f;
            attackToIdle.hasFixedDuration = true;

            var toHit = rootSM.AddAnyStateTransition(hitState);
            toHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            toHit.hasExitTime = false;
            toHit.duration = 0.1f;
            toHit.canTransitionToSelf = false;

            var hitToIdle = hitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.85f;
            hitToIdle.duration = 0.15f;
            hitToIdle.hasFixedDuration = true;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[HumanEnemySetupTool] Built {ControllerPath} - Idle/Run/Attack/Hit states, " +
                      "Speed/Attack/Hit/Death parameters.");
        }

        [MenuItem("Tools/Souls-Like Horror/Add Human Enemy To Scene")]
        public static void AddHumanEnemyToScene()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError($"[HumanEnemySetupTool] No GameObject tagged 'Player' found in {TargetScenePath}");
                return;
            }

            GameObject existing = GameObject.Find("HumanEnemy");
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject armaturePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmaturePrefabPath);
            if (armaturePrefab == null)
            {
                Debug.LogError($"[HumanEnemySetupTool] Could not find {ArmaturePrefabPath}");
                return;
            }

            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(armaturePrefab);
            enemy.name = "HumanEnemy";
            enemy.tag = "Enemy";
            // Offset sideways from the wolf's spawn spot (6m forward) so they don't overlap.
            enemy.transform.position = player.transform.position + player.transform.forward * 8f - player.transform.right * 3f;
            enemy.transform.rotation = Quaternion.LookRotation(-player.transform.forward, Vector3.up);

            var animator = enemy.GetComponent<Animator>();
            if (animator == null) animator = enemy.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[HumanEnemySetupTool] {ControllerPath} not found - run 'Build Human Enemy " +
                                "Animator Controller' first.");
                return;
            }
            animator.runtimeAnimatorController = controller;

            // Measure the ACTUAL mesh bounds to fit the collider - learned this the hard way with
            // the wolf (a copy-pasted 1x-scale collider became a 6m-tall floating hitbox once the
            // wolf's transform got rescaled). This character isn't being rescaled, but fitting from
            // real geometry instead of a guessed constant avoids repeating that mistake.
            Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(true);
            var capsule = enemy.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = enemy.AddComponent<CapsuleCollider>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                Vector3 localMin = enemy.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
                Vector3 localMax = enemy.transform.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
                float localHeight = Mathf.Abs(localMax.y - localMin.y);
                capsule.height = localHeight;
                capsule.center = (localMin + localMax) * 0.5f;
                capsule.radius = localHeight * 0.18f;
                Debug.Log($"[HumanEnemySetupTool] Fitted collider from measured bounds: height={localHeight:F3} " +
                          $"center={capsule.center:F3} radius={capsule.radius:F3}");
            }
            else
            {
                Debug.LogWarning("[HumanEnemySetupTool] No renderers found to measure - collider left at default size.");
            }

            // Same reasoning as the wolf fix: a trigger vs non-trigger pair needs at least one
            // Rigidbody or Unity won't fire OnTriggerEnter between them at all.
            var rb = enemy.GetComponent<Rigidbody>();
            if (rb == null) rb = enemy.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var navAgent = enemy.GetComponent<NavMeshAgent>();
            if (navAgent == null) navAgent = enemy.AddComponent<NavMeshAgent>();
            navAgent.radius = Mathf.Max(0.3f, capsule.radius);
            navAgent.height = Mathf.Max(1f, capsule.height);
            navAgent.speed = 3.5f;
            navAgent.acceleration = 8f;
            navAgent.angularSpeed = 360f;

            var health = enemy.GetComponent<Health>();
            if (health == null) health = enemy.AddComponent<Health>();
            health.maxHealth = 120f;

            var enemyAI = enemy.GetComponent<EnemyAI>();
            if (enemyAI == null) enemyAI = enemy.AddComponent<EnemyAI>();
            enemyAI.player = player.transform;
            enemyAI.animator = animator;
            enemyAI.sightRange = 12f;
            enemyAI.sightAngle = 120f;
            enemyAI.loseSightRange = 18f;
            enemyAI.attackRange = 2.2f;
            enemyAI.attackCooldown = 1.8f;
            enemyAI.attackDamage = 20f;
            enemyAI.attackWindup = 0.5f;
            enemyAI.staggerDuration = 0.6f;
            enemyAI.riposteVulnerableDuration = 2.5f;

            // The Armature prefab is DoubleL's own demo character, and it ships PRE-EQUIPPED
            // with SM_Wep_Sword_03 already parented to the hand bone (confirmed via
            // DiagnoseAndFixTwoSwords - it's a raw child of Right_Hand in the untouched prefab
            // asset itself). No need to attach anything - it's already there, and presumably
            // positioned correctly by whoever built the pack, unlike a manually bolted-on copy.
            Transform handBone = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            bool hasPrefabSword = handBone != null && handBone.Find("SM_Wep_Sword_03") != null;
            Debug.Log($"[HumanEnemySetupTool] Prefab's own pre-equipped sword present: {hasPrefabSword} " +
                      "(not attaching a duplicate).");

            EditorUtility.SetDirty(enemy);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[HumanEnemySetupTool] HumanEnemy added to {TargetScenePath} at {enemy.transform.position:F2}.");
        }

        /// <summary>
        /// The screenshot showed a comically oversized sword (like a giant lance) and a
        /// human-looking-tiny enemy - the exact same "massive weapon" bug class the player's own
        /// DarkMoonGreatsword had at the very start of this project (inherited/unnormalized
        /// scale from its parent bone). The enemy's sword was attached with a bare identity
        /// transform and no counter-scaling at all, unlike WeaponSystem.Equip() which explicitly
        /// counter-scales against the socket's lossyScale. This measures the real numbers for
        /// both the enemy body and the sword, then applies the same counter-scale fix.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Diagnose And Fix Human Enemy Scale")]
        public static void DiagnoseAndFixHumanEnemyScale()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject enemy = GameObject.Find("HumanEnemy");
            if (enemy == null)
            {
                Debug.LogError("[HumanEnemySetupTool] No 'HumanEnemy' found in scene.");
                return;
            }

            Debug.Log($"[HumanEnemySetupTool] HumanEnemy.transform.localScale={enemy.transform.localScale:F4} " +
                      $"lossyScale={enemy.transform.lossyScale:F4}");

            var animator = enemy.GetComponent<Animator>();
            Transform handBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (handBone == null)
            {
                Debug.LogError("[HumanEnemySetupTool] No RightHand bone found.");
                return;
            }
            Debug.Log($"[HumanEnemySetupTool] Hand bone lossyScale={handBone.lossyScale:F4}");

            Transform sword = handBone.Find("EnemySword");
            if (sword == null)
            {
                Debug.LogError("[HumanEnemySetupTool] No 'EnemySword' child found under the hand bone.");
                return;
            }

            Renderer[] swordRenderersBefore = sword.GetComponentsInChildren<Renderer>(true);
            if (swordRenderersBefore.Length > 0)
            {
                Bounds b = swordRenderersBefore[0].bounds;
                for (int i = 1; i < swordRenderersBefore.Length; i++) b.Encapsulate(swordRenderersBefore[i].bounds);
                Debug.Log($"[HumanEnemySetupTool] Sword BEFORE fix - world bounds.size={b.size:F3} " +
                          $"(current localScale={sword.localScale:F4})");
            }

            // Same technique WeaponSystem.Equip() uses for the player's own weapon: counter-scale
            // against whatever the parent bone inherits, so the sword's WORLD size matches its
            // own real ~1m mesh dimensions regardless of any scale baked into the rig.
            Vector3 parentLossy = handBone.lossyScale;
            sword.localScale = new Vector3(
                parentLossy.x != 0 ? 1f / parentLossy.x : 1f,
                parentLossy.y != 0 ? 1f / parentLossy.y : 1f,
                parentLossy.z != 0 ? 1f / parentLossy.z : 1f);

            Renderer[] swordRenderersAfter = sword.GetComponentsInChildren<Renderer>(true);
            if (swordRenderersAfter.Length > 0)
            {
                Bounds b = swordRenderersAfter[0].bounds;
                for (int i = 1; i < swordRenderersAfter.Length; i++) b.Encapsulate(swordRenderersAfter[i].bounds);
                Debug.Log($"[HumanEnemySetupTool] Sword AFTER fix - world bounds.size={b.size:F3} " +
                          $"localScale={sword.localScale:F4}");
            }

            // Cross-check the body measurement too, for comparison against the sword.
            Renderer[] bodyRenderers = enemy.GetComponentsInChildren<Renderer>(true);
            Bounds bodyBounds = default;
            bool hasBody = false;
            foreach (var r in bodyRenderers)
            {
                if (r.transform.IsChildOf(sword)) continue; // exclude the sword itself
                if (!hasBody) { bodyBounds = r.bounds; hasBody = true; }
                else bodyBounds.Encapsulate(r.bounds);
            }
            if (hasBody)
                Debug.Log($"[HumanEnemySetupTool] Enemy body (excluding sword) world bounds.size={bodyBounds.size:F3} " +
                          $"min.y={bodyBounds.min.y:F3} max.y={bodyBounds.max.y:F3}");

            EditorUtility.SetDirty(sword.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[HumanEnemySetupTool] Saved to {TargetScenePath}.");
        }

        /// <summary>
        /// The sword's SIZE turned out fine (see DiagnoseAndFixHumanEnemyScale) - the "giant
        /// lance" look was its ROTATION: attached at identity, so whatever direction its raw
        /// mesh axes happen to point is whatever direction it renders, unrelated to a natural
        /// held pose. This is a decorative NPC prop with no hit detection riding on it (EnemyAI
        /// damages by distance check, not a physical hitbox), so it doesn't need the full
        /// grip-anchor treatment the player's own sword needed - just measure the mesh's real
        /// long axis (same probe-at-origin technique used throughout this project) and align it
        /// to point up, a reasonable "blade up" resting pose.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Fix Human Enemy Sword Rotation")]
        public static void FixEnemySwordRotation()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject enemy = GameObject.Find("HumanEnemy");
            var animator = enemy != null ? enemy.GetComponent<Animator>() : null;
            Transform handBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (handBone == null)
            {
                Debug.LogError("[HumanEnemySetupTool] No RightHand bone found on HumanEnemy.");
                return;
            }
            Transform sword = handBone.Find("EnemySword");
            if (sword == null)
            {
                Debug.LogError("[HumanEnemySetupTool] No 'EnemySword' found under the hand bone.");
                return;
            }

            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordModelPath);
            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
            Vector3 localSize;
            try
            {
                probe.transform.position = Vector3.zero;
                probe.transform.rotation = Quaternion.identity;
                probe.transform.localScale = Vector3.one;
                Renderer[] renderers = probe.GetComponentsInChildren<Renderer>(true);
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                localSize = b.size;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }

            Debug.Log($"[HumanEnemySetupTool] Sword local-space size: {localSize:F3}");

            Vector3 longAxisLocal;
            if (localSize.x >= localSize.y && localSize.x >= localSize.z) longAxisLocal = Vector3.right;
            else if (localSize.y >= localSize.x && localSize.y >= localSize.z) longAxisLocal = Vector3.up;
            else longAxisLocal = Vector3.forward;

            Quaternion rotation = Quaternion.FromToRotation(longAxisLocal, Vector3.up);
            sword.localRotation = rotation;

            EditorUtility.SetDirty(sword.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[HumanEnemySetupTool] Sword long axis was local {longAxisLocal} -> rotated to point up. " +
                      $"Saved to {TargetScenePath}.");
        }

        /// <summary>
        /// The enemy's own body measured 1.76m tall in isolation, which reads as a normal human
        /// height - but "looks like a midget" is a comparison AGAINST the player, not an
        /// absolute judgment. Measures both characters' actual real body heights the same way
        /// and compares directly, then rescales the enemy to match the player's proportions if
        /// they genuinely differ, instead of assuming the enemy's own number in isolation was
        /// good enough.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Compare And Fix Enemy Height Vs Player")]
        public static void CompareAndFixEnemyHeightVsPlayer()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            GameObject enemy = GameObject.Find("HumanEnemy");
            if (player == null || enemy == null)
            {
                Debug.LogError($"[HumanEnemySetupTool] Player found={player != null} HumanEnemy found={enemy != null}");
                return;
            }

            // The previous run of this method measured player height WITHOUT excluding a
            // weapon and got 3.955m - clearly wrong for a person, and it fed a bad 2.245x scale
            // into the enemy. Start clean: reset that bad scale, and force-remove any stray
            // weapon instance under either hand socket first (same DestroyImmediate-vs-Destroy
            // issue as the earlier "Repair Equip Test Side Effects" bug - Equip()/Unequip() calls
            // elsewhere in this tool use the real Destroy(), which doesn't reliably run in edit
            // mode, and it's easy for that to leave something behind unnoticed).
            enemy.transform.localScale = Vector3.one;

            var weaponSystem = player.GetComponent<WeaponSystem>();
            int strayRemoved = 0;
            if (weaponSystem != null)
            {
                if (weaponSystem.rightHandSocket != null)
                    strayRemoved += DestroyAllChildren(weaponSystem.rightHandSocket);
                if (weaponSystem.backSocket != null)
                    strayRemoved += DestroyAllChildren(weaponSystem.backSocket);
            }
            if (strayRemoved > 0)
                Debug.Log($"[HumanEnemySetupTool] Removed {strayRemoved} stray weapon object(s) from the player before measuring.");

            float playerHeight = MeasureStandingHeight(player, null);

            // Exclude the sword from the enemy's height measurement.
            Transform swordToExclude = null;
            var animator = enemy.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null) swordToExclude = hand.Find("EnemySword");
            }
            float enemyHeight = MeasureStandingHeight(enemy, swordToExclude);

            Debug.Log($"[HumanEnemySetupTool] Player real height={playerHeight:F3}m, HumanEnemy real height={enemyHeight:F3}m, " +
                      $"ratio={(playerHeight > 0 ? enemyHeight / playerHeight : 0):F3}");

            if (playerHeight <= 0f || enemyHeight <= 0f)
            {
                Debug.LogError("[HumanEnemySetupTool] Could not measure one or both heights (no renderers found).");
                return;
            }

            // Confirmed with the user: the player really does measure ~4m (its own visual mesh
            // vs. its ~2m CharacterController capsule are mismatched) and they chose to match
            // the enemy to the player rather than fix the player's scale - so this is expected,
            // not bad data to guard against anymore.
            float scaleFactor = playerHeight / enemyHeight;
            enemy.transform.localScale = Vector3.one * scaleFactor;

            float afterHeight = MeasureStandingHeight(enemy, swordToExclude);
            Debug.Log($"[HumanEnemySetupTool] Rescaled enemy to {enemy.transform.localScale:F3} " +
                      $"(x{scaleFactor:F3}) - new measured height={afterHeight:F3}m (should now match player's {playerHeight:F3}m).");

            EditorUtility.SetDirty(enemy);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[HumanEnemySetupTool] Saved to {TargetScenePath}.");
        }

        private static int DestroyAllChildren(Transform parent)
        {
            var children = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in parent)
                children.Add(child.gameObject);
            foreach (var child in children)
                Object.DestroyImmediate(child);
            return children.Count;
        }

        private static float MeasureStandingHeight(GameObject root, Transform exclude)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool has = false;
            foreach (var r in renderers)
            {
                if (exclude != null && r.transform.IsChildOf(exclude)) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has ? bounds.size.y : 0f;
        }

        // (A DiagnoseAndFixTwoSwords method existed here and was deleted - it mistook real
        // finger bone Transforms for extra weapon objects and destroyed them, which risked
        // corrupting the skinned mesh's bind pose. The real fix is in AddHumanEnemyToScene:
        // don't attach a duplicate sword at all, since the prefab already ships with one.)

        /// <summary>
        /// Rebuilds HumanEnemy completely fresh from the untouched source prefab - needed after
        /// the finger-bone deletion mistake above, since that scene instance may have a
        /// corrupted skin binding. The prefab ASSET itself was never touched, only the scene
        /// instance, so this is a clean slate. Re-applies AddHumanEnemyToScene's setup, then
        /// the height match to the player.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Rebuild Human Enemy Fresh")]
        public static void RebuildHumanEnemyFresh()
        {
            AddHumanEnemyToScene();
            CompareAndFixEnemyHeightVsPlayer();
        }
    }
}
#endif
