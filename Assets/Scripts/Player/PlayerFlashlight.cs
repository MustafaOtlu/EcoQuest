using UnityEngine;
using UnityEngine.Rendering;

// A held utility light, with its own switch but the same rechargeable electricity as the other tools.
[DefaultExecutionOrder(120)]
public sealed class PlayerFlashlight : MonoBehaviour
{
    public const float ElectricityPerSecond = 0.18f;
    public bool SwitchedOn { get; private set; } = true;
    public bool IsOn => beam != null && beam.enabled && gameObject.activeInHierarchy;

    private PlayerWeaponSystem weapons;
    private Light beam;
    private EcoCatalogueToolVisual visual;

    public static PlayerFlashlight Create(PlayerWeaponSystem owner, Transform parent)
    {
        var visual = EcoCatalogueToolVisual.Create(PlayerWeaponSystem.Tool.Flashlight, owner, parent);
        var flashlight = visual.gameObject.AddComponent<PlayerFlashlight>();
        flashlight.weapons = owner;
        flashlight.visual = visual;
        flashlight.BuildBeam(visual.Muzzle);
        return flashlight;
    }

    private void BuildBeam(Transform lens)
    {
        var lightObject = new GameObject("Flashlight beam", typeof(Light));
        lightObject.transform.SetParent(lens, false);
        beam = lightObject.GetComponent<Light>();
        beam.type = LightType.Spot;
        beam.color = new Color(1f, 0.94f, 0.77f);
        beam.intensity = 6f; beam.range = 17f;
        beam.spotAngle = 58f; beam.innerSpotAngle = 32f;
        beam.shadows = LightShadows.Soft;
        beam.shadowResolution = LightShadowResolution.Medium;
        beam.shadowBias = 0.04f; beam.shadowNormalBias = 0.15f;
        beam.enabled = false;
    }

    public void Toggle() => SetSwitch(!SwitchedOn);
    public void SetSwitch(bool on) { SwitchedOn = on; Refresh(0f); }
    public void Suspend() => ApplyLight(false);
    private void OnDisable() => Suspend();
    private void LateUpdate() => Refresh(Time.deltaTime);

    // Zero seconds also allows editor pose previews without consuming power or advancing the simulation.
    public void Refresh(float seconds)
    {
        bool held = weapons != null && weapons.isActiveAndEnabled && weapons.Selected == PlayerWeaponSystem.Tool.Flashlight
            && weapons.CanOperate && weapons.holder != null && weapons.holder.IsHandMounted
            && weapons.holder.EquippedWeapon == transform && gameObject.activeInHierarchy
            && (weapons.Builder == null || !weapons.Builder.IsBuilding);
        bool on = held && SwitchedOn && weapons.Energy > 0f;
        bool drain = on && Application.isPlaying && Application.isFocused && !PlayerController.Paused
            && !EcoUIFlow.InputBlocked && !EcoMarketUI.BlocksGameplay;
        if (drain && seconds > 0f && EcoRegion.Valid(seconds))
        {
            float amount = Mathf.Min(weapons.Energy, ElectricityPerSecond * seconds);
            weapons.TrySpendElectricity(amount);
            on = weapons.Energy > 0f;
        }
        ApplyLight(on);
    }

    private void ApplyLight(bool on)
    {
        if (beam != null) beam.enabled = on;
        if (visual != null) visual.SetFlashlight(on);
    }
}
