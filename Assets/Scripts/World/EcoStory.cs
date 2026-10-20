using UnityEngine;

// GDD §6: the neighbourhood child and three care episodes (clean glass of water, healthy meal, birds at the window),
// 20 YEP each, plus three field milestones (first clean harvest, replanted tree, clean fish) of 20 YEP each.
// Field names are saved by reflection (EcoSaveIdentity); rewards are idempotent through AwardOnce identities.
public sealed class EcoStory : MonoBehaviour
{
    public static EcoStory Current { get; private set; }
    public int completed;                       // bit 1 water, 2 meal, 4 birds
    public bool cleanHarvest, replanted, cleanFish, intro, finalConversation, requestedHint;

    private Transform child;
    private PlayerWeaponSystem weapons;
    private float nextScan;
    private const float Reach = 4.5f;

    private void Awake() { Current = this; }
    private void OnDestroy() { if (Current == this) Current = null; }

    private void Update()
    {
        if (child != null && Camera.main != null)
        {
            var label = child.Find("Label");
            if (label != null) label.rotation = Camera.main.transform.rotation;
        }
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1f;
        if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        if (child == null) CreateChild();
        if (weapons == null || weapons.Progression == null) return;
        // Field milestones (harvest/replant/fish) are rewarded by EcoDiscoveries with their stricter conditions.
        if (!requestedHint && EcoProjects.Current != null) foreach (int level in EcoProjects.Current.HintLevels) if (level > 0) requestedHint = true;
    }

    private void CreateChild()
    {
        var market = FindFirstObjectByType<EcoMarket>();
        Vector3 basePosition = market != null ? market.transform.position : transform.position;
        Vector3 position = basePosition + new Vector3(5f, 0f, 3f);
        if (Physics.Raycast(position + Vector3.up * 40f, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore)) position = hit.point;
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Mahalle çocuğu"; go.transform.position = position + Vector3.up * 0.75f; go.transform.localScale = new Vector3(0.6f, 0.75f, 0.6f);
        var collider = go.GetComponent<Collider>(); if (collider != null) collider.isTrigger = true;
        EcoNavigation.Ignore(go);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null) { var material = new Material(shader); material.color = new Color(0.98f, 0.75f, 0.35f); if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.color); go.GetComponent<Renderer>().sharedMaterial = material; }
        var label = new GameObject("Label"); label.transform.SetParent(go.transform, false); label.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        label.transform.localScale = new Vector3(1f / 0.6f, 1f / 0.75f, 1f / 0.6f);
        var mesh = label.AddComponent<TextMesh>(); var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.font = font; label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        mesh.text = "MAHALLE ÇOCUĞU\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Konuş"; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.characterSize = 0.1f; mesh.fontSize = 48; mesh.color = Color.white;
        child = go.transform;
    }

    public bool TryInteract(PlayerWeaponSystem w)
    {
        if (w == null || child == null || (w.transform.position - child.position).sqrMagnitude > Reach * Reach) return false;
        if (!intro) { intro = true; w.ShowFeedback("Çocuk: \"Su, yemek ve pencereden kuş sesi istiyorum. Mahalle düzelirse hepsi olur.\""); return true; }
        var finale = EcoFinale.Current;
        if (finale != null && finale.Won) { finalConversation = true; w.ShowFeedback("Çocuk: \"Artık nefes alabiliyoruz. Teşekkürler!\""); return true; }
        if ((completed & 1) == 0 && w.TryTakeCleanWater(2f)) { Finish(w, 1, "water", "Temiz bir bardak su"); return true; }
        if ((completed & 2) == 0 && w.TryConsumeCareFood()) { Finish(w, 2, "meal", "Sağlıklı sofra"); return true; }
        if ((completed & 4) == 0)
        {
            var grove = EcoProjects.Current != null ? EcoProjects.Current.GroveRegion : null;
            float habitat = grove != null && grove.TryGetComponent<EcoHabitat>(out var wildlife) ? wildlife.recovery : 0f;
            if (habitat >= 0.3f && EcoWorldClock.Current != null && EcoWorldClock.Current.SolarFactor > 0.2f) { Finish(w, 4, "birds", "Penceredeki kuşlar"); return true; }
        }
        if (completed == 7) { w.ShowFeedback("Çocuk mutlu. Üç bakım anı tamam."); return true; }
        w.ShowFeedback("Çocuk bekliyor: " + ((completed & 1) == 0 ? "2 SB temiz su • " : "") + ((completed & 2) == 0 ? "2 temiz ürün veya 1 temiz balık • " : "")
            + ((completed & 4) == 0 ? "gündüz koruluk habitatı %30+" : ""));
        return true;
    }

    private void Finish(PlayerWeaponSystem w, int bit, string id, string title)
    {
        completed |= bit; w.Progression?.AwardOnce("care." + id, 20);
        w.ShowFeedback("Bakım anı tamamlandı • " + title + " • +20 YEP");
    }
}
