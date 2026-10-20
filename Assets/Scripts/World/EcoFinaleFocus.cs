using UnityEngine;
using UnityEngine.UI;

// The hit receiver is deliberately separate from EnemyBrain: killing a visual boss or hitting the wrong
// focus can never complete a phase. Only resources already spent by PlayerWeaponSystem enter here.
public sealed class EcoFinaleFocus : MonoBehaviour
{
    public EcoFinale Owner { get; private set; }
    public int Phase { get; private set; }
    public float WaterReceived { get; private set; }
    public float AirSeconds { get; private set; }
    public int MetalHits { get; private set; }
    public float Held { get; internal set; }
    public bool Balanced { get; internal set; }
    public bool Active => Owner != null && Owner.Fighting && Owner.Phase == Phase && !Balanced;

    private Renderer orb;
    private Text caption;
    private Material material;
    private static readonly string[] Names = { "ISI ODAĞI", "DUMAN ODAĞI", "ÇEKİRDEK" };
    private static readonly Color[] Colors = { new Color(1f, 0.37f, 0.12f), new Color(0.67f, 0.43f, 0.93f), new Color(0.3f, 0.75f, 1f) };

    public void Initialize(EcoFinale owner, int phase)
    {
        Owner = owner; Phase = Mathf.Clamp(phase, 0, 2);
        gameObject.name = Names[Phase];
        var hit = gameObject.AddComponent<SphereCollider>(); hit.center = Vector3.up * 1.25f; hit.radius = 0.9f;
        EcoNavigation.Ignore(gameObject);
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Focus orb"; sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = Vector3.up * 1.25f; sphere.transform.localScale = Vector3.one * 1.4f;
        Destroy(sphere.GetComponent<Collider>()); orb = sphere.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
        if (shader != null) { material = new Material(shader); orb.sharedMaterial = material; }
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Focus ground ring"; ring.transform.SetParent(transform, false);
        ring.transform.localPosition = Vector3.up * 0.05f; ring.transform.localScale = new Vector3(2.2f, 0.04f, 2.2f);
        Destroy(ring.GetComponent<Collider>()); if (material != null) ring.GetComponent<Renderer>().sharedMaterial = material;

        var label = new GameObject("Focus instruction", typeof(RectTransform), typeof(Canvas), typeof(WorldSpaceLabel));
        label.transform.SetParent(transform, false); label.transform.localPosition = Vector3.up * 2.9f;
        label.transform.localScale = Vector3.one * 0.008f;
        label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        label.GetComponent<RectTransform>().sizeDelta = new Vector2(430f, 120f);
        var text = new GameObject("Text", typeof(RectTransform), typeof(Text)); text.transform.SetParent(label.transform, false);
        var rect = text.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        caption = text.GetComponent<Text>(); caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        caption.fontSize = 26; caption.alignment = TextAnchor.MiddleCenter; caption.color = Color.white;
        caption.raycastTarget = false;
        var outline = text.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(1.2f, -1.2f);
        RefreshVisual();
    }

    public bool TryUseWater(float actualCleanUnits)
    {
        if (!Active || Phase != 0 || !EcoRegion.Valid(actualCleanUnits)) return false;
        WaterReceived = Mathf.Min(Owner.RequiredWater, WaterReceived + actualCleanUnits);
        RefreshVisual(); return true;
    }

    public bool TryUseAir(float seconds)
    {
        if (!Active || Phase != 1 || !EcoRegion.Valid(seconds)) return false;
        AirSeconds = Mathf.Min(3f, AirSeconds + seconds);
        RefreshVisual(); return true;
    }

    public bool TryHitMetal()
    {
        if (!Active || Phase != 2) return false;
        MetalHits = Mathf.Min(Owner.RequiredMetalHits, MetalHits + 1);
        RefreshVisual(); return true;
    }

    public string Describe()
    {
        if (Balanced) return Names[Phase] + " • DENGELENDİ";
        if (!Active) return Names[Phase] + " • Önceki odağı dengele";
        if (Phase == 0) return Names[Phase] + "\nTemiz su " + WaterReceived.ToString("0.0") + "/" + Owner.RequiredWater.ToString("0.0") + " SB • Destek " + Held.ToString("0.0") + "/4 sn";
        if (Phase == 1) return Names[Phase] + "\nAnti-vakum " + AirSeconds.ToString("0.0") + "/3 sn • Destek " + Held.ToString("0.0") + "/4 sn";
        return Names[Phase] + "\nDemir top " + MetalHits + "/" + Owner.RequiredMetalHits + (MetalHits >= Owner.RequiredMetalHits ? " • Terminalde E" : " • Zırhı aç");
    }

    public void RefreshVisual()
    {
        if (caption != null) caption.text = Describe();
        Color color = Balanced ? new Color(0.25f, 0.9f, 0.45f) : Active ? Colors[Phase] : new Color(0.3f, 0.34f, 0.4f);
        if (material != null) { material.color = color; if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); }
    }

    private void OnDestroy() { if (material != null) Destroy(material); }
}
