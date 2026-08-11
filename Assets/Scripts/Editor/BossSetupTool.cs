#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SoulsLike.EditorTools
{
    /// <summary>
    /// Tools for inspecting and wiring up the boss arena content.
    /// </summary>
    public static class BossSetupTool
    {
        private const string BossModelPath = "Assets/Models/Boss/Boss.fbx";

        /// <summary>
        /// The user dropped in a new FBX with no context on what it actually is - a character
        /// (has a skeleton, meant to be rigged/animated) or a static environment piece (arena
        /// geometry, meant to be placed as scenery). Checking the actual import data instead of
        /// assuming from the filename ("1st it.fbx" gives no hint either way).
        /// </summary>
        [MenuItem("Tools/Souls-Like Horror/Inspect Boss FBX")]
        public static void InspectBossFbx()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossModelPath);
            if (prefab == null)
            {
                Debug.LogError($"[BossSetupTool] Could not load {BossModelPath} - check it imported without errors.");
                return;
            }

            var importer = AssetImporter.GetAtPath(BossModelPath) as ModelImporter;
            if (importer != null)
            {
                Debug.Log($"[BossSetupTool] animationType={importer.animationType} " +
                          $"(0=None, 1=Legacy, 2=Generic, 3=Humanoid) isHuman-capable-check-below");
            }

            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var skinned = probe.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var staticMesh = probe.GetComponentsInChildren<MeshRenderer>(true);
                var animator = probe.GetComponentInChildren<Animator>();
                var allTransforms = probe.GetComponentsInChildren<Transform>(true);

                Debug.Log($"[BossSetupTool] SkinnedMeshRenderers={skinned.Length} (character-like if >0), " +
                          $"static MeshRenderers={staticMesh.Length} (environment-like if >0 and no skinned), " +
                          $"total transforms/bones={allTransforms.Length}, has Animator={animator != null}, " +
                          $"isHuman={(animator != null && animator.isHuman)}");

                Renderer[] allRenderers = probe.GetComponentsInChildren<Renderer>(true);
                if (allRenderers.Length > 0)
                {
                    Bounds b = allRenderers[0].bounds;
                    for (int i = 1; i < allRenderers.Length; i++) b.Encapsulate(allRenderers[i].bounds);
                    Debug.Log($"[BossSetupTool] Combined bounds.size={b.size:F3} (helps tell character-scale from room-scale)");
                }

                Debug.Log("[BossSetupTool] Top-level child names:");
                foreach (Transform child in probe.transform)
                    Debug.Log($"[BossSetupTool]   - {child.name}");
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }

            Debug.Log("[BossSetupTool] INSPECT DONE.");
        }
    }
}
#endif
