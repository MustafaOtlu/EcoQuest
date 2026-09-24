using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class SetupMaleCharacterAnimator
{
    private const string Folder = "Assets/3D Assets/CHARACTER/MaleCharacter/";

    [MenuItem("EcoQuest/Setup Male Character Animator")]
    public static void SetupAnimator()
    {
        // Validate assets before touching the existing controller, preserving its GUID and scene references.
        AnimationClip idle = LoadClip(Folder + "Animasyon/Idle Male.fbx");
        AnimationClip walk = LoadClip(Folder + "Animasyon/Walking Male.fbx");
        AnimationClip run = LoadClip(Folder + "Animasyon/Running Male.fbx");
        if (idle == null || walk == null || run == null)
        {
            Debug.LogError("Idle, Walking or Running Male animation could not be loaded.");
            return;
        }

        string path = Folder + "MaleCharacterAnimator.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (!System.Array.Exists(controller.parameters, p => p.name == "Speed"))
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        var machine = controller.layers[0].stateMachine;
        AnimatorState movement = null;
        foreach (var child in machine.states)
            if (child.state.name == "Movement") movement = child.state;
        if (movement == null) movement = machine.AddState("Movement");
        var tree = movement.motion as BlendTree;
        if (tree == null)
        {
            tree = new BlendTree { name = "MovementBlendTree" };
            AssetDatabase.AddObjectToAsset(tree, controller);
            movement.motion = tree;
        }
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.children = new[]
        {
            new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
            new ChildMotion { motion = walk, threshold = 0.5f, timeScale = 1f },
            new ChildMotion { motion = run, threshold = 1f, timeScale = 1f }
        };
        machine.defaultState = movement;
        // The previous setup accidentally created a second state using the same tree.
        foreach (var child in machine.states)
            if (child.state != movement && child.state.name == "MovementBlendTree" && child.state.motion == tree)
                machine.RemoveState(child.state);
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(movement);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("MaleCharacterAnimator: Idle 0 / Walk 0.5 / Run 1.");
    }

    private static AnimationClip LoadClip(string path)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
        return null;
    }
}
