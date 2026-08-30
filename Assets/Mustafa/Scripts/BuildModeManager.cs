using UnityEngine;

public class BuildModeManager : MonoBehaviour
{
    [SerializeField] PlayerController playerController;
    [SerializeField] CameraController cameraController;
    [SerializeField] GameObject buildModeUI;
    [SerializeField] KeyCode toggleKey = KeyCode.B;

    bool isActive;

    public bool IsActive => isActive;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleBuildMode();

        if (isActive && Input.GetKeyDown(KeyCode.Escape))
            ExitBuildMode();
    }

    public void ToggleBuildMode()
    {
        if (isActive)
            ExitBuildMode();
        else
            EnterBuildMode();
    }

    void EnterBuildMode()
    {
        isActive = true;
        playerController.SetBuildMode(true);
        cameraController.EnterBuildMode();
        if (buildModeUI != null)
            buildModeUI.SetActive(true);
    }

    void ExitBuildMode()
    {
        isActive = false;
        playerController.SetBuildMode(false);
        cameraController.ExitBuildMode();
        if (buildModeUI != null)
            buildModeUI.SetActive(false);
    }
}
