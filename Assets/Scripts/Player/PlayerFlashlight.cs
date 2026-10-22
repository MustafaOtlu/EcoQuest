using System.Collections.Generic;
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
    private Material lens;
    private readonly List<Material> materials = new List<Material>();

    public static PlayerFlashlight Create(PlayerWeaponSystem owner, Transform parent)
    {
        var root = new GameObject("El feneri");
        root.transform.SetParent(parent, false);
        var flashlight = root.AddComponent<PlayerFlashlight>();
        flashlight.weapons = owner;
        flashlight.BuildModel();
        root.SetActive(false);
        return flashlight;
    }

    private void BuildModel()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var body = Paint(shader, new Color(0.19f, 0.34f, 0.25f), 0.35f);
        var rubber = Paint(shader, new Color(0.055f, 0.075f, 0.065f), 0.05f);
        var metal = Paint(shader, new Color(0.43f, 0.49f, 0.47f), 0.65f);
        lens = Paint(shader, new Color(0.70f, 0.79f, 0.77f), 0.4f);
        lens.EnableKeyword("_EMISSION");
        Cylinder("Grip", 0f, 0.062f, 0.22f, body);
        Cylinder("Rear cap", -0.12f, 0.072f, 0.028f, rubber);
        Cylinder("Head casing", 0.11f, 0.104f, 0.064f, rubber);
        Cylinder("Head rim", 0.143f, 0.108f, 0.014f, metal);
        Cylinder("LED lens", 0.152f, 0.086f, 0.008f, lens);
        foreach (float z in new[] { -0.075f, -0.025f, 0.025f }) Cylinder("Grip ring", z, 0.068f, 0.009f, rubber);
        var button = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        button.name = "Power button"; button.transform.SetParent(transform, false);
        button.transform.localPosition = new Vector3(0f, 0.034f, 0.02f);
        button.transform.localScale = new Vector3(0.022f, 0.008f, 0.03f);
        button.GetComponent<Renderer>().sharedMaterial = rubber;
        RemoveCollider(button);

        var lightObject = new GameObject("Flashlight beam", typeof(Light));
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0, 0, 0.166f);
        lightObject.transform.localRotation = Quaternion.Euler(12f, 0, 0);
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

    private Material Paint(Shader shader, Color color, float metallic)
    {
        var material = new Material(shader) { name = "Flashlight finish", color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.45f);
        materials.Add(material); return material;
    }

    private void Cylinder(string label, float z, float diameter, float length, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = label; part.transform.SetParent(transform, false);
        part.transform.localPosition = new Vector3(0, 0, z);
        part.transform.localRotation = Quaternion.Euler(90, 0, 0);
        part.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
        part.GetComponent<Renderer>().sharedMaterial = material;
        RemoveCollider(part);
    }

    private static void RemoveCollider(GameObject part)
    {
        var collider = part.GetComponent<Collider>();
        if (collider == null) return;
        if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
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
        if (lens != null && lens.HasProperty("_EmissionColor"))
            lens.SetColor("_EmissionColor", on ? new Color(1f, 0.92f, 0.65f) * 2.4f : Color.black);
    }

    private void OnDestroy()
    {
        foreach (var material in materials)
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
    }
}
