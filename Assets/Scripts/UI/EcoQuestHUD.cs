using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(40)]
public sealed class EcoQuestHUD : MonoBehaviour
{
    public PlayerWeaponSystem weapons;
    [Header("Optional imported UI artwork")]
    public Sprite panelSprite;
    public Sprite slotSprite;
    public Sprite[] toolIcons = new Sprite[5];
    private PlayerVitals vitals;
    private Canvas canvas;
    private Font font;
    private Image[] slots, fills;
    private RectTransform[] slotRects;
    private Text[] slotCounts, barLabels, slotTitles;
    private Text mode, inventory, context, feedback, instructions, dead, objective;
    private float refreshAt;
    private EcoWorldClock worldClock;
    private Text weather;
    private readonly Color ink = new Color(0.055f, 0.09f, 0.08f, 0.91f);
    private readonly Color cream = new Color(0.94f, 0.94f, 0.84f);
    private readonly string[] names = { "VAKUM", "TOHUM", "GERİ DÖNÜŞÜM", "TARAYICI", "ELLER" };

    private void Start()
    {
        if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        vitals = weapons != null ? weapons.GetComponentInParent<PlayerVitals>() : null;
        if (vitals != null) vitals.showLegacyHUD = false;
        worldClock = FindFirstObjectByType<EcoWorldClock>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = new GameObject("EcoQuest HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        // Keep the overlay at scene root so camera/weapon sway cannot rotate or scale the HUD.
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20; canvas.pixelPerfect = true;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = 0.5f;
        var left = Panel("Vitals", root.transform, new Vector2(0,1), new Vector2(24,-24), new Vector2(330,184), new Vector2(0,1));
        Label(left, "ECOQUEST", new Vector2(14,-10), new Vector2(300,28), 21, TextAnchor.MiddleLeft);
        fills = new Image[3]; barLabels = new Text[3];
        string[] labels = { "KARAKTER ENERJİSİ", "SİLAH ELEKTRİĞİ", "SU" };
        Color[] colors = { new Color(0.22f,0.65f,0.42f), new Color(0.8f,0.76f,0.24f), new Color(0.23f,0.65f,0.85f) };
        for(int i=0;i<3;i++)
        {
            var bar = Panel(labels[i],left,new Vector2(0,1),new Vector2(14,-46-i*32),new Vector2(300,25),new Vector2(0,1));
            bar.GetComponent<Image>().color = new Color(0.12f,0.16f,0.14f,1);
            fills[i] = Panel("Fill",bar,new Vector2(0,1),Vector2.zero,new Vector2(300,25),new Vector2(0,1)).GetComponent<Image>();
            fills[i].color=colors[i];
            barLabels[i]=Label(bar,labels[i],new Vector2(8,0),new Vector2(284,25),15,TextAnchor.MiddleLeft);
        }
        weather=Label(left,"",new Vector2(14,-143),new Vector2(300,27),16,TextAnchor.MiddleLeft);
        var bag = Panel("Materials",root.transform,new Vector2(1,1),new Vector2(-24,-24),new Vector2(330,205),new Vector2(1,1));
        Label(bag,"KAYNAKLAR",new Vector2(14,-10),new Vector2(274,28),20,TextAnchor.MiddleLeft);
        inventory=Label(bag,"",new Vector2(14,-43),new Vector2(302,150),18,TextAnchor.UpperLeft);
        var objectivePanel=Panel("Objective",root.transform,new Vector2(0.5f,1),new Vector2(0,-24),new Vector2(520,44),new Vector2(0.5f,1));
        objective=Label(objectivePanel,"",new Vector2(10,-5),new Vector2(500,34),18,TextAnchor.MiddleCenter);
        var dock=Panel("Toolbelt",root.transform,new Vector2(0.5f,0),new Vector2(0,24),new Vector2(824,112),new Vector2(0.5f,0));
        slots=new Image[5];slotRects=new RectTransform[5];slotCounts=new Text[5];slotTitles=new Text[5];
        for(int i=0;i<5;i++)
        {
            var slot=Panel(names[i],dock,new Vector2(0,1),new Vector2(12+i*162,-10),new Vector2(152,92),new Vector2(0,1));
            slotRects[i]=slot;slots[i]=slot.GetComponent<Image>();slots[i].sprite=slotSprite;
            Label(slot,(i+1).ToString(),new Vector2(10,-3),new Vector2(24,24),17,TextAnchor.MiddleLeft);
            slotTitles[i]=Label(slot,names[i],new Vector2(6,-31),new Vector2(140,24),i==2?14:17,TextAnchor.MiddleCenter);
            slotCounts[i]=Label(slot,"",new Vector2(6,-60),new Vector2(140,24),15,TextAnchor.MiddleCenter);
            if(i<toolIcons.Length && toolIcons[i]!=null)
            {
                var icon=Panel("Icon",slot,new Vector2(1,1),new Vector2(-10,-4),new Vector2(27,27),new Vector2(1,1)).GetComponent<Image>();
                icon.sprite=toolIcons[i];icon.color=Color.white;icon.preserveAspect=true;
            }
        }
        var modePanel=Panel("Mode",root.transform,new Vector2(0.5f,0),new Vector2(0,145),new Vector2(760,68),new Vector2(0.5f,0));
        mode=Label(modePanel,"",new Vector2(12,-4),new Vector2(736,29),21,TextAnchor.MiddleCenter);
        instructions=Label(modePanel,"",new Vector2(12,-34),new Vector2(736,25),16,TextAnchor.MiddleCenter);
        var hint=Panel("Context",root.transform,new Vector2(0.5f,0.5f),new Vector2(0,-108),new Vector2(640,72),new Vector2(0.5f,0.5f));
        hint.GetComponent<Image>().color=new Color(0,0,0,0);
        context=Label(hint,"",Vector2.zero,new Vector2(640,72),20,TextAnchor.MiddleCenter);
        var note=Panel("Feedback",root.transform,new Vector2(0.5f,1),new Vector2(0,-82),new Vector2(660,55),new Vector2(0.5f,1));
        note.GetComponent<Image>().color=new Color(0,0,0,0);
        feedback=Label(note,"",Vector2.zero,new Vector2(660,55),22,TextAnchor.MiddleCenter);
        var reticle=Panel("Aim",root.transform,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(30,30),new Vector2(0.5f,0.5f));
        reticle.GetComponent<Image>().color=Color.clear;
        Label(reticle,"+",Vector2.zero,new Vector2(30,30),24,TextAnchor.MiddleCenter);
        var death=Panel("Recovery",root.transform,new Vector2(0.5f,0.5f),new Vector2(0,60),new Vector2(500,50),new Vector2(0.5f,0.5f));
        death.GetComponent<Image>().color=Color.clear;dead=Label(death,"",Vector2.zero,new Vector2(500,50),26,TextAnchor.MiddleCenter);
        Refresh();
    }
    private void Update()
    {
        if(weapons==null || canvas==null)return;
        for(int i=0;i<5;i++)
        {
            bool selected=(weapons.Builder!=null&&weapons.Builder.IsBuilding?weapons.Builder.Selection%5:(int)weapons.Selected)==i;
            slots[i].color=selected?new Color(0.22f,0.43f,0.28f,1):new Color(0.12f,0.18f,0.15f,0.95f);
            slotRects[i].localScale=Vector3.one;
        }
        if(Time.unscaledTime>=refreshAt){refreshAt=Time.unscaledTime+0.1f;Refresh();}
    }
    private void Refresh()
    {
        if(weapons==null)return;
        if(worldClock!=null) weather.text="Gün "+worldClock.day+" • "+Mathf.FloorToInt(worldClock.hour).ToString("00")+":"
            + Mathf.FloorToInt(worldClock.hour%1f*60f).ToString("00")+" • Rüzgâr %"+Mathf.RoundToInt(worldClock.windStrength*100);
        float[] values={vitals!=null?vitals.Energy:100,weapons.Energy,weapons.Water};
        float[] maxima={vitals!=null?vitals.maximumEnergy:100,weapons.maximumEnergy,weapons.maximumWater};
        string[] labels={"KARAKTER ENERJİSİ","SİLAH ELEKTRİĞİ",weapons.HasDirtyWater?"KİRLİ SU":"TEMİZ SU"};
        for(int i=0;i<3;i++)
        { fills[i].rectTransform.sizeDelta=new Vector2(300*Mathf.Clamp01(values[i]/Mathf.Max(1,maxima[i])),25);barLabels[i].text=labels[i]+"   "+Mathf.CeilToInt(values[i])+" / "+maxima[i]; }
        inventory.text="Metal  "+weapons.Metal+"     Plastik  "+weapons.Plastic+"\nÜrün  "+weapons.Food+"     Gübre  "+weapons.Compost
            +"\nHam atık  "+(weapons.CollectedMetalScrap+weapons.CollectedPlasticScrap)+"     Organik  "+weapons.CollectedOrganicWaste
            + (weapons.Progression!=null ? "\nYEP • Seviye "+weapons.Progression.Level+"\n"+(weapons.Progression.Level==EcoProgression.MaximumLevel?"En yüksek seviye":weapons.Progression.LevelProgress+" / "+weapons.Progression.LevelRequirement) : "");
        slotCounts[0].text=Mathf.CeilToInt(weapons.Water)+" su";slotCounts[1].text=weapons.Seeds+" tohum / "+weapons.MetalAmmo+" top";
        slotCounts[2].text=(weapons.CollectedMetalScrap+weapons.CollectedPlasticScrap+weapons.CollectedOrganicWaste)+" atık";slotCounts[3].text="Analiz";slotCounts[4].text="Etkileşim";
        string[] modes={"Toplama","Anti-vakum","Su"};
        mode.text=names[(int)weapons.Selected]+(weapons.Selected==PlayerWeaponSystem.Tool.Vacuum?"  •  "+modes[(int)weapons.Mode]:weapons.Selected==PlayerWeaponSystem.Tool.SeedGun?"  •  "+(weapons.MetalMode?"Demir top":"Tohum ekimi"):"");
        instructions.text=weapons.Selected==PlayerWeaponSystem.Tool.Hands?"[E] Etkileşim   •   [1–5 / Tekerlek] Silah seç"
            :weapons.Selected==PlayerWeaponSystem.Tool.Scanner?"[Sol fare] Tara   •   [1–5 / Tekerlek] Silah seç"
            :weapons.Selected==PlayerWeaponSystem.Tool.Recycler?"[Sol fare] Geri dönüştür   •   [R] Atıkları işle / gübrele"
            :"[Sol fare] Kullan   •   [Q] Mod değiştir"+(weapons.Selected==PlayerWeaponSystem.Tool.SeedGun&&weapons.MetalMode?"   •   [R] Mermi hazırla":"");
        context.text=weapons.Selected==PlayerWeaponSystem.Tool.Scanner&&!string.IsNullOrEmpty(weapons.LastScan)?weapons.LastScan:weapons.ContextHint();
        bool building=weapons.Builder!=null&&weapons.Builder.IsBuilding;
        for(int i=0;i<5;i++)
        {
            slotTitles[i].text=building?weapons.Builder.SlotName(i):names[i];
            if(building)slotCounts[i].text=weapons.Builder.SlotCost(i);
        }
        if(building)
        {
            mode.text="İNŞA • "+weapons.Builder.SelectedName+" • "+(weapons.Builder.Page+1)+"/"+weapons.Builder.PageCount;
            instructions.text="[Tab] Sayfa   •   [1–5] Seç   •   [Sol tık] Kur   •   [R] Döndür   •   [B] Çık";
            context.text=weapons.Builder.Status;
        }
        else instructions.text+="   •   [B] İnşa";
        context.transform.parent.GetComponent<Image>().color=string.IsNullOrEmpty(context.text)?Color.clear:new Color(0.02f,0.04f,0.03f,0.8f);
        feedback.text=weapons.ChargingStation!=null
            ? (weapons.ChargingStation.GetComponent<EcoPowerNode>().DeliveredPower>0f?"Silah bataryası şarj oluyor…":"Şarj bağlı • Elektrik bekleniyor…")
            : Time.time<weapons.FeedbackUntil?weapons.Feedback:"";
        dead.text=vitals!=null&&vitals.IsRecovering?"Toparlanıyorsun… "+vitals.RecoveryRemaining.ToString("0.0")+" sn":"";
        var enemies=FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
        int alive=0;
        for(int i=0;i<enemies.Length;i++) if(enemies[i]!=null && enemies[i].State!=EnemyBrain.Behaviour.Dead && enemies[i].Health>0f) alive++;
        if (alive > 0) objective.text="Alanı temizle • Kalan düşman: "+alive;
        else
        {
            var crops=FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None);
            bool mature=false, living=false;
            for(int i=0;i<crops.Length;i++)
            {
                if(crops[i]==null || crops[i].IsWithered) continue;
                living=true; if(crops[i].IsMature) mature=true;
            }
            objective.text=weapons.Food>0 ? "Görev tamamlandı • Ekosistemi koru"
                : !living ? "Sıradaki hedef • Tohum ek"
                : mature ? "Sıradaki hedef • Olgun bitkiyi hasat et"
                : "Sıradaki hedef • Bitkini vakumun su moduyla sula";
        }
    }
    private RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Vector2 pivot)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=5;
        var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;
        var image=go.GetComponent<Image>();image.sprite=panelSprite;image.type=Image.Type.Sliced;image.color=ink;image.raycastTarget=false;
        return rect;
    }
    private Text Label(Transform parent,string content,Vector2 position,Vector2 size,int fontSize,TextAnchor align)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.layer=5;
        var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;
        // Rasterize at 4x resolution and display at the intended UI size.
        const int rasterScale = 4;
        rect.sizeDelta = size * rasterScale; rect.localScale = Vector3.one / rasterScale;
        var text=go.GetComponent<Text>();text.font=font;text.fontSize=Mathf.Max(14,fontSize)*rasterScale;text.color=cream;text.alignment=align;text.text=content;text.raycastTarget=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Overflow;
        return text;
    }
    private void OnDestroy(){if(canvas!=null)Destroy(canvas.gameObject);if(vitals!=null)vitals.showLegacyHUD=true;}
}
