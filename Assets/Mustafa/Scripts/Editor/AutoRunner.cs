using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class AutoRunner
{
    static AutoRunner()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (SessionState.GetBool("AutoRunner_Executed", false)) return;
        SessionState.SetBool("AutoRunner_Executed", true);

        // Access the wizard method via reflection or just duplicate the logic
        Debug.Log("AutoRunner executing...");
        var wizard = ScriptableObject.CreateInstance<AssetIntegrationWizard>();
        
        var method1 = typeof(AssetIntegrationWizard).GetMethod("SetupPlayerAnimations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method1 != null) method1.Invoke(wizard, null);

        var method2 = typeof(AssetIntegrationWizard).GetMethod("CreateBuildingData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method2 != null) method2.Invoke(wizard, null);
    }
}
