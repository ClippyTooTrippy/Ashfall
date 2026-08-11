using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;

public class AnimationControllerGenerator : EditorWindow
{
    private string animationsFolderPath = "Assets/Animations";
    private AnimatorController generatedController;

    [MenuItem("Tools/Generate Animation Controller")]
    public static void ShowWindow()
    {
        GetWindow<AnimationControllerGenerator>("Animation Controller Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Animation Controller Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        animationsFolderPath = EditorGUILayout.TextField("Animations Folder Path", animationsFolderPath);

        if (GUILayout.Button("Generate Controller"))
        {
            GenerateAnimationController();
        }

        if (generatedController != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Controller:", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField(generatedController, typeof(AnimatorController), false);

            if (GUILayout.Button("Save Controller"))
            {
                SaveController();
            }
        }
    }

    private void GenerateAnimationController()
    {
        // Normalize the folder path and ensure it is Assets-relative for AssetDatabase.FindAssets
        string assetsRelativePath = NormalizeToAssetsPath(animationsFolderPath);
        if (!AssetDatabase.IsValidFolder(assetsRelativePath))
        {
            EditorUtility.DisplayDialog("Error", "Animations folder not found: " + animationsFolderPath, "OK");
            return;
        }

        // Find all animation clips in the folder
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { assetsRelativePath });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Warning", "No animation clips found in the specified folder.", "OK");
            return;
        }

        List<AnimationClip> clips = new List<AnimationClip>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
                clips.Add(clip);
        }

        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No valid animation clips found.", "OK");
            return;
        }

        // Create new Animator Controller using an Assets-relative path
        string controllerPath = Path.Combine(assetsRelativePath, "GeneratedAnimatorController.controller").Replace('\\', '/');
        generatedController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (generatedController == null)
        {
            EditorUtility.DisplayDialog("Error", "Failed to create Animator Controller.", "OK");
            return;
        }

        // Ensure the base layer exists and has a valid state machine
        AnimatorControllerLayer layer;
        if (generatedController.layers.Length > 0)
        {
            layer = generatedController.layers[0];
            layer.name = "Base Layer";
            if (layer.stateMachine == null)
            {
                layer.stateMachine = new AnimatorStateMachine();
            }

            var layers = generatedController.layers;
            layers[0] = layer;
            generatedController.layers = layers;
        }
        else
        {
            layer = new AnimatorControllerLayer
            {
                name = "Base Layer",
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine()
            };
            generatedController.layers = new[] { layer };
        }

        // Force serialization of the state machine
        generatedController.layers = generatedController.layers;

        // Add parameters used by the runtime animation system
        generatedController.AddParameter("State", AnimatorControllerParameterType.Int);
        generatedController.AddParameter("Speed", AnimatorControllerParameterType.Float);
        generatedController.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        generatedController.AddParameter("Roll", AnimatorControllerParameterType.Trigger);
        generatedController.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

        // Create states for each animation clip
        AnimatorStateMachine sm = layer.stateMachine;
        Dictionary<int, AnimatorState> stateMap = new Dictionary<int, AnimatorState>();

        for (int i = 0; i < clips.Count; i++)
        {
            AnimatorState state = sm.AddState(clips[i].name);
            state.motion = clips[i];
            stateMap[i] = state;
        }

        // Create transitions from Any State to each state
        for (int i = 0; i < clips.Count; i++)
        {
            AnimatorStateTransition transition = sm.AddAnyStateTransition(stateMap[i]);
            transition.AddCondition(AnimatorConditionMode.Equals, i, "State");
            transition.hasExitTime = false;
            transition.duration = 0.1f;
        }

        // Set default state (first clip)
        sm.defaultState = stateMap[0];

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated Animator Controller at: {controllerPath}");

        EditorUtility.DisplayDialog("Success", $"Animator Controller created with {clips.Count} states.\nPath: {controllerPath}", "OK");
    }

    private string NormalizeToAssetsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string normalized = path.Trim().Replace('\\', '/');

        if (normalized.StartsWith("Assets/"))
        {
            return normalized;
        }

        string dataPathNormalized = Application.dataPath.Replace('\\', '/');
        if (normalized.StartsWith(dataPathNormalized))
        {
            normalized = "Assets" + normalized.Substring(dataPathNormalized.Length);
            return normalized;
        }

        if (normalized.StartsWith("/"))
        {
            normalized = normalized.Substring(1);
        }

        if (!normalized.StartsWith("Assets/"))
        {
            normalized = Path.Combine("Assets", normalized).Replace('\\', '/');
        }

        return normalized;
    }

    private void SaveController()
    {
        if (generatedController != null)
        {
            EditorUtility.SetDirty(generatedController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Saved", "Animator Controller saved successfully.", "OK");
        }
    }
}
// Trivial change to force reimport