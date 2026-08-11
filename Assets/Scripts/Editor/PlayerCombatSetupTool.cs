#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using SoulsLike.Player;
using SoulsLike.CameraSystem;

namespace SoulsLike.EditorTools
{
    /// <summary>
    /// Roll (with i-frames) and lock-on are both already fully implemented in
    /// PlayerController.cs / LockOnSystem.cs - but PlayerController.OnAnimatorActionComplete()
    /// (the thing that actually returns the player from Rolling/Attacking back to Free) only
    /// gets called by ActionStateNotifier, a StateMachineBehaviour that has to be manually
    /// attached to the Roll and Attack states inside the Animator Controller itself - it can't
    /// be wired via code on the MonoBehaviour side. RebuildPlayerPrefab's own comments flagged
    /// this as never done ("add it to the Roll/Attack states... once you build one"). If it's
    /// still missing, rolling would soft-lock the player in the Rolling state forever - this
    /// checks and fixes that, and double-checks LockOnSystem is actually wired to
    /// PlayerController.lockOn while it's at it.
    /// </summary>
    public static class PlayerCombatSetupTool
    {
        private const string TargetScenePath = "Assets/no wolf.unity";

        [MenuItem("Tools/Souls-Like Horror/Diagnose And Fix Player Animator States")]
        public static void DiagnoseAndFixPlayerAnimatorStates()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError($"[PlayerCombatSetupTool] No GameObject tagged 'Player' found in {TargetScenePath}");
                return;
            }

