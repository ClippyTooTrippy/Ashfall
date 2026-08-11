#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SoulsLike.Player;
using SoulsLike.Systems;
using SoulsLike.CameraSystem;

namespace SoulsLike.EditorTools
{
    /// <summary>
    /// The old Player prefab had gotten corrupted at the YAML level - two different
    /// Transform components both claimed ownership of the same GameObject, and the
    /// weapon child's MeshFilter pointed at an empty inline mesh instead of the real
    /// DarkMoonGreatsword.obj. Loading that file with PrefabUtility.LoadPrefabContents
    /// hangs the Editor because the engine's hierarchy traversal chokes on the
    /// duplicate-ownership data.
    ///
    /// This tool never touches the broken file. It builds a fresh, temporary Player
    /// GameObject from scratch (every field here matches what the corrupted prefab
    /// already had, since none of it was hand-tuned beyond the script defaults),
    /// saves it as Assets/Prefabs/Player.prefab, then discards the temp object.
    /// </summary>
    public static class WeaponSetupTool
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string SwordModelPath = "Assets/Models/DarkMoonGreatsword.obj";
        private const string SwordPrefabPath = "Assets/Prefabs/DarkMoonGreatsword.prefab";

        [MenuItem("Tools/Souls-Like Horror/Rebuild Player Prefab (fixes corrupted weapon setup)")]
        public static void RebuildPlayerPrefab()
        {
            GameObject swordModel = AssetDatabase.LoadAssetAtPath<GameObject>(SwordModelPath);
            if (swordModel == null)
            {
                Debug.LogError($"[WeaponSetupTool] Could not find sword model at {SwordModelPath}");
                return;
            }

            // Clean, standalone weapon prefab from the imported model.
            GameObject swordInstance = (GameObject)PrefabUtility.InstantiatePrefab(swordModel);
            swordInstance.name = "DarkMoonGreatsword";
            GameObject swordPrefab = PrefabUtility.SaveAsPrefabAsset(swordInstance, SwordPrefabPath);
            Object.DestroyImmediate(swordInstance);

            EnsureTag("Player");

            // Built as a loose GameObject (never added to any scene) so nothing in the
            // currently open scene or the corrupted prefab is ever loaded or referenced.
            GameObject player = null;
            try
            {
                player = new GameObject("Player");
                player.tag = "Player";

                var cc = player.AddComponent<CharacterController>();
                cc.radius = 0.4f;
                cc.center = new Vector3(0f, 1f, 0f);

                player.AddComponent<Health>();
                player.AddComponent<Stamina>();
                var lockOn = player.AddComponent<LockOnSystem>();

                var controller = player.AddComponent<PlayerController>();
                controller.hittableLayers = ~0; // everything
                controller.lockOn = lockOn;
                // cameraTransform intentionally left null - PlayerController.Awake()
                // resolves it to Camera.main automatically, same as TestSceneBuilder does.

                // NOTE: ActionStateNotifier is a StateMachineBehaviour, not a Component -
                // it can't be AddComponent'd onto a GameObject (the old prefab tried to,
                // which is invalid data Unity's own Inspector would never let you create).
                // It belongs on the Roll/Attack states inside your Animator Controller,
                // once you build one: select the state -> Add Behaviour -> ActionStateNotifier.

                var socket = new GameObject("RightHandSocket");
                socket.transform.SetParent(player.transform);
                socket.transform.localPosition = new Vector3(0.3f, 1f, 0.2f);
                socket.transform.localRotation = Quaternion.identity;

                var weaponSystem = player.AddComponent<WeaponSystem>();
                weaponSystem.weaponPrefab = swordPrefab;
                weaponSystem.rightHandSocket = socket.transform;

                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);

                Debug.Log("[WeaponSetupTool] Player.prefab rebuilt cleanly. The old corrupted file was " +
                          "backed up to Assets/Prefabs/Player.prefab.broken.bak - delete it once you've " +
                          "confirmed everything works. NOTES: (1) RightHandSocket is a placeholder transform " +
                          "near the player's root, not parented to a hand bone, since there's no rigged " +
                          "character model/Animator on the prefab yet. (2) ActionStateNotifier was NOT " +
                          "re-added as a component (it's a StateMachineBehaviour, invalid on a GameObject) - " +
                          "add it to the Roll/Attack states in your Animator Controller once you build one.");
            }
            finally
            {
                if (player != null)
                    Object.DestroyImmediate(player);
            }
        }

        private const string TargetScenePath = "Assets/no wolf.unity";

        /// <summary>
        /// The Player.prefab fix doesn't touch this scene's Player - it's a standalone
        /// object (see TestSceneBuilder), not a prefab instance, and never had a
        /// WeaponSystem. Separately, a real rigged character ("FemaleCharacter", from
        /// test.fbx) got parented under Player with a Humanoid Animator, so there's now
        /// an actual hand bone to attach to instead of a floating placeholder transform.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Attach Weapon To Scene Character")]
        public static void AttachWeaponToSceneCharacter()
        {
            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            if (swordPrefab == null)
            {
                Debug.LogError($"[WeaponSetupTool] Could not find {SwordPrefabPath}. Run 'Rebuild Player Prefab' first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError($"[WeaponSetupTool] No GameObject tagged 'Player' found in {TargetScenePath}");
                return;
            }

            Animator animator = player.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("[WeaponSetupTool] No Humanoid Animator found under Player - can't locate the hand bone.");
                return;
            }

            Transform handBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (handBone == null)
            {
                Debug.LogError("[WeaponSetupTool] Animator's Avatar has no RightHand bone mapped.");
                return;
            }

            Transform socket = handBone.Find("RightHandSocket");
            if (socket == null)
            {
                var socketObj = new GameObject("RightHandSocket");
                socketObj.transform.SetParent(handBone, false);
                socket = socketObj.transform;
            }

            var weaponSystem = player.GetComponent<WeaponSystem>();
            if (weaponSystem == null)
                weaponSystem = player.AddComponent<WeaponSystem>();

            weaponSystem.weaponPrefab = swordPrefab;
            weaponSystem.rightHandSocket = socket;

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[WeaponSetupTool] WeaponSystem wired up on Player in {TargetScenePath}, socket parented " +
                      $"to the real '{handBone.name}' bone. Press Play and attack (mouse buttons) to see the sword.");
        }

        /// <summary>
        /// Drops a DarkMoonGreatsword pickup a couple meters in front of the scene's
        /// Player, with a trigger collider so WeaponPickup can equip it on contact.
        /// Safe to run repeatedly - it replaces any pickup it previously placed rather
        /// than stacking up duplicates.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Place Weapon Pickup In Scene")]
        public static void PlaceWeaponPickupInScene()
        {
            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            if (swordPrefab == null)
            {
                Debug.LogError($"[WeaponSetupTool] Could not find {SwordPrefabPath}. Run 'Rebuild Player Prefab' first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError($"[WeaponSetupTool] No GameObject tagged 'Player' found in {TargetScenePath}");
                return;
            }

            var existing = GameObject.Find("DarkMoonGreatsword_Pickup");
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject pickupInstance = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
            pickupInstance.name = "DarkMoonGreatsword_Pickup";

            // player.transform.position.y isn't reliably ground level (e.g. if the scene was
            // last saved mid-jump), so find the actual floor under the drop spot with a
            // raycast instead of trusting it.
            Vector3 desiredSpot = player.transform.position + player.transform.forward * 2f;
            Vector3 rayStart = new Vector3(desiredSpot.x, desiredSpot.y + 10f, desiredSpot.z);
            Vector3 pickupPosition;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f))
            {
                pickupPosition = hit.point + Vector3.up * 0.15f;
                Debug.Log($"[WeaponSetupTool] Floor raycast hit '{hit.collider.name}' at {hit.point:F3}, resting pickup there.");
            }
            else
            {
                pickupPosition = desiredSpot;
                Debug.LogWarning("[WeaponSetupTool] Floor raycast found nothing below the drop spot - " +
                                  "falling back to player height, pickup may float. Check there's a ground collider.");
            }
            pickupInstance.transform.position = pickupPosition;

            var box = pickupInstance.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1f, 1f, 1f);

            var pickup = pickupInstance.AddComponent<WeaponPickup>();
            pickup.weaponPrefab = swordPrefab;

            EditorUtility.SetDirty(pickupInstance);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[WeaponSetupTool] Placed a pickup 2m in front of Player in {TargetScenePath}. " +
                      "Walk into it in Play Mode to trigger WeaponSystem.Equip().");
        }

        /// <summary>
        /// Only Index and Middle proximal are mapped in the Humanoid Avatar right now (see
        /// DumpGripAlignmentData earlier - Little/Thumb came back "NOT MAPPED"). Before anyone
        /// spends hours rigging in Blender, check whether the raw FBX skeleton already has
        /// finger bones under the hand that just never got wired into the Avatar's Humanoid
        /// mapping - dumps the FULL raw child hierarchy, not just what Unity's Humanoid bones
        /// resolve to, so it shows bones Unity isn't using at all.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Dump Raw Hand Bone Hierarchy")]
        public static void DumpRawHandBoneHierarchy()
        {
            EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null)
            {
                Debug.LogError("[WeaponSetupTool] No RightHand bone mapped - can't inspect its children.");
                return;
            }

            Debug.Log($"[WeaponSetupTool] Raw child hierarchy under hand bone '{hand.name}' " +
                      $"({hand.childCount} direct children, excluding RightHandSocket which we created):");
            LogBoneTreeRecursive(hand, 0);
            Debug.Log("[WeaponSetupTool] DONE - if you see 4-5 separate branches here (thumb/index/middle/ring/little) " +
                      "the rig already has fingers, just unmapped in the Avatar. If it's flat/empty, real rigging is needed.");
        }

        private static void LogBoneTreeRecursive(Transform t, int depth)
        {
            foreach (Transform child in t)
            {
                if (child.name == "RightHandSocket") continue; // ours, not part of the original rig
                Debug.Log($"[WeaponSetupTool] {new string(' ', depth * 2)}- {child.name}");
                LogBoneTreeRecursive(child, depth + 1);
            }
        }

        /// <summary>
        /// Removes any orphaned sword objects left in the scene root - e.g. a probe from
        /// ComputeAndApplyGripAlignment that never got destroyed because an exception fired
        /// between instantiating it and the cleanup call (fixed now, but this mops up any
        /// leftover from before the fix). Leaves DarkMoonGreatsword_Pickup alone - that's the
        /// real floor pickup, not a stray probe.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Cleanup Stray Weapon Probes")]
        public static void CleanupStrayWeaponProbes()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("DarkMoonGreatsword") && root.name != "DarkMoonGreatsword_Pickup")
                {
                    Debug.Log($"[WeaponSetupTool] Removing stray '{root.name}' at {root.transform.position:F3}");
                    Object.DestroyImmediate(root);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[WeaponSetupTool] Removed {removed} stray weapon object(s) from {TargetScenePath}.");
        }

        /// <summary>
        /// The grip anchor point was previously computed by parsing the raw .obj file's
        /// vertex data directly in Python - but that's the MESH's own local space, which
        /// isn't necessarily the same as the imported PREFAB ROOT's local space (the model
        /// importer commonly nests the actual mesh under a child GameObject with its own
        /// offset). Using the raw-.obj-space point directly on weaponInstance.TransformPoint
        /// (which transforms relative to the ROOT) sent the sword flying off to nowhere -
        /// exactly the "completely detached, floating" symptom. This recomputes the same
        /// "centroid of the vertex cluster farthest from the blade tip" anchor, but by reading
        /// vertices through Unity's own transform hierarchy (root at identity/origin, so
        /// world position IS root-local position), so it's correct regardless of nesting.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Compute And Apply Grip Anchor")]
        public static void ComputeAndApplyGripAnchor()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            var weaponSystem = player.GetComponent<WeaponSystem>();
            if (weaponSystem == null)
            {
                Debug.LogError("[WeaponSetupTool] Player has no WeaponSystem - run Attach Weapon To Scene Character first.");
                return;
            }

            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one;

            MeshFilter[] filters = probe.GetComponentsInChildren<MeshFilter>(true);
            Debug.Log($"[WeaponSetupTool] Prefab hierarchy - root '{probe.name}' has {filters.Length} MeshFilter(s):");
            foreach (var f in filters)
            {
                Debug.Log($"[WeaponSetupTool]   '{f.transform.name}' localPos={f.transform.localPosition:F4} " +
                          $"localRot={f.transform.localRotation.eulerAngles:F4} localScale={f.transform.localScale:F4} " +
                          $"vertexCount={f.sharedMesh.vertexCount}");
            }

            var worldVerts = new System.Collections.Generic.List<Vector3>();
            foreach (var f in filters)
            {
                foreach (Vector3 v in f.sharedMesh.vertices)
                    worldVerts.Add(f.transform.TransformPoint(v)); // probe root is at identity/origin, so world == root-local
            }

            if (worldVerts.Count == 0)
            {
                Debug.LogError("[WeaponSetupTool] No vertices found on the sword prefab.");
                Object.DestroyImmediate(probe);
                return;
            }

            // The pommel-end centroid (previous approach) anchors right at the very butt of
            // the handle, so the crossguard ends up flush against the fist with nothing
            // poking out either side. Better: find the actual handle segment - between the
            // crossguard (the blade's single widest cross-section) and the pommel - and
            // anchor at ITS midpoint, so a bit of pommel shows past the pinky and a bit of
            // neck shows past the thumb, like an actual gripped handle.
            worldVerts.Sort((a, b) => a.z.CompareTo(b.z));

            // Find the largest Z gap - this mesh has an isolated, sparse blade-tip cluster
            // sitting far from the dense hilt/blade-base body (confirmed via raw vertex
            // analysis earlier), so the biggest gap in sorted Z values marks that split.
            int splitIndex = 0;
            float largestGap = -1f;
            for (int i = 0; i < worldVerts.Count - 1; i++)
            {
                float gap = worldVerts[i + 1].z - worldVerts[i].z;
                if (gap > largestGap) { largestGap = gap; splitIndex = i; }
            }

            var lowSide = worldVerts.GetRange(0, splitIndex + 1);
            var highSide = worldVerts.GetRange(splitIndex + 1, worldVerts.Count - splitIndex - 1);
            // The tip is the sparse side (few verts, low-poly point); the body (hilt+blade
            // base) is the dense side - whichever has more vertices is the body.
            var mainBody = lowSide.Count >= highSide.Count ? lowSide : highSide;
            bool bodyIsLowSide = lowSide.Count >= highSide.Count;

            // Within the body, the end closest to the gap faces the tip; the far end is the
            // pommel/butt - always the correct end to grip, regardless of blade/guard/grip
            // styling in a low-poly mesh with no separate materials to key off.
            float pommelZ = bodyIsLowSide ? mainBody[0].z : mainBody[mainBody.Count - 1].z;
            float nearTipZ = bodyIsLowSide ? mainBody[mainBody.Count - 1].z : mainBody[0].z;

            // Bin the body by Z and find the single widest cross-section (XY spread) - a
            // crossguard is reliably the widest point on any sword.
            const int nBins = 24;
            float zLo = Mathf.Min(pommelZ, nearTipZ);
            float zHi = Mathf.Max(pommelZ, nearTipZ);
            float binSize = (zHi - zLo) / nBins;
            float bestWidth = -1f;
            float crossguardZ = pommelZ;
            for (int b = 0; b < nBins; b++)
            {
                float binStart = zLo + b * binSize;
                float binEnd = binStart + binSize + 0.0001f;
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                bool any = false;
                foreach (var v in mainBody)
                {
                    if (v.z < binStart || v.z >= binEnd) continue;
                    any = true;
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
                if (!any) continue;
                float width = new Vector2(maxX - minX, maxY - minY).magnitude;
                if (width > bestWidth) { bestWidth = width; crossguardZ = binStart + binSize * 0.5f; }
            }

            // Handle = body vertices between the crossguard and the pommel; anchor at ITS
            // centroid (the midpoint of the actual grippable segment, not either extreme).
            float handleZLo = Mathf.Min(crossguardZ, pommelZ);
            float handleZHi = Mathf.Max(crossguardZ, pommelZ);
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var v in mainBody)
            {
                if (v.z < handleZLo || v.z > handleZHi) continue;
                sum += v;
                count++;
            }
            if (count == 0) { sum = mainBody[bodyIsLowSide ? 0 : mainBody.Count - 1]; count = 1; }
            Vector3 gripAnchor = sum / count;

            weaponSystem.gripAnchorLocal = gripAnchor;
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[WeaponSetupTool] pommelZ={pommelZ:F4} crossguardZ={crossguardZ:F4} (widest cross-section={bestWidth:F4}) " +
                      $"handle midpoint from {count} verts: {gripAnchor:F4} (saved to WeaponSystem.gripAnchorLocal in {TargetScenePath}).");

            Object.DestroyImmediate(probe);
        }

        /// <summary>
        /// Creates a back socket for sheathing and wires it into WeaponSystem. Unlike the
        /// hand socket, this doesn't need a computed grip axis - it just needs a predictable
        /// world orientation, which the chest/spine bone's own local axes are NOT guaranteed
        /// to have (arbitrary per the FBX rig). So instead of inheriting the bone's rotation,
        /// this forces the socket's world rotation to match the character root's own forward/up
        /// at the time of creation, then aligns the blade's local Z (its long axis) to that
        /// socket's local up - i.e. "point along the spine" - a fixed, non-guessed 90 degree tilt.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Add Sheathe (Back Socket + Toggle Key)")]
        public static void AddSheatheSupport()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Chest)
                ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine == null)
            {
                Debug.LogError("[WeaponSetupTool] No Chest or Spine bone mapped - can't place a back socket.");
                return;
            }

            var weaponSystem = player.GetComponent<WeaponSystem>();
            if (weaponSystem == null)
            {
                Debug.LogError("[WeaponSetupTool] Player has no WeaponSystem - run Attach Weapon To Scene Character first.");
                return;
            }

            Transform backSocket = spine.Find("BackSocket");
            if (backSocket == null)
            {
                var socketObj = new GameObject("BackSocket");
                socketObj.transform.SetParent(spine, false);
                backSocket = socketObj.transform;
            }

            backSocket.position = spine.position + player.transform.up * 0.1f - player.transform.forward * 0.15f;
            backSocket.rotation = Quaternion.LookRotation(player.transform.forward, player.transform.up);

            weaponSystem.backSocket = backSocket;
            weaponSystem.sheathedPositionOffset = Vector3.zero;
            weaponSystem.sheathedRotationOffset = Quaternion.FromToRotation(Vector3.forward, Vector3.up).eulerAngles;

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[WeaponSetupTool] BackSocket created under '{spine.name}', wired to WeaponSystem. " +
                      "Press R in Play Mode to toggle drawn/sheathed once a weapon is equipped.");
        }

        /// <summary>
        /// Logs real, measured rig/mesh data instead of guessing at a grip rotation -
        /// hand bone axes, finger bone positions (if mapped), and the sword mesh's actual
        /// imported bounds. Run this, then read the log and compute the alignment from
        /// real numbers via ComputeAndApplyGripAlignment.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Dump Grip Alignment Data")]
        public static void DumpGripAlignmentData()
        {
            EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            Debug.Log($"[GripDump] Player.forward(world)={player.transform.forward:F4} Player.up(world)={player.transform.up:F4}");
            Debug.Log($"[GripDump] Hand.position(world)={hand.position:F4}");
            Debug.Log($"[GripDump] Hand.right(world)={hand.right:F4} Hand.up(world)={hand.up:F4} Hand.forward(world)={hand.forward:F4}");

            LogFingerBone(animator, HumanBodyBones.RightIndexProximal, "IndexProximal", hand);
            LogFingerBone(animator, HumanBodyBones.RightLittleProximal, "LittleProximal", hand);
            LogFingerBone(animator, HumanBodyBones.RightMiddleProximal, "MiddleProximal", hand);
            LogFingerBone(animator, HumanBodyBones.RightThumbProximal, "ThumbProximal", hand);

            Transform socket = hand.Find("RightHandSocket");
            if (socket != null)
                Debug.Log($"[GripDump] Socket.localPosition={socket.localPosition:F4} Socket.localRotation.euler={socket.localRotation.eulerAngles:F4}");
            else
                Debug.Log("[GripDump] No RightHandSocket found under hand bone.");

            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
            temp.transform.position = Vector3.zero;
            temp.transform.rotation = Quaternion.identity;
            temp.transform.localScale = Vector3.one;

            Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);
                Debug.Log($"[GripDump] SwordMesh(identity rot, at origin) bounds.center={b.center:F4} bounds.size={b.size:F4} bounds.min={b.min:F4} bounds.max={b.max:F4}");
                Debug.Log($"[GripDump] SwordMesh pivot(0,0,0) relative to bounds.center = {(Vector3.zero - b.center):F4}");
            }
            else
            {
                Debug.Log("[GripDump] Sword prefab has no renderers!");
            }
            Object.DestroyImmediate(temp);

            Debug.Log("[GripDump] DONE");
        }

        /// <summary>
        /// Computes a real grip rotation from measured bone data (see DumpGripAlignmentData)
        /// instead of a hand-tuned guess, and writes it to WeaponSystem.weaponRotationOffset.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Compute And Apply Grip Alignment")]
        public static void ComputeAndApplyGripAlignment()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);

            if (index == null || middle == null)
            {
                Debug.LogError("[WeaponSetupTool] Need Index + Middle proximal finger bones mapped to compute a real grip axis.");
                return;
            }

            var weaponSystem = player.GetComponent<WeaponSystem>();
            if (weaponSystem == null)
            {
                Debug.LogError("[WeaponSetupTool] Player has no WeaponSystem - run Attach Weapon To Scene Character first.");
                return;
            }

            // A gripped handle runs across the knuckle line - the axis fingers curl around -
            // not along the direction the fingers point. Index-to-middle proximal is a real
            // (if short) segment of that line, taken from actual bind-pose bone positions.
            // The SIGN of this line is ambiguous from two points alone - a fixed "assume up"
            // heuristic guessed wrong last time (blade ended up pointing back across the
            // forearm instead of away from the body), so both signs are tried below and
            // scored by which one actually points the blade away from the torso.
            Vector3 knuckleLine = (index.position - middle.position).normalized;

            Transform torso = animator.GetBoneTransform(HumanBodyBones.Chest)
                ?? animator.GetBoneTransform(HumanBodyBones.Spine)
                ?? animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 torsoPos = torso.position;

            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            Transform socket = hand.Find("RightHandSocket");
            if (socket == null)
            {
                Debug.LogError("[WeaponSetupTool] No RightHandSocket under the hand bone - run Attach Weapon To Scene Character first. " +
                                "(Also make sure you're NOT in Play Mode - these tools edit the saved scene, not the live play session.)");
                return;
            }

            // Measure the mesh's own long-axis (local Z) extreme points once, in its own
            // unrotated local space, by instantiating it unparented at the origin - same
            // technique as DumpGripAlignmentData.
            GameObject originProbe = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
            Vector3 localEndA, localEndB;
            try
            {
                originProbe.transform.position = Vector3.zero;
                originProbe.transform.rotation = Quaternion.identity;
                originProbe.transform.localScale = Vector3.one;
                Renderer[] originRenderers = originProbe.GetComponentsInChildren<Renderer>(true);
                Bounds meshBounds = originRenderers[0].bounds;
                for (int i = 1; i < originRenderers.Length; i++) meshBounds.Encapsulate(originRenderers[i].bounds);
                localEndA = new Vector3(meshBounds.center.x, meshBounds.center.y, meshBounds.min.z);
                localEndB = new Vector3(meshBounds.center.x, meshBounds.center.y, meshBounds.max.z);
            }
            finally
            {
                Object.DestroyImmediate(originProbe);
            }

            Quaternion bestRotation = Quaternion.identity;
            float bestFarDistance = float.NegativeInfinity;
            string bestLabel = "";

            foreach (float sign in new[] { 1f, -1f })
            {
                Vector3 candidateKnuckleLine = knuckleLine * sign;
                Vector3 gripDirLocal = new Vector3(
                    Vector3.Dot(candidateKnuckleLine, hand.right),
                    Vector3.Dot(candidateKnuckleLine, hand.up),
                    Vector3.Dot(candidateKnuckleLine, hand.forward));
                Quaternion candidateRotation = Quaternion.FromToRotation(Vector3.forward, gripDirLocal);

                GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
                try
                {
                    probe.transform.SetParent(socket, false);
                    probe.transform.localPosition = Vector3.zero;
                    probe.transform.localRotation = candidateRotation;
                    Vector3 parentLossy = socket.lossyScale;
                    probe.transform.localScale = new Vector3(1f / parentLossy.x, 1f / parentLossy.y, 1f / parentLossy.z);

                    Renderer[] probeRenderers = probe.GetComponentsInChildren<Renderer>(true);
                    Bounds pb = probeRenderers[0].bounds;
                    for (int i = 1; i < probeRenderers.Length; i++) pb.Encapsulate(probeRenderers[i].bounds);
                    probe.transform.position += socket.position - pb.center;

                    Vector3 worldA = probe.transform.TransformPoint(localEndA);
                    Vector3 worldB = probe.transform.TransformPoint(localEndB);
                    float distA = Vector3.Distance(worldA, torsoPos);
                    float distB = Vector3.Distance(worldB, torsoPos);
                    float farDistance = Mathf.Max(distA, distB);

                    Debug.Log($"[WeaponSetupTool] Candidate sign={sign:F0}: endA dist-from-torso={distA:F3} endB dist-from-torso={distB:F3}");

                    // The correctly-held blade extends its tip well away from the torso while
                    // the hilt stays near the hand (already guaranteed by the recenter step) -
                    // so the candidate whose farthest extreme reaches furthest from the torso
                    // is the one where the blade points outward instead of back across the body.
                    if (farDistance > bestFarDistance)
                    {
                        bestFarDistance = farDistance;
                        bestRotation = candidateRotation;
                        bestLabel = $"sign={sign:F0}";
                    }
                }
                finally
                {
                    // Guaranteed cleanup even if something above throws - a previous version
                    // without this left an orphaned sword sitting at the world origin when
                    // socket.lossyScale threw on a null socket (see the null check added above).
                    Object.DestroyImmediate(probe);
                }
            }

            // gripAnchorLocal lands the grip point at rightHandSocket, which sits at the HAND
            // BONE - roughly the wrist/back-of-hand, not the middle of a closed fist. A real
            // handle needs to pass THROUGH the curled fingers, so nudge the target point to
            // the knuckle-line midpoint (average of index + middle proximal, the same real
            // bone data used for the rotation above) instead of the bare hand bone position.
            Vector3 knuckleCenter = (index.position + middle.position) * 0.5f;
            Vector3 fistOffsetWorld = knuckleCenter - hand.position;
            Vector3 fistOffsetLocal = new Vector3(
                Vector3.Dot(fistOffsetWorld, hand.right),
                Vector3.Dot(fistOffsetWorld, hand.up),
                Vector3.Dot(fistOffsetWorld, hand.forward));

            weaponSystem.weaponRotationOffset = bestRotation.eulerAngles;
            weaponSystem.weaponPositionOffset = fistOffsetLocal;

            Debug.Log($"[WeaponSetupTool] knuckleCenter(world)={knuckleCenter:F4} fistOffsetWorld={fistOffsetWorld:F4} " +
                      $"-> weaponPositionOffset={fistOffsetLocal:F4} (shifts grip from the hand bone into the fist).");

            // The scene's Player previously had weaponPrefab assigned directly on WeaponSystem,
            // which auto-equips in Start() - but the intended flow is starting unarmed and
            // equipping via the floor pickup (WeaponPickup carries its own prefab reference
            // and calls Equip() explicitly, so this doesn't affect pickup behavior at all).
            weaponSystem.weaponPrefab = null;

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[WeaponSetupTool] Picked {bestLabel} (farthest extreme {bestFarDistance:F3}m from torso) " +
                      $"-> weaponRotationOffset={weaponSystem.weaponRotationOffset:F2}, weaponPrefab cleared so " +
                      $"the player starts unarmed and only equips via the floor pickup. Saved to {TargetScenePath}.");
        }

        private static void LogFingerBone(Animator animator, HumanBodyBones bone, string label, Transform hand)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null)
            {
                Debug.Log($"[GripDump] {label} = NOT MAPPED");
                return;
            }
            Vector3 fromHand = t.position - hand.position;
            Debug.Log($"[GripDump] {label}.position(world)={t.position:F4} relativeToHand={fromHand:F4}");
        }

        private const string TargetScenePathForSpawn = "Assets/no wolf.unity";

        /// <summary>
        /// Sets weaponPrefab back on the Player's WeaponSystem so it auto-equips at Start() -
        /// combined with the Start() change to call SetDrawn(false) right after, the player now
        /// spawns already carrying the sword, sheathed on the back, instead of unarmed. Requires
        /// the back socket from Add Sheathe (Back Socket + Toggle Key) to already exist.
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Spawn With Sword Sheathed")]
        public static void SpawnWithSwordSheathed()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePathForSpawn, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            var weaponSystem = player != null ? player.GetComponent<WeaponSystem>() : null;
            if (weaponSystem == null)
            {
                Debug.LogError("[WeaponSetupTool] No WeaponSystem found on Player.");
                return;
            }

            if (weaponSystem.backSocket == null)
            {
                Debug.LogError("[WeaponSetupTool] WeaponSystem.backSocket is not set - run " +
                                "'Add Sheathe (Back Socket + Toggle Key)' first, otherwise the " +
                                "player would spawn with the sword stuck drawn (no back socket to sheathe to).");
                return;
            }

            GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            weaponSystem.weaponPrefab = swordPrefab;
            EditorUtility.SetDirty(player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[WeaponSetupTool] weaponPrefab set on Player's WeaponSystem - will auto-equip " +
                      $"sheathed at Start() (WeaponSystem.Start() now calls SetDrawn(false) right after Equip()). " +
                      $"Saved to {TargetScenePathForSpawn}.");
        }

        private static void EnsureTag(string tag)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }
    }
}
#endif
