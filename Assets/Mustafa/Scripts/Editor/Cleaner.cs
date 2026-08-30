using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public class Cleaner
{
    static Cleaner()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (SessionState.GetBool("Cleaner_Executed", false)) return;
        SessionState.SetBool("Cleaner_Executed", true);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        }

        var ground = GameObject.Find("Grid/Ground")?.GetComponent<Tilemap>();
        if (ground != null)
        {
            ground.ClearAllTiles();
        }

        Debug.Log("Tilemap temizlendi ve Kamera duzeltildi.");
    }
}
