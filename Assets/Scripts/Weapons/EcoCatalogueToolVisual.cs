using System.Collections.Generic;
using UnityEngine;
using Tool = PlayerWeaponSystem.Tool;

/// <summary>Art and mechanical motion for the field tools. All coordinates use +z as the working direction.</summary>
[DefaultExecutionOrder(35)]
public sealed partial class EcoCatalogueToolVisual : MonoBehaviour
{
    private static readonly Vector3 Forward = Vector3.forward;
    private static readonly Vector3 Up = Vector3.up;
    private static readonly Vector3 Right = Vector3.right;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private static readonly Color Amber = new Color(1f, .62f, .24f);
    private static readonly Color Mint = new Color(.39f, .86f, .65f);
    private static readonly Color Cyan = new Color(.36f, .77f, 1f);

    internal Transform Palm { get; private set; }
    internal Transform Support { get; private set; }
    internal Transform Muzzle { get; private set; }

    private readonly List<Object> generated = new List<Object>();
    private readonly MaterialPropertyBlock lampProperties = new MaterialPropertyBlock();
    private Material shell, edge, metal, rubber, glass, accent, signal;
    private Transform primary, secondary, selector;
    private MeshRenderer lamp;
    private PlayerWeaponSystem owner;
    private WeaponVisualEffects effects;
    private Tool kind;
    private Vector3 primaryRest, secondaryRest, selectorRest;
    private float work, angle, kick;
    private int seeds, ammo;

    internal static EcoCatalogueToolVisual Create(Tool kind, PlayerWeaponSystem owner, Transform parent)
    {
        var root = new GameObject(kind switch
        {
            Tool.Vacuum => "Katalog Vakumu", Tool.SeedGun => "Katalog Tohum Aracı", Tool.Recycler => "Katalog Geri Dönüşüm Aracı",
            Tool.Scanner => "Katalog Tarayıcı", Tool.Flashlight => "Katalog Feneri", _ => "Katalog Aracı"
        });
        root.SetActive(false);
        root.transform.SetParent(parent, false);
        var tool = root.AddComponent<EcoCatalogueToolVisual>();
        tool.owner = owner;
        tool.kind = kind;
        tool.MakeMaterials();
        switch (kind)
        {
            case Tool.Vacuum: tool.Vacuum(); break;
            case Tool.SeedGun: tool.SeedLauncher(); break;
            case Tool.Recycler: tool.Recycler(); break;
            case Tool.Scanner: tool.Scanner(); break;
            case Tool.Flashlight: tool.Flashlight(); break;
        }
        if (tool.primary != null) tool.primaryRest = tool.primary.localPosition;
        if (tool.secondary != null) tool.secondaryRest = tool.secondary.localPosition;
        if (tool.selector != null) tool.selectorRest = tool.selector.localPosition;
        return tool;
    }

    private void MakeMaterials()
    {
        Color paint = kind switch
        {
            Tool.Vacuum => new Color(.28f, .60f, .46f),
            Tool.SeedGun => new Color(.61f, .45f, .20f),
            Tool.Recycler => new Color(.59f, .28f, .16f),
            Tool.Scanner => new Color(.24f, .43f, .53f),
            _ => new Color(.32f, .42f, .25f)
        };
        Color highlight = kind == Tool.Scanner ? new Color(.58f, .78f, .79f) : new Color(.83f, .72f, .48f);
        shell = Surface("Boyalı alaşım", paint, .16f, .39f);
        edge = Surface("Açık renk kenar", Color.Lerp(paint, highlight, .6f), .15f, .4f);
        metal = Surface("Fırçalı metal", new Color(.52f, .58f, .55f), .44f, .48f);
        rubber = Surface("Koyu kauçuk", new Color(.055f, .083f, .073f), .02f, .26f);
        glass = Surface("Koyu cam", new Color(.14f, .28f, .24f), .22f, .72f);
        accent = Surface("Oker işaret", new Color(.86f, .55f, .19f), .11f, .32f);
        signal = Surface("Durum göstergesi", highlight, .1f, .54f);
        signal.EnableKeyword("_EMISSION");
    }

