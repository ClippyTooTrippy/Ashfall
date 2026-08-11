#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using SoulsLike.Enemy;

namespace SoulsLike.EditorTools
{
    /// <summary>
    /// Lays out three connected zones in "no wolf.unity": a spawn arena (existing Ground) at
    /// Z=0, a wolf arena further out, and a boss room (the imported Boss.fbx, a ~35x35x6m
    /// static room mesh - confirmed via BossSetupTool.InspectBossFbx to have no skeleton/
    /// Animator, i.e. it's environment geometry, not a character) at the far end, with the
    /// HumanEnemy repositioned inside it as the boss. Simple box corridors connect the zones.
    /// This keeps everything in the single existing scene (spatially separated "arenas") rather
    /// than building actual multi-scene loading/transition infrastructure, which is a much
    /// larger undertaking than distinct combat spaces along one continuous ground.
    /// </summary>
    public static class ArenaLayoutTool
    {
        private const string TargetScenePath = "Assets/no wolf.unity";
        private const string BossModelPath = "Assets/Models/Boss/Boss.fbx";

        private const float WolfArenaZ = 45f;
        private const float BossArenaZ = 110f;
        private const float CorridorWidth = 6f;

        [MenuItem("Tools/Souls-Like Horror/Build Three-Arena Layout")]
        public static void BuildThreeArenaLayout()
        {
            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.FindWithTag("Player");
            GameObject wolf = GameObject.Find("Wolf");
            GameObject humanEnemy = GameObject.Find("HumanEnemy");
            GameObject spawnGround = GameObject.Find("Ground");

            if (player == null || wolf == null || humanEnemy == null || spawnGround == null)
            {
                Debug.LogError($"[ArenaLayoutTool] Missing something - Player={player != null} " +
                                $"Wolf={wolf != null} HumanEnemy={humanEnemy != null} Ground={spawnGround != null}");
                return;
            }

            // ---------- Spawn arena: keep existing Ground at origin, just confirm player is there ----------
            player.transform.position = new Vector3(0f, player.transform.position.y, 0f);

            // ---------- Wolf arena: a new ground plane further down +Z ----------
            GameObject wolfArenaGround = GameObject.Find("WolfArenaGround");
            if (wolfArenaGround == null)
            {
                wolfArenaGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
                wolfArenaGround.name = "WolfArenaGround";
            }
            wolfArenaGround.transform.position = new Vector3(0f, 0f, WolfArenaZ);
            wolfArenaGround.transform.localScale = new Vector3(2.5f, 1f, 2.5f); // Unity plane primitive is 10x10 at scale 1 -> 25x25

            // ---------- Corridor 1: spawn -> wolf arena ----------
            GameObject corridor1 = GameObject.Find("Corridor_SpawnToWolf");
            if (corridor1 == null)
            {
                corridor1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                corridor1.name = "Corridor_SpawnToWolf";
            }
            float corridor1Length = WolfArenaZ - 5f - 5f; // leave gaps to clear each arena's radius roughly
            corridor1.transform.position = new Vector3(0f, -0.05f, (5f + (WolfArenaZ - 5f)) / 2f);
            corridor1.transform.localScale = new Vector3(CorridorWidth, 0.1f, corridor1Length);

            // ---------- Boss room: instantiate Boss.fbx, floor-aligned, at the far end ----------
            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossModelPath);
            if (bossPrefab == null)
            {
                Debug.LogError($"[ArenaLayoutTool] Could not load {BossModelPath}");
                return;
            }

            GameObject existingBossRoom = GameObject.Find("BossRoom");
            if (existingBossRoom != null) Object.DestroyImmediate(existingBossRoom);

            GameObject bossRoom = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
            bossRoom.name = "BossRoom";
            bossRoom.transform.position = Vector3.zero;
            // Deliberately NOT touching rotation - forcing it to identity here previously rotated
            // the room onto its side (measured height jumped from a sane 6.096m to 34.7m, meaning
            // a ~35m-long axis got rotated onto vertical). Leave it at the prefab's own authored
            // default rotation, which is what gave the correct 6.096m reading in InspectBossFbx.

            // Measure its floor offset (min.y) so the room's actual floor sits at world y=0
            // rather than guessing - same "measure, don't assume" approach used throughout.
            Renderer[] bossRenderers = bossRoom.GetComponentsInChildren<Renderer>(true);
            float floorOffset = 0f;
            if (bossRenderers.Length > 0)
            {
                Bounds b = bossRenderers[0].bounds;
                for (int i = 1; i < bossRenderers.Length; i++) b.Encapsulate(bossRenderers[i].bounds);
                floorOffset = -b.min.y;
                Debug.Log($"[ArenaLayoutTool] Boss room bounds min.y={b.min.y:F3} max.y={b.max.y:F3} size={b.size:F3} - floor offset={floorOffset:F3}");
                if (b.size.y > 15f)
                {
                    Debug.LogError($"[ArenaLayoutTool] Room height ({b.size.y:F1}m) still looks wrong (expected ~6m) - " +
                                    "stopping before placing it sideways. Needs manual investigation of the prefab's rotation/pivot.");
                    Object.DestroyImmediate(bossRoom);
                    return;
                }
            }
            bossRoom.transform.position = new Vector3(0f, floorOffset, BossArenaZ);

            // A collider for the room so the player/agents don't fall through and so it
            // registers on the NavMesh bake - add MeshColliders to any renderer that lacks one.
            var meshFilters = bossRoom.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }

