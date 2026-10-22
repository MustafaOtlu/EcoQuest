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
        // GDD §13: a line the child is saying stays up while the player stands next to them; it fades on its own timer once they leave.
        if (weapons != null && child != null && Time.time < weapons.FeedbackUntil && weapons.Feedback != null && weapons.Feedback.StartsWith("Çocuk")
            && (weapons.transform.position - child.position).sqrMagnitude <= Reach * Reach)
            weapons.HoldFeedback(Time.time + 1.5f);
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1f;
        if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        if (child == null) CreateChild();
        // Idempotent: a loaded save with finished care moments still holds their YEP (older builds granted none).
        if (weapons != null && weapons.Progression != null)
        {
            if ((completed & 1) != 0) weapons.Progression.AwardOnce("care.water", 20);
            if ((completed & 2) != 0) weapons.Progression.AwardOnce("care.meal", 20);
            if ((completed & 4) != 0) weapons.Progression.AwardOnce("care.birds", 20);
        }
        if (weapons == null || weapons.Progression == null) return;
        // Field milestones (harvest/replant/fish) are rewarded by EcoDiscoveries with their stricter conditions.
        if (!requestedHint && EcoProjects.Current != null) foreach (int level in EcoProjects.Current.HintLevels) if (level > 0) requestedHint = true;
    }

    private void CreateChild()
    {
        // The child waits at the front door of the home in town (map builder); older scenes fall back to the market.
        var door = GameObject.Find("Eco Home door");
        var market = FindFirstObjectByType<EcoMarket>();
        Vector3 position = door != null ? door.transform.position
            : (market != null ? market.transform.position : transform.position) + new Vector3(5f, 0f, 3f);
        if (Physics.Raycast(position + Vector3.up * 40f, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore)) position = hit.point;
        var go = new GameObject("Mahalle çocuğu");
        go.transform.position = position;
        if (door != null) go.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(door.transform.position - door.transform.parent.position, Vector3.up).normalized + Vector3.forward * 1e-4f);
        var trigger = go.AddComponent<CapsuleCollider>(); trigger.isTrigger = true; trigger.radius = 0.35f; trigger.height = 1.3f; trigger.center = Vector3.up * 0.65f;
        EcoNavigation.Ignore(go);
        // A small stylised figure: legs, shirt, head and hair, so the child reads as a person from the iso camera.
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material Paint(Color c) { var m = new Material(shader); m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); return m; }
        void Part(PrimitiveType type, Vector3 at, Vector3 size, Material m)
        {
            var part = GameObject.CreatePrimitive(type); Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(go.transform, false); part.transform.localPosition = at; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = m;
        }
        if (shader != null)
        {
            var trousers = Paint(new Color(0.22f, 0.32f, 0.52f)); var shirt = Paint(new Color(0.98f, 0.72f, 0.28f));
            var skin = Paint(new Color(0.94f, 0.78f, 0.64f)); var hair = Paint(new Color(0.33f, 0.21f, 0.13f));
            Part(PrimitiveType.Capsule, new Vector3(-0.09f, 0.27f, 0f), new Vector3(0.14f, 0.27f, 0.14f), trousers);
            Part(PrimitiveType.Capsule, new Vector3(0.09f, 0.27f, 0f), new Vector3(0.14f, 0.27f, 0.14f), trousers);
            Part(PrimitiveType.Capsule, new Vector3(0f, 0.72f, 0f), new Vector3(0.36f, 0.26f, 0.26f), shirt);
            Part(PrimitiveType.Capsule, new Vector3(-0.22f, 0.7f, 0f), new Vector3(0.1f, 0.2f, 0.1f), shirt);
            Part(PrimitiveType.Capsule, new Vector3(0.22f, 0.7f, 0f), new Vector3(0.1f, 0.2f, 0.1f), shirt);
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.1f, 0f), new Vector3(0.32f, 0.32f, 0.32f), skin);
            Part(PrimitiveType.Sphere, new Vector3(0f, 1.18f, 0.03f), new Vector3(0.34f, 0.22f, 0.34f), hair);
        }
        EcoMapMarker.Add(go, "poi_home", "Mahalle çocuğu", "Bakım anları ve günlük sohbetler. Yanına gidip " + EcoKeys.Tag(EcoKeys.Action.Interact) + " ile konuş.", EcoMapMarker.Category.Quest, true);
        var label = new GameObject("Label", typeof(RectTransform), typeof(Canvas), typeof(WorldSpaceLabel));
        label.transform.SetParent(go.transform, false); label.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var caption = new GameObject("Caption", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        caption.transform.SetParent(label.transform, false);
        var text = caption.GetComponent<UnityEngine.UI.Text>();
        text.font = EcoUI.Font; text.color = Color.white; text.raycastTarget = false;
        text.text = "MAHALLE ÇOCUĞU\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Konuş";
        child = go.transform;
    }

    /// <summary>HUD context line while the child is within reach.</summary>
    public string Prompt(PlayerWeaponSystem w)
        => w != null && child != null && (w.transform.position - child.position).sqrMagnitude <= Reach * Reach
            ? "Mahalle çocuğu • " + EcoKeys.Tag(EcoKeys.Action.Interact) + " Konuş" : "";
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
        completed |= bit; int granted = w.Progression != null ? w.Progression.AwardOnce("care." + id, 20) : 0;
        w.ShowFeedback("Bakım anı tamamlandı • " + title + (granted > 0 ? " • +" + granted + " YEP" : ""));
    }
}