    private Material Surface(string name, Color color, float metallic, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = name, color = color };
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        generated.Add(material);
        return material;
    }

    private Transform Part(string name, Vector3 position, Transform parent = null)
    {
        var part = new GameObject(name).transform;
        part.SetParent(parent != null ? parent : transform, false);
        part.localPosition = position;
        return part;
    }

    private void Anchors(Vector3 palm, Vector3 muzzle, Vector3? support = null, Transform barrel = null)
    {
        Palm = Part("Sağ el tutuşu", palm);
        if (support.HasValue) Support = Part("Sol el desteği", support.Value);
        Muzzle = Part("Ağız merkezi", muzzle, barrel);
    }

    private void Finish(EcoToolMesh mesh, string name, Transform root = null)
        => mesh.Build(root != null ? root : transform, name, generated);

    private void Lamp(EcoToolMesh mesh, string name, Transform root = null)
        => lamp = mesh.Build(root != null ? root : transform, name, generated);

    private void OnEnable()
    {
        if (owner == null) return;
        seeds = owner.TotalSeeds;
        ammo = owner.MetalAmmo;
        kick = 0f;
        if (kind == Tool.Scanner) owner.Scanned += OnScan;
    }

    private void OnDisable()
    {
        if (owner != null && kind == Tool.Scanner) owner.Scanned -= OnScan;
        work = 0f;
    }

    private void OnScan(string result) => kick = 1f;

    private void LateUpdate()
    {
        if (owner == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (effects == null) effects = owner.GetComponentInChildren<WeaponVisualEffects>(true);
        bool working = effects != null && effects.Flowing && owner.Selected == kind;
        work = Mathf.MoveTowards(work, working ? 1f : 0f, dt * (working ? 8f : 4f));
        switch (kind)
        {
            case Tool.Vacuum:
                angle = Mathf.Repeat(angle + 1120f * work * dt, 360f);
                primary.localRotation = Quaternion.AngleAxis(angle, Forward);
                secondary.localPosition = secondaryRest + Up * (Mathf.Sin(angle * Mathf.Deg2Rad * 2f) * work * .004f);
                var mode = owner.Mode;
                RotateSelector(Quaternion.AngleAxis((int)mode * 120f, Up), dt);
                SetLamp(mode == PlayerWeaponSystem.VacuumMode.Water ? Cyan :
                    mode == PlayerWeaponSystem.VacuumMode.Disperse ? Mint : Amber, .48f + work * .86f);
                break;
            case Tool.SeedGun:
                if (owner.TotalSeeds < seeds || owner.MetalAmmo < ammo) kick = 1f;
                seeds = owner.TotalSeeds;
                ammo = owner.MetalAmmo;
                primary.localPosition = primaryRest - Forward * (.027f * kick);
                secondary.localRotation = Quaternion.AngleAxis(kick * 14f, Up);
                RotateSelector(Quaternion.AngleAxis(owner.MetalMode ? 76f : 0f, Right), dt);
                SetLamp(owner.MetalMode ? Amber : Mint, .45f + kick);
                break;
            case Tool.Recycler:
                angle = Mathf.Repeat(angle + dt * (24f + work * 470f), 360f);
                float bite = 4f + work * (7f + Mathf.Sin(angle * Mathf.Deg2Rad) * 8f);
                primary.localRotation = Quaternion.AngleAxis(-bite, Right);
                secondary.localRotation = Quaternion.AngleAxis(bite, Right);
                selector.localRotation = Quaternion.AngleAxis(angle, Right);
                SetLamp(work > .1f ? Mint : Amber, .45f + work * .8f);
                break;
            case Tool.Scanner:
                angle = Mathf.Repeat(angle + dt * 570f * kick, 360f);
                primary.localRotation = Quaternion.AngleAxis(angle, Forward);
                primary.localPosition = primaryRest + Forward * (.018f * kick);
                selector.localPosition = selectorRest + Up * (Mathf.Sin(Time.time * 20f) * kick * .004f);
                SetLamp(Cyan, .47f + kick * 1.55f);
                break;
            case Tool.Flashlight:
                bool on = owner.FlashlightOn;
                selector.localPosition = Vector3.Lerp(selector.localPosition,
                    selectorRest - Up * (on ? .004f : 0f), 1f - Mathf.Exp(-dt * 20f));
                break;
        }
        kick = Mathf.MoveTowards(kick, 0f, dt * (kind == Tool.SeedGun ? 6.4f : 2.7f));
    }

    private void RotateSelector(Quaternion rotation, float dt)
        => selector.localRotation = Quaternion.Slerp(selector.localRotation, rotation, 1f - Mathf.Exp(-dt * 16f));

    internal void SetFlashlight(bool on) => SetLamp(new Color(1f, .89f, .62f), on ? 2.15f : 0f);

    private void SetLamp(Color color, float strength)
    {
        if (lamp == null) return;
        lampProperties.SetColor(BaseColor, Color.Lerp(new Color(.24f, .34f, .31f), color, Mathf.Clamp01(strength + .12f)));
        lampProperties.SetColor(Emission, color * strength);
        lamp.SetPropertyBlock(lampProperties);
    }
}