            // The room's footprint isn't necessarily centered on its own transform origin (same
            // off-center-pivot class of issue seen with the sword and wolf earlier) - measure its
            // ACTUAL world-space X/Z center now that it's placed, instead of assuming BossArenaZ
            // is where the usable floor center actually is.
            Vector3 bossRoomFloorCenter = new Vector3(0f, floorOffset, BossArenaZ);
            Renderer[] placedRenderers = bossRoom.GetComponentsInChildren<Renderer>(true);
            if (placedRenderers.Length > 0)
            {
                Bounds wb = placedRenderers[0].bounds;
                for (int i = 1; i < placedRenderers.Length; i++) wb.Encapsulate(placedRenderers[i].bounds);
                bossRoomFloorCenter = new Vector3(wb.center.x, wb.min.y, wb.center.z);
                Debug.Log($"[ArenaLayoutTool] Boss room actual world footprint center={bossRoomFloorCenter:F2} " +
                          $"(vs assumed (0, *, {BossArenaZ:F0}))");
            }

            // ---------- Corridor 2: wolf arena -> boss room ----------
            GameObject corridor2 = GameObject.Find("Corridor_WolfToBoss");
            if (corridor2 == null)
            {
                corridor2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                corridor2.name = "Corridor_WolfToBoss";
            }
            float corridor2Start = WolfArenaZ + 12f;
            float corridor2End = BossArenaZ - 15f; // stop short of the room's own footprint edge
            corridor2.transform.position = new Vector3(0f, -0.05f, (corridor2Start + corridor2End) / 2f);
            corridor2.transform.localScale = new Vector3(CorridorWidth, 0.1f, Mathf.Max(1f, corridor2End - corridor2Start));

            // ---------- Reposition enemies into their arenas (provisional - refined after bake) ----------
            wolf.transform.position = new Vector3(0f, wolf.transform.position.y, WolfArenaZ);
            humanEnemy.transform.position = new Vector3(bossRoomFloorCenter.x,
                humanEnemy.transform.position.y, bossRoomFloorCenter.z);

            var wolfAI = wolf.GetComponent<SoulsLike.Enemy.WolfAI>();
            var enemyAI = humanEnemy.GetComponent<EnemyAI>();
            if (wolfAI != null) wolfAI.player = player.transform;
            if (enemyAI != null) enemyAI.player = player.transform;

            // ---------- Rebake, scanning the whole scene instead of a hand-picked volume ----------
            // A manually computed Size/Center volume is exactly the kind of thing that's easy to
            // get subtly wrong (as just happened - agents failed to place, meaning some ground
            // wasn't actually covered). Switching to "All" collect mode scans every valid
            // NavMesh-relevant collider in the scene instead, which is far more robust than
            // hand-computing bounds for a growing multi-arena layout.
            var surface = spawnGround.GetComponent<NavMeshSurface>();
            if (surface != null)
            {
                surface.collectObjects = CollectObjects.All;
                surface.BuildNavMesh();
                Debug.Log("[ArenaLayoutTool] NavMesh rebaked with collectObjects=All (whole scene scanned).");
            }
            else
            {
                Debug.LogWarning("[ArenaLayoutTool] No NavMeshSurface on Ground - couldn't rebake.");
            }

            // The room's bounding-box center measured almost exactly where I assumed (confirmed:
            // NOT an off-center-pivot problem this time) yet still isn't covered - it's likely a
            // complex multi-part structure where the box center lands inside a wall or void, not
            // open floor. Rather than keep guessing where the floor actually is, search the REAL
            // baked mesh for the nearest walkable point with a generous radius and place the
            // enemy exactly there - guaranteed valid because it's read from the bake itself.
            if (NavMesh.SamplePosition(humanEnemy.transform.position, out NavMeshHit roomFloorHit, 20f, NavMesh.AllAreas))
            {
                Vector3 before = humanEnemy.transform.position;
                humanEnemy.transform.position = roomFloorHit.position;
                Debug.Log($"[ArenaLayoutTool] HumanEnemy moved from assumed center {before:F2} to actual " +
                          $"walkable point {roomFloorHit.position:F2} found on the baked mesh.");
            }
            else
            {
                Debug.LogError("[ArenaLayoutTool] No walkable NavMesh found within 20m of the boss room's " +
                                "assumed center at all - the room's floor may not be baking as walkable " +
                                "(check mesh normals / collider). Needs manual investigation in the Editor.");
            }

            // Verify final coverage directly at both enemies' actual positions instead of
            // assuming - this is exactly the check that would have caught the failure before it
            // ever reached Play Mode.
            bool wolfOnMesh = NavMesh.SamplePosition(wolf.transform.position, out NavMeshHit wolfHit, 2f, NavMesh.AllAreas);
            bool enemyOnMesh = NavMesh.SamplePosition(humanEnemy.transform.position, out NavMeshHit enemyHit, 0.5f, NavMesh.AllAreas);
            Debug.Log($"[ArenaLayoutTool] Final NavMesh coverage check - Wolf at {wolf.transform.position:F2}: " +
                      $"{(wolfOnMesh ? $"OK, nearest point {wolfHit.position:F2}" : "NOT COVERED")}. " +
                      $"HumanEnemy at {humanEnemy.transform.position:F2}: " +
                      $"{(enemyOnMesh ? $"OK, nearest point {enemyHit.position:F2}" : "NOT COVERED")}.");

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[ArenaLayoutTool] Layout built - spawn at Z=0, Wolf arena at Z={WolfArenaZ}, " +
                      $"Boss room at Z={BossArenaZ}. Saved to {TargetScenePath}.");
        }
    }
}
#endif
