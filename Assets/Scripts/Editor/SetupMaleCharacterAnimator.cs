using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public class SetupMaleCharacterAnimator : EditorWindow
{
    [MenuItem("EcoQuest/Setup Male Character Animator")]
    public static void SetupAnimator()
    {
        string savePath = "Assets/3D Assets/CHARACTER/MaleCharacter/MaleCharacterAnimator.controller";
        
        // Create Animator Controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(savePath);
        
        // Add Parameter
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        
        // Create Blend Tree
        BlendTree blendTree;
        AnimatorState state = controller.layers[0].stateMachine.AddState("Movement");
        controller.CreateBlendTreeInController("MovementBlendTree", out blendTree);
        
        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.blendParameter = "Speed";
        
        // Load Animation Clips
        AnimationClip idleClip = LoadAnimationClip("Assets/3D Assets/CHARACTER/MaleCharacter/Animasyon/Idle Male.fbx");
        AnimationClip walkClip = LoadAnimationClip("Assets/3D Assets/CHARACTER/MaleCharacter/Animasyon/Walking Male.fbx");
        AnimationClip runClip = LoadAnimationClip("Assets/3D Assets/CHARACTER/MaleCharacter/Animasyon/Running Male.fbx");
        
        if (idleClip == null || walkClip == null || runClip == null)
        {
            Debug.LogError("Animasyonlar bulunamadı! Lütfen dosya yollarını kontrol edin.");
            return;
        }

        // Add motions to blend tree
        blendTree.AddChild(idleClip, 0f);
        blendTree.AddChild(walkClip, 0.5f);
        blendTree.AddChild(runClip, 1.0f);
        
        // Assign blend tree to state
        state.motion = blendTree;
        
        Debug.Log("MaleCharacterAnimator başarıyla oluşturuldu: " + savePath);
    }

    private static AnimationClip LoadAnimationClip(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip && !asset.name.StartsWith("__preview__"))
            {
                return asset as AnimationClip;
            }
        }
        return null;
    }
}
