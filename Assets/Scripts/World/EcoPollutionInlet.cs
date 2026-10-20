using UnityEngine;

public sealed class EcoPollutionInlet : MonoBehaviour
{
    public EcoRegion region;
    public float reach = 3f;
    public bool Remediated => EcoDiscoveries.InletRemediated(region);
    private PlayerWeaponSystem player;
    private Renderer sign;
    private Material material;
    public string Describe() => Remediated ? "Kirletici giriş kapatıldı • Gölün kalan suyu arıtılmalı."
        : "Kirletici giriş • +3 su kirliliği / dakika\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Sızıntıyı kapat • 10M + 5P";
    public void CreateVisual()
    {
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Bakım direği"; post.transform.SetParent(transform, false);
        post.transform.localPosition = Vector3.up * 0.65f; post.transform.localScale = new Vector3(0.18f, 0.65f, 0.18f);
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Giriş vanası"; board.transform.SetParent(transform, false);
        board.transform.localPosition = Vector3.up * 1.4f; board.transform.localScale = new Vector3(1.2f, 0.65f, 0.18f);
        sign = board.GetComponent<Renderer>();
        material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")); sign.sharedMaterial = material;
    }
    private void Update()
    {
        if (region == null) region = EcoRegion.At(transform.position);
        if (!Remediated) region?.AddPollution(0f, Time.deltaTime * 0.05f, 0f, 0f);
        if (sign != null) material.color = Remediated ? new Color(0.25f, 0.7f, 0.4f) : new Color(0.85f, 0.48f, 0.12f);
        if (player == null) player = FindFirstObjectByType<PlayerWeaponSystem>();
    }
    public static bool TryInteract(PlayerWeaponSystem owner)
    {
        if (owner == null || !owner.CanOperate) return false;
        EcoPollutionInlet best = null; float nearest = float.PositiveInfinity;
        foreach (var inlet in FindObjectsByType<EcoPollutionInlet>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(owner.transform.position, inlet.transform.position);
            if (distance > inlet.reach || distance >= nearest) continue;
            nearest = distance; best = inlet;
        }
        if (best == null) return false;
        best.Interact(owner); return true;
    }
    public bool Interact(PlayerWeaponSystem owner)
    {
        if (owner == null || !owner.CanOperate || Vector3.Distance(owner.transform.position, transform.position) > reach) return false;
        EcoDiscoveries.DiscoverRegion(region);
        if (Remediated) { owner.ShowFeedback(Describe()); return true; }
        if (!owner.TrySpendMaterials(10, 5)) { owner.ShowFeedback("Giriş onarımı için 10 metal ve 5 plastik gerekiyor."); return false; }
        EcoDiscoveries.RemediateInlet(region);
        owner.ShowFeedback("Sızıntı kapatıldı. Doğu Gölü'ne yeni kirli giriş durdu; arıtmayı sürdür."); return true;
    }
    private void OnGUI()
    {
        if (player == null || EcoUIFlow.WorldBlocked || Vector3.Distance(player.transform.position, transform.position) > reach) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        GUI.Box(new Rect((Screen.width - 500f) * 0.5f, Screen.height - 170f, 500f, 58f), Describe(), style);
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
