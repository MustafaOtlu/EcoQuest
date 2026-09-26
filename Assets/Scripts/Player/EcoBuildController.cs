using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10)]
public sealed class EcoBuildController : MonoBehaviour
{
    public PlayerWeaponSystem weapons;
    public bool IsBuilding { get; private set; }
    public int Selection { get; private set; }
    public string Status { get; private set; } = "";
    public int Page => Selection / 5;
    public int PageCount => (EcoBuildingCatalog.Entries.Length + 6) / 5;
    public int CableIndex => EcoBuildingCatalog.Entries.Length;
    public int PipeIndex => CableIndex + 1;
    public EcoWaterNode.Fluid PipeFluid { get; private set; }
    public string SelectedName => Selection == CableIndex ? "Elektrik kablosu" : Selection == PipeIndex ? (PipeFluid == EcoWaterNode.Fluid.Clean ? "Temiz su borusu" : "Kirli su borusu") : EcoBuildingCatalog.Entries[Selection].Name;
    private GameObject preview;
    private Material previewMaterial;
    private EcoPowerNode cableStart;
    private EcoWaterNode pipeStart;
    private float yaw;
    private const float Reach = 8f;
    public string SlotName(int slot)
    {
        int i = Page * 5 + slot;
        return i < CableIndex ? EcoBuildingCatalog.Entries[i].Name : i == CableIndex ? "Kablo" : i == PipeIndex ? "Su borusu" : "—";
    }
    public string SlotCost(int slot)
    {
        int i = Page * 5 + slot;
        return i < CableIndex ? EcoBuildingCatalog.Entries[i].Metal + "M " + EcoBuildingCatalog.Entries[i].Plastic + "P • Sv" + EcoBuildingCatalog.Entries[i].Level
            : i == CableIndex ? "1M + 1P / m" : i == PipeIndex ? "2P / m" : "";
    }
    public void NextPage() => Select(((Page + 1) % PageCount) * 5);
    public void TogglePipeFluid() { PipeFluid = PipeFluid == EcoWaterNode.Fluid.Dirty ? EcoWaterNode.Fluid.Clean : EcoWaterNode.Fluid.Dirty; pipeStart = null; }