            Animator animator = player.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("[PlayerCombatSetupTool] Player has no Animator component - Roll/Attack state-exit " +
                                "callbacks can't work, and neither can any animation at all.");
                return;
            }

            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                Debug.LogError($"[PlayerCombatSetupTool] Player's Animator has no editable AnimatorController assigned " +
                                $"(runtimeAnimatorController={animator.runtimeAnimatorController}). Roll/Attack will " +
                                "NEVER return to the Free state - the player would be permanently stuck after the first roll or attack.");
                return;
            }

            Debug.Log($"[PlayerCombatSetupTool] Player Animator uses controller '{controller.name}' with {controller.layers.Length} layer(s).");

            bool anyChanged = false;
            foreach (var layer in controller.layers)
            {
                foreach (var childState in layer.stateMachine.states)
                {
                    AnimatorState state = childState.state;
                    bool isRollOrAttack = state.name.IndexOf("Roll", System.StringComparison.OrdinalIgnoreCase) >= 0
                                        || state.name.IndexOf("Attack", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!isRollOrAttack) continue;

                    bool hasNotifier = false;
                    foreach (var behaviour in state.behaviours)
                    {
                        if (behaviour is ActionStateNotifier) { hasNotifier = true; break; }
                    }

                    Debug.Log($"[PlayerCombatSetupTool] State '{state.name}' (layer '{layer.name}') - ActionStateNotifier attached: {hasNotifier}");

                    if (!hasNotifier)
                    {
                        state.AddStateMachineBehaviour<ActionStateNotifier>();
                        Debug.Log($"[PlayerCombatSetupTool]   -> Added ActionStateNotifier to '{state.name}'.");
                        anyChanged = true;
                    }
                }
            }

            if (anyChanged)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Debug.Log("[PlayerCombatSetupTool] Controller modified and saved - Roll/Attack will now correctly return to Free.");
            }
            else
            {
                Debug.Log("[PlayerCombatSetupTool] All Roll/Attack states already had ActionStateNotifier - nothing to fix there.");
            }

            var playerController = player.GetComponent<PlayerController>();
            var lockOn = player.GetComponent<LockOnSystem>();
            Debug.Log($"[PlayerCombatSetupTool] LockOnSystem present: {lockOn != null}, " +
                      $"PlayerController.lockOn wired: {(playerController != null && playerController.lockOn != null)}, " +
                      $"toggle key: {(lockOn != null ? lockOn.toggleKey.ToString() : "n/a")}, " +
                      $"enemyTag: {(lockOn != null ? lockOn.enemyTag : "n/a")}");

            if (playerController != null && lockOn != null && playerController.lockOn == null)
            {
                playerController.lockOn = lockOn;
                EditorUtility.SetDirty(player);
                Debug.Log("[PlayerCombatSetupTool] Wired PlayerController.lockOn -> LockOnSystem (was null).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[PlayerCombatSetupTool] DONE.");
        }

        /// <summary>
        /// The animation pack has no dedicated roll/dodge/evade clip at all (confirmed by
        /// name search across every .fbx in Assets/Animations), so whatever built
        /// PlayerAutoGenerated.controller fell back to wiring the "Roll" state to
        /// 1Hand_Up_Crouch_F_InPlace - same fileID/guid, confirmed byte-for-byte identical to
        /// the real Crouch state's motion. Movement during the roll is already driven entirely
        /// by code (PlayerController.GetRollMotion), not root motion, so the clip only needs
        /// to look like a burst of evasive motion rather than match distance/timing - a jump
        /// clip is the closest fit available. Uses the same "_InPlace, non-AS_" clip that
        /// every other correctly-wired action state in this controller already follows.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Fix Roll Animation Clip")]
        public static void FixRollAnimationClip()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            Animator animator = player.GetComponentInChildren<Animator>();
            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                Debug.LogError("[PlayerCombatSetupTool] No editable AnimatorController on Player's Animator.");
                return;
            }

            AnimationClip replacement = FindClipByName("1Hand_Up_Jump_B_InPlace");
            if (replacement == null)
            {
                Debug.LogError("[PlayerCombatSetupTool] Could not find 1Hand_Up_Jump_B_InPlace clip.");
                return;
            }

            bool found = false;
            foreach (var layer in controller.layers)
            {
                foreach (var childState in layer.stateMachine.states)
                {
                    AnimatorState state = childState.state;
                    if (state.name != "Roll") continue;

                    Motion before = state.motion;
                    state.motion = replacement;
                    found = true;
                    Debug.Log($"[PlayerCombatSetupTool] Roll motion changed from '{(before != null ? before.name : "null")}' " +
                              $"to '{replacement.name}'.");
                }
            }

            if (!found)
            {
                Debug.LogError("[PlayerCombatSetupTool] No state named 'Roll' found in the controller.");
                return;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[PlayerCombatSetupTool] Roll animation fixed and saved.");
        }

        private static AnimationClip FindClipByName(string clipName)
        {
            string[] guids = AssetDatabase.FindAssets(clipName + " t:AnimationClip");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && clip.name == clipName)
                        return clip;
                }
            }
            return null;
        }

        /// <summary>
        /// The player now spawns with the sword already equipped (sheathed), so the floor
        /// pickup is redundant - removes it. Also checks/fixes Animator.applyRootMotion on the
        /// Player: PlayerController drives 100% of movement itself (CharacterController.Move()
        /// from code-computed vectors in GetFreeMovement/GetRollMotion - no reference to
        /// animator deltas anywhere), so root motion must be off. If it were on, any clip with
        /// baked vertical motion - like the jump clip now substituted in for Roll - would
        /// physically lift the actual character transform, fighting the controller's own
        /// gravity handling and very plausibly explaining "jumps up and stays kinda in air".
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Remove Floor Pickup And Fix Root Motion")]
        public static void RemoveFloorPickupAndFixRootMotion()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject pickup = GameObject.Find("DarkMoonGreatsword_Pickup");
            if (pickup != null)
            {
                Object.DestroyImmediate(pickup);
                Debug.Log("[PlayerCombatSetupTool] Removed DarkMoonGreatsword_Pickup - redundant now that the player spawns armed.");
            }
            else
            {
                Debug.Log("[PlayerCombatSetupTool] No floor pickup found - already removed.");
            }

            GameObject player = GameObject.FindWithTag("Player");
            Animator animator = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (animator != null)
            {
                bool wasOn = animator.applyRootMotion;
                Debug.Log($"[PlayerCombatSetupTool] Player Animator.applyRootMotion = {wasOn}" +
                          (wasOn ? " - THIS is very likely the jump/float bug: the jump clip now used for " +
                                   "Roll has baked vertical motion that root motion would apply directly to " +
                                   "the character's actual transform, on top of PlayerController's own " +
                                   "code-driven movement and gravity." : " - already off, not the cause here."));
                if (wasOn)
                {
                    animator.applyRootMotion = false;
                    EditorUtility.SetDirty(animator);
                    Debug.Log("[PlayerCombatSetupTool] Set applyRootMotion = false.");
                }
            }
            else
            {
                Debug.LogWarning("[PlayerCombatSetupTool] No Animator found on Player.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[PlayerCombatSetupTool] Saved to {TargetScenePath}.");
        }
    }
}
#endif
