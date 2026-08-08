#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using SoulsLike.Player;
using SoulsLike.Enemy;
using SoulsLike.CameraSystem;
using SoulsLike.Systems;
using SoulsLike.UI;

namespace SoulsLike.EditorTools
{
    /// <summary>
    /// Builds a minimal playable scene from code: ground, player, camera, one enemy,
    /// dim horror lighting, and fog. Saves you from wiring every component by hand.
    ///
    /// IMPORTANT: after running this, you still need to:
    ///   1. Window > AI > Navigation (or install the "AI Navigation" package) and Bake a NavMesh
    ///      for the ground plane, so the enemy can path to the player.
    ///   2. Make sure Tags "Player" and "Enemy" exist (Edit > Project Settings > Tags and Layers).
    ///      This script creates them automatically if missing.
    ///   3. Assign Shaders/PS1Dither.shader to the camera's PS1RenderEffect if Shader.Find
    ///      doesn't pick it up in your project settings (Graphics > Always Included Shaders).
    /// </summary>
    public static class TestSceneBuilder
    {
        [MenuItem("Tools/Souls-Like Horror/Build Test Scene")]
        public static void BuildScene()
        {
            EnsureTag("Player");
            EnsureTag("Enemy");

            // ---------- Ground ----------
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            ApplyRetroMaterial(ground, new Color(0.25f, 0.24f, 0.22f));

            // ---------- Player ----------
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            ApplyRetroMaterial(player, new Color(0.55f, 0.15f, 0.15f));

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;

            Health playerHealth = player.AddComponent<Health>();
            Stamina playerStamina = player.AddComponent<Stamina>();
            LockOnSystem lockOn = player.AddComponent<LockOnSystem>();

            PlayerController controller = player.AddComponent<PlayerController>();
            controller.lockOn = lockOn;
            controller.hittableLayers = ~0;

            // ---------- Camera ----------
            GameObject camObj = new GameObject("ThirdPersonCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            camObj.AddComponent<AudioListener>();

            ThirdPersonCamera camRig = camObj.AddComponent<ThirdPersonCamera>();
            camRig.target = player.transform;
            camRig.lockOn = lockOn;
            camObj.transform.position = player.transform.position - Vector3.forward * 4.5f + Vector3.up * 1.6f;

            camObj.AddComponent<PS1RenderEffect>();
            controller.cameraTransform = camObj.transform;

            // ---------- HUD ----------
            GameObject hudObj = new GameObject("GameHUD");
            GameHUD hud = hudObj.AddComponent<GameHUD>();
            hud.playerHealth = playerHealth;
            hud.playerStamina = playerStamina;

            // ---------- Enemy ----------
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Enemy_Basic";
            enemy.tag = "Enemy";
            enemy.transform.position = new Vector3(4f, 1f, 4f);
            ApplyRetroMaterial(enemy, new Color(0.1f, 0.08f, 0.12f));

            NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;

            Health enemyHealth = enemy.AddComponent<Health>();
            enemyHealth.maxHealth = 60f;

            EnemyAI ai = enemy.AddComponent<EnemyAI>();
            ai.player = player.transform;
            ai.hittableLayers = ~0;

            // ---------- Horror lighting ----------
            GameObject lightObj = new GameObject("Dim Moonlight");
            Light sun = lightObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.55f, 0.6f, 0.7f);
            sun.intensity = 0.35f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.03f);
            RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.07f);

            Selection.activeGameObject = player;
            Debug.Log("Souls-Like Horror test scene built. Remember to bake a NavMesh (Window > AI > Navigation) so the enemy can move.");
        }

        private static void ApplyRetroMaterial(GameObject go, Color color)
        {
            Shader shader = Shader.Find("SoulsLike/PS1VertexJitter");
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            if (shader != null)
            {
                Material mat = new Material(shader) { color = color };
                renderer.sharedMaterial = mat;
            }
            else
            {
                renderer.sharedMaterial.color = color;
            }
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