    public void SetBuildMode(bool active)
    {
        active &= weapons != null && weapons.CanOperate;
        if (active == IsBuilding) return;
        IsBuilding = active; cableStart = null; pipeStart = null;
        if (active) { weapons.StopCharging(); weapons.holder.Unequip(); Select(Selection); }
        else
        {
            if (preview != null) { preview.SetActive(false); Destroy(preview); }
            if (weapons != null) weapons.SelectTool((int)weapons.Selected);
            Status = "";
        }
    }
    public void Select(int index)
    {
        if (index < 0 || index > PipeIndex) return;
        Selection = index; cableStart = null; pipeStart = null;
        if (preview != null) { preview.SetActive(false); Destroy(preview); }
        if (!IsBuilding || index >= CableIndex) return;
        var prefab = Resources.Load<GameObject>("EcoInfrastructure/" + EcoBuildingCatalog.Entries[index].Prefab);
        if (prefab == null) { Status = "Yapı modeli bulunamadı."; return; }
        preview = Instantiate(prefab); preview.name = "Build preview";
        foreach (var behaviour in preview.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        foreach (var collider in preview.GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (var child in preview.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
        if (previewMaterial == null)
        {
            previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            previewMaterial.SetFloat("_Surface", 1); previewMaterial.SetFloat("_ZWrite", 0);
            previewMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            previewMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            previewMaterial.renderQueue = 3000;
        }
        foreach (var renderer in preview.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterials = Enumerable.Repeat(previewMaterial, renderer.sharedMaterials.Length).ToArray();
    }
    private void Update()
    {
        if (weapons == null) return;
        if (!weapons.CanOperate) { SetBuildMode(false); return; }
        if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked) return;
        var keys = Keyboard.current; var mouse = Mouse.current;
        if (keys != null && keys.bKey.wasPressedThisFrame) SetBuildMode(!IsBuilding);
        if (!IsBuilding) return;
        if (keys != null)
        {
            if (keys.tabKey.wasPressedThisFrame) NextPage();
            int offset = Page * 5;
            if (keys.digit1Key.wasPressedThisFrame) Select(offset);
            if (keys.digit2Key.wasPressedThisFrame) Select(offset + 1);
            if (keys.digit3Key.wasPressedThisFrame) Select(offset + 2);
            if (keys.digit4Key.wasPressedThisFrame) Select(offset + 3);
            if (keys.digit5Key.wasPressedThisFrame) Select(offset + 4);
            if (keys.qKey.wasPressedThisFrame && Selection == PipeIndex) TogglePipeFluid();
            if (keys.rKey.wasPressedThisFrame) yaw = Mathf.Repeat(yaw + 90f, 360f);
        }
        if (!Aim(out var hit)) { if (preview != null) preview.SetActive(false); Status = "Sekiz metre içinde düz bir zemine veya bağlantı noktasına nişan al."; return; }
        if (keys != null && keys.uKey.wasPressedThisFrame) hit.collider.GetComponentInParent<EcoStructure>()?.Upgrade(weapons);
        if (keys != null && keys.tKey.wasPressedThisFrame)
        {
            var structure = hit.collider.GetComponentInParent<EcoStructure>();
            if (structure == null || !structure.Repair(weapons)) weapons.ShowFeedback("Tamir gerekmiyor veya malzeme yetersiz.");
        }
        if (keys != null && keys.xKey.wasPressedThisFrame)
        {
            if (Selection == CableIndex) RemoveCable(hit.collider.GetComponentInParent<EcoPowerNode>());
            else if (Selection == PipeIndex) RemovePipe(pipeStart, hit.collider.GetComponentInParent<EcoWaterNode>(), PipeFluid);
            else
            {
                var structure = hit.collider.GetComponentInParent<EcoStructure>();
                if (structure == null || !structure.Dismantle(weapons)) weapons.ShowFeedback("Yalnızca kurduğun yapıları sökebilirsin.");
            }
        }
        if (Selection == PipeIndex)
        {
            var node = hit.collider.GetComponentInParent<EcoWaterNode>();
            Status = (pipeStart == null ? "Önce suyun çıkacağı tesisi seç." : "Şimdi suyun gideceği tesisi seç. X: boruyu sök") + "\n[Q] Temiz/kirli hat • " + SelectedName;
            if (node != null && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (pipeStart == null)
                {
                    if (node.CanSend(PipeFluid)) pipeStart = node;
                    else weapons.ShowFeedback("Bu tesis seçili su türünü dışarı vermez.");
                }
                else if (TryConnectPipe(pipeStart, node, PipeFluid)) pipeStart = null;
            }
            return;
        }
        if (Selection == CableIndex)
        {
            var node = hit.collider.GetComponentInParent<EcoPowerNode>();
            Status = cableStart == null ? "İlk elektrik bağlantı noktasını seç." : "İkinci noktayı seç • X: aradaki kabloyu sök";
            if (node != null && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (cableStart == null) cableStart = node;
                else if (TryConnect(cableStart, node)) cableStart = null;
            }
            return;
        }
        var position = hit.point + Vector3.up * 0.03f; var rotation = Quaternion.Euler(0, yaw, 0);
        bool valid = CanPlace(Selection, position, rotation, out string reason);
        Status = reason;
        var targetStructure = hit.collider.GetComponentInParent<EcoStructure>();
        if (targetStructure != null) Status = "[T] Tamir • [X] Sök • Sağlamlık %" + targetStructure.integrity.ToString("0") + "\n" + targetStructure.UpgradeHint;
        if (preview != null)
        {
            preview.SetActive(true); preview.transform.SetPositionAndRotation(position, rotation);
            previewMaterial.SetColor("_BaseColor", valid ? new Color(0.2f, 0.9f, 0.4f, 0.5f) : new Color(0.95f, 0.15f, 0.1f, 0.5f));
        }
        if (valid && mouse != null && mouse.leftButton.wasPressedThisFrame) TryPlace(Selection, position, rotation);
    }
    private bool Aim(out RaycastHit nearest)
    {
        nearest = default; var camera = Camera.main; if (camera == null) return false;
        float distance = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, Reach, ~(1 << 2), QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.distance < distance) { nearest = hit; distance = hit.distance; }
        return distance < float.PositiveInfinity;
    }
    public bool CanPlace(int index, Vector3 position, Quaternion rotation, out string reason)
    {
        reason = "Geçersiz yapı.";
        if (weapons == null || !weapons.CanOperate || index < 0 || index >= EcoBuildingCatalog.Entries.Length) return false;
        var entry = EcoBuildingCatalog.Entries[index];
        if (!weapons.Progression.HasLevel(entry.Level)) { reason = "Bu yapı YEP " + entry.Level + ". seviyede açılır."; return false; }
        if (EcoStructure.Active.Count(s => s != null && s.buildingId == entry.Id) >= EcoBuildingCatalog.Limit(index, weapons.Progression.Level))
        { reason = "Bu seviyedeki yapı sınırına ulaştın."; return false; }
        if (Vector3.Distance(transform.position, position) > Reach) { reason = "Yapı çok uzakta."; return false; }
        if (weapons.Metal < entry.Metal || weapons.Plastic < entry.Plastic) { reason = "Malzeme gerekli: " + entry.Metal + " metal / " + entry.Plastic + " plastik"; return false; }
        if (entry.Id == "intake")
        {
            bool nearSource = FindObjectsByType<WeaponWaterSource>(FindObjectsSortMode.None).Any(s =>
                Vector3.Distance(position, s.GetComponentInChildren<Collider>() is Collider c ? c.ClosestPoint(position) : s.transform.position) <= 3f);
            if (!nearSource) { reason = "Pompa bir su kaynağının en fazla üç metre yakınına kurulmalı."; return false; }
        }
        rotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
        for (int i = 0; i < 5; i++)
        {
            Vector3 corner = i == 0 ? Vector3.zero : rotation * new Vector3((i % 2 == 0 ? -1 : 1) * entry.Footprint.x * 0.45f, 0, (i <= 2 ? -1 : 1) * entry.Footprint.y * 0.45f);
            if (!Physics.Raycast(position + corner + Vector3.up * 0.4f, Vector3.down, out var floor, 0.7f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                || floor.normal.y < 0.9f || Mathf.Abs(floor.point.y - (position.y - 0.03f)) > 0.12f
                || floor.collider.GetComponentInParent<EcoStructure>() != null
                || (floor.collider is not TerrainCollider && floor.collider.GetComponent<PlantingSurface>() == null))
            { reason = "Yapının tamamı düz, sağlam zeminde olmalı."; return false; }
        }
        var prefab = Resources.Load<GameObject>("EcoInfrastructure/" + entry.Prefab);
        if (prefab == null) { reason = "Yapı modeli bulunamadı."; return false; }
        float height = prefab.GetComponent<BoxCollider>().size.y;
        foreach (var obstacle in Physics.OverlapBox(position + Vector3.up * (height * 0.5f + 0.03f), new Vector3(entry.Footprint.x * 0.48f, height * 0.5f, entry.Footprint.y * 0.48f), rotation, ~(1 << 2), QueryTriggerInteraction.Ignore))
            if (obstacle.GetComponentInParent<EcoStructure>() != null || (obstacle is not TerrainCollider && obstacle.GetComponent<PlantingSurface>() == null))
            { reason = "Başka bir nesneyle veya oyuncuyla çakışıyor."; return false; }
        reason = "Sol tık: kur • R: döndür • T: tamir • X: sök"; return true;
    }
    public bool TryPlace(int index, Vector3 position, Quaternion rotation)
    {
        if (!IsBuilding || !CanPlace(index, position, rotation, out string reason)) { weapons?.ShowFeedback("Buraya yapı kurulamıyor."); return false; }
        var entry = EcoBuildingCatalog.Entries[index];
        if (!weapons.TrySpendMaterials(entry.Metal, entry.Plastic)) return false;
        var go = Instantiate(Resources.Load<GameObject>("EcoInfrastructure/" + entry.Prefab), position, Quaternion.Euler(0, rotation.eulerAngles.y, 0));
        var structure = go.GetComponent<EcoStructure>(); structure.playerBuilt = true;
        if (go.TryGetComponent<EcoWaterNode>(out var water) && water.kind == EcoWaterNode.Kind.Intake) water.FindSource();
        weapons.Progression.Award(12); weapons.ShowFeedback(entry.Name + " kuruldu." + (water != null ? " Su borusuyla bağla." : " Elektrik kablosuyla bağla.")); return true;
    }
    public bool TryConnect(EcoPowerNode a, EcoPowerNode b)
    {
        if (!IsBuilding || !weapons.CanOperate || a == null || b == null || a == b || !a.Operational || !b.Operational) return false;
        if (EcoPowerCable.Active.Any(c => c != null && ((c.a == a && c.b == b) || (c.b == a && c.a == b)))) { weapons.ShowFeedback("Bu iki nokta zaten bağlı."); return false; }
        float length = Vector3.Distance(a.SocketPosition, b.SocketPosition);
        if (length > 15f || Vector3.Distance(transform.position, b.transform.position) > Reach) { weapons.ShowFeedback("Bağlantı mesafesi fazla."); return false; }
        int cost = Mathf.CeilToInt(length);
        if (!weapons.TrySpendMaterials(cost, cost)) { weapons.ShowFeedback("Kablo için " + cost + " metal ve plastik gerekiyor."); return false; }
        var go = new GameObject("Player power cable"); var link = go.AddComponent<EcoPowerCable>(); link.a = a; link.b = b; link.playerBuilt = true; link.materialCost = cost;
        var renderer = go.GetComponent<LineRenderer>(); renderer.sharedMaterial = Resources.Load<Material>("EcoInfrastructure/Cable"); renderer.startWidth = renderer.endWidth = 0.035f;
        weapons.ShowFeedback("Elektrik hattı bağlandı."); return true;
    }
    private void RemoveCable(EcoPowerNode end)
    {
        var link = EcoPowerCable.Active.FirstOrDefault(c => c != null && c.playerBuilt && ((c.a == cableStart && c.b == end) || (c.b == cableStart && c.a == end)));
        if (link == null) { weapons.ShowFeedback("Önce kablonun ilk ucunu seç, sonra diğer uca bakıp X kullan."); return; }
        link.playerBuilt = false; link.connected = false; weapons.ReturnMaterials(link.materialCost / 2, link.materialCost / 2); Destroy(link.gameObject); cableStart = null;
    }
    public bool TryConnectPipe(EcoWaterNode from, EcoWaterNode to, EcoWaterNode.Fluid fluid)
    {
        if (!IsBuilding || !weapons.CanOperate || from == null || to == null || from == to || !from.CanSend(fluid) || !to.CanReceive(fluid))
        { weapons?.ShowFeedback("Bu yönde veya bu su türüyle boru bağlanamaz."); return false; }
        if (EcoWaterPipe.Active.Any(p => p != null && p.from == from && p.to == to && p.fluid == fluid))
        { weapons.ShowFeedback("Bu su hattı zaten bağlı."); return false; }
        float length = Vector3.Distance(from.SocketPosition, to.SocketPosition);
        if (length > 15 || Vector3.Distance(transform.position, to.transform.position) > Reach) return false;
        int cost = Mathf.CeilToInt(length) * 2;
        if (!weapons.TrySpendMaterials(0, cost)) { weapons.ShowFeedback("Boru için " + cost + " plastik gerekiyor."); return false; }
        var go = new GameObject("Player water pipe"); var pipe = go.AddComponent<EcoWaterPipe>();
        pipe.from = from; pipe.to = to; pipe.fluid = fluid; pipe.playerBuilt = true; pipe.materialCost = cost;
        var line = go.GetComponent<LineRenderer>(); line.sharedMaterial = Resources.Load<Material>("EcoInfrastructure/WaterPipe");
        line.startWidth = line.endWidth = 0.09f; line.numCapVertices = 3; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        weapons.ShowFeedback("Su hattı bağlandı. Akış: " + from.DisplayName + " → " + to.DisplayName); return true;
    }
    public bool RemovePipe(EcoWaterNode from, EcoWaterNode to, EcoWaterNode.Fluid fluid)
    {
        if (!IsBuilding || !weapons.CanOperate) return false;
        var pipe = EcoWaterPipe.Active.FirstOrDefault(p => p != null && p.playerBuilt && p.from == from && p.to == to && p.fluid == fluid);
        if (pipe == null) { weapons.ShowFeedback("Önce çıkış ucunu seç, sonra giriş ucuna bakıp X kullan."); return false; }
        pipe.playerBuilt = false; pipe.connected = false; weapons.ReturnMaterials(0, pipe.materialCost / 2); Destroy(pipe.gameObject); pipeStart = null; return true;
    }
    private void OnDisable() => SetBuildMode(false);
    private void OnDestroy() { if (preview != null) Destroy(preview); if (previewMaterial != null) Destroy(previewMaterial); }
}
