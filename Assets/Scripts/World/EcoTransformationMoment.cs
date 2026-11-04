using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// GDD v4 Ö4 – dönüşüm anı. When a main project finishes its 120 s verification, a short (≈5.5 s) moment plays:
//   1. the camera eases over to the project's place and pulls back a little,
//   2. light sparks rise from the ground and small birds/butterflies fly out, a rising four-note motif plays,
//   3. a card shows the real reading before → after ("Oksijen 3,8 → 7,4 mg/L"),
//   4. the child reacts in one sentence.
// The world runs in slow motion meanwhile (shared panel clock); any key or click skips it. Nothing plays during the
// finale or while another window is open: then only the card is shown. The "before" readings are taken once at the
// start of a new game and saved, so the card compares against where the player began.
public sealed class EcoTransformationMoment : MonoBehaviour
{
    public static EcoTransformationMoment Current { get; private set; }

    // ---- saved (public fields, EcoSaveIdentity)
    public float[] before = new float[6];
    public bool snapshotTaken;

    private const float Duration = 5.5f, SkipGrace = 0.4f;
    private static readonly string[] ChildLines =
    {
        "Işıklarımız artık güneşten ve rüzgârdan geliyor!",
        "Göl artık kokmuyor, balıklar geri geliyor!",
        "Ormandan yeniden kuş sesleri geliyor!",
        "Bacalardan kara duman çıkmıyor, nefes almak kolaylaştı!",
        "Rüzgâr dursa da güneş batsa da ışıklarımız sönmüyor!",
        "Buzul artık erimiyor, dağ yine beyaz!"
    };

    private EcoProjects projects;
    private bool playing, usesCamera, preview;
    private int index = -1;
    private float startedAt;
    private string line1 = "", line2 = "";
    private Transform proxy, previousTarget;
    private float previousZoom;
    private readonly List<Transform> props = new List<Transform>();
    private readonly List<Vector3> velocities = new List<Vector3>();
    private Material sparkMaterial, wingMaterialA, wingMaterialB;
    private AudioSource voice;
    private AudioClip motif;
    private float nextScan;

    private void Awake() { Current = this; }
    private void OnDestroy() { if (projects != null) projects.Completed -= OnCompleted; if (Current == this) Current = null; Finish(); }
    private void OnDisable() { if (playing) Finish(); }

    private void Update()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            if (projects == null && EcoProjects.Current != null) { projects = EcoProjects.Current; projects.Completed += OnCompleted; }
            if (!snapshotTaken && projects != null && FindFirstObjectByType<EcoRegion>() != null) TakeSnapshot();
        }
        if (!playing) return;
        float t = Time.unscaledTime - startedAt;
        AnimateProps(Time.unscaledDeltaTime, t);
        var keyboard = Keyboard.current; var mouse = Mouse.current;
        bool skip = t > SkipGrace && ((keyboard != null && keyboard.anyKey.wasPressedThisFrame) || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)));
        if (skip) EcoUIFlow.ConsumeEscape();
        if (t >= Duration || skip) Finish();
    }

    // ------------------------------------------------------------------ readings

    private static EcoRegion RegionOf(int project)
    {
        foreach (var info in FindObjectsByType<EcoRegionInfo>(FindObjectsSortMode.None))
            if (info != null && info.projectIndex == project) return info.GetComponent<EcoRegion>();
        return null;
    }

    /// <summary>The real-world reading that best shows the change a project makes.</summary>
    public static float Reading(int project)
    {
        var region = RegionOf(project);
        switch (project)
        {
            case 0: { float total = 0f; foreach (var s in FindObjectsByType<EcoChargingStation>(FindObjectsSortMode.None)) total += s.TotalDeliveredEnergy; return Mathf.Max(total, EcoProjects.Current != null ? EcoProjects.Current.ChargedEnergy : 0f); }
            case 1: return region != null ? region.DissolvedOxygen : 0f;
            case 2: return region != null ? region.vegetation * 100f : 0f;
            case 3: return region != null ? region.AQI : 0f;
            case 4:
            {
                float reserve = 0f;
                foreach (var n in FindObjectsByType<EcoPowerNode>(FindObjectsSortMode.None)) if (n != null && n.kind == EcoPowerNode.Kind.Battery) reserve += n.storedEnergy;
                return reserve;
            }
            case 5: return EcoRegion.WorldCarbon();
            default: return 0f;
        }
    }

    private void TakeSnapshot()
    {
        if (before == null || before.Length != 6) before = new float[6];
        for (int i = 0; i < 6; i++) before[i] = Reading(i);
        snapshotTaken = true;
    }

    // "a → b" when the reading moved; otherwise only the current value, so a preview never shows a fake change.
    private static string Change(float a, float b, string format, float threshold) => Mathf.Abs(b - a) >= threshold ? a.ToString(format) + " → " + b.ToString(format) : "şu an " + b.ToString(format);

    private string Describe(int project, out string extra)
    {
        float a = before != null && before.Length == 6 ? before[project] : 0f, b = Reading(project);
        var region = RegionOf(project);
        extra = "";
        switch (project)
        {
            case 0:
                return "Mahalle ağından şarj edilen enerji " + Change(a, b, "0", 1f) + " EB · üretim, depolama ve tüketim artık tek ağda";
            case 1:
                extra = region != null ? "Su kalitesi " + Mathf.RoundToInt(100f - region.waterPollution) + "/100 · balıklar için gereken 4 mg/L'nin " + (b >= 4f ? "üstünde" : "altında") : "";
                return "Göldeki çözünmüş oksijen " + Change(a, b, "0.0", 0.1f) + " mg/L";
            case 2:
            {
                var habitat = region != null ? region.GetComponent<EcoHabitat>() : null;
                extra = habitat != null ? "Ormanda " + habitat.Birds + " kuş sürüsü ve " + habitat.Butterflies + " kelebek grubu" : "";
                return "Bitki örtüsü (%) " + Change(a, b, "0", 1f);
            }
            case 3:
                extra = "Filtre havayı, temiz enerji dönüşümü karbonu azalttı";
                return "Fabrikalar'da hava kalitesi (AQI) " + Change(a, b, "0", 1f) + " · " + (Mathf.Abs(b - a) >= 1f ? EcoRegion.AqiName(Mathf.RoundToInt(a)).ToLowerInvariant() + " → " : "") + EcoRegion.AqiName(Mathf.RoundToInt(b)).ToLowerInvariant();
            case 4:
                return "Bataryalarda yedek enerji " + Change(a, b, "0", 1f) + " EB · bir kaynak dursa da hizmet sürüyor";
            case 5:
                extra = "CO₂ ≈ " + Change(425f + a * 2.5f, 425f + b * 2.5f, "0", 2.5f) + " ppm";
                return "Dünya karbonu " + Change(a, b, "0", 1f) + " /100 · buzulun erimesi durdu";
            default: return "";
        }
    }

    private Vector3 PlaceOf(int project)
    {
        if (project == 0)
        {
            foreach (var s in FindObjectsByType<EcoChargingStation>(FindObjectsSortMode.None)) if (s != null) return s.transform.position;
        }
        if (project == 5 && EcoGlacierStation.Current != null) return EcoGlacierStation.Current.transform.position;
        if (project == 4 && EcoHomeSaving.Current != null && EcoHomeSaving.Current.Meter != null) return EcoHomeSaving.Current.Meter.position;
        var region = RegionOf(project);
        return region != null ? region.transform.position : Vector3.zero;
    }

    // ------------------------------------------------------------------ the moment

    private void OnCompleted(int project)
    {
        if (project < 0 || project > 5) return;
        if (playing) Finish();
        index = project; preview = false;
        line1 = Describe(project, out line2);
        startedAt = Time.unscaledTime; playing = true;
        var finale = EcoFinale.Current;
        usesCamera = !PlayerController.Paused && !EcoUIFlow.IsOpen && (finale == null || !finale.Fighting) && EcoIsoCamera.Current != null;
        Vector3 place = PlaceOf(project);
        if (usesCamera)
        {
            var cam = EcoIsoCamera.Current;
            proxy = new GameObject("Dönüşüm odağı").transform;
            proxy.position = place;
            previousTarget = cam.target; previousZoom = cam.distance;
            cam.target = proxy; cam.SetZoom(Mathf.Min(cam.maxDistance, 8.5f));
            EcoUIFlow.SetPanel(this, true); // shared slow-motion clock; gameplay input waits
        }
        SpawnProps(place);
        PlayMotif();
        EcoPlaytestLog.Objective("Dönüşüm anı: " + (projects != null && project < projects.All.Count ? projects.All[project].name : project.ToString()));
    }

    private void Finish()
    {
        if (!playing && proxy == null && props.Count == 0) return;
        playing = false;
        var cam = EcoIsoCamera.Current;
        if (cam != null && proxy != null && cam.target == proxy) { cam.target = previousTarget; cam.SetZoom(previousZoom); }
        if (proxy != null) Destroy(proxy.gameObject);
        proxy = null;
        if (EcoUIFlow.IsPanel(this)) EcoUIFlow.SetPanel(this, false);
        foreach (var p in props) if (p != null) Destroy(p.gameObject);
        props.Clear(); velocities.Clear();
    }

    // Sparks rise from a ring; small two-winged fliers (birds over water/forest, butterflies elsewhere) fly out.
    private void SpawnProps(Vector3 centre)
    {
        var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (unlit == null) return;
        Material Paint(Color c) { var m = new Material(unlit); m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); return m; }
        sparkMaterial ??= Paint(new Color(0.72f, 1f, 0.55f));
        wingMaterialA ??= Paint(new Color(1f, 0.82f, 0.3f));
        wingMaterialB ??= Paint(new Color(0.55f, 0.82f, 1f));
        float ground = centre.y;
        if (Physics.Raycast(centre + Vector3.up * 40f, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
        for (int i = 0; i < 40; i++)
        {
            float angle = i / 40f * Mathf.PI * 2f, radius = Random.Range(2f, 7f);
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(spark.GetComponent<Collider>());
            spark.name = "Dönüşüm kıvılcımı";
            spark.transform.position = new Vector3(centre.x + Mathf.Cos(angle) * radius, ground + 0.2f, centre.z + Mathf.Sin(angle) * radius);
            spark.transform.localScale = Vector3.one * Random.Range(0.2f, 0.34f);
            spark.GetComponent<Renderer>().sharedMaterial = sparkMaterial;
            props.Add(spark.transform); velocities.Add(new Vector3(0f, Random.Range(1.2f, 2.6f), 0f));
        }
        bool birds = index == 1 || index == 2 || index == 5;
        for (int i = 0; i < 12; i++)
        {
            var flier = new GameObject(birds ? "Dönüşüm kuşu" : "Dönüşüm kelebeği").transform;
            flier.position = new Vector3(centre.x + Random.Range(-1.5f, 1.5f), ground + 1.2f, centre.z + Random.Range(-1.5f, 1.5f));
            for (int side = -1; side <= 1; side += 2)
            {
                var wing = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(wing.GetComponent<Collider>());
                wing.transform.SetParent(flier, false);
                wing.transform.localPosition = new Vector3(side * 0.16f, 0f, 0f);
                wing.transform.localScale = birds ? new Vector3(0.3f, 0.03f, 0.12f) : new Vector3(0.18f, 0.02f, 0.16f);
                wing.GetComponent<Renderer>().sharedMaterial = birds ? wingMaterialB : wingMaterialA;
            }
            float a = Random.Range(0f, Mathf.PI * 2f);
            props.Add(flier); velocities.Add(new Vector3(Mathf.Cos(a), Random.Range(0.3f, 0.8f), Mathf.Sin(a)) * Random.Range(2f, 3.5f));
        }
    }

    private void AnimateProps(float dt, float t)
    {
        float shrink = Mathf.Clamp01((Duration - t) / 1.2f);
        for (int i = 0; i < props.Count; i++)
        {
            var p = props[i]; if (p == null) continue;
            p.position += velocities[i] * dt;
            if (p.childCount > 0)
            {
                // Wing flap and a gentle turn towards the direction of flight.
                float flap = Mathf.Sin((t + i) * 18f) * 40f;
                if (p.childCount >= 2) { p.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, flap); p.GetChild(1).localRotation = Quaternion.Euler(0f, 0f, -flap); }
                var flat = new Vector3(velocities[i].x, 0f, velocities[i].z);
                if (flat.sqrMagnitude > 0.01f) p.rotation = Quaternion.LookRotation(flat);
                p.localScale = Vector3.one * (1.7f * Mathf.Max(0.01f, shrink));
            }
            else p.localScale = Vector3.one * (0.28f * Mathf.Max(0.01f, shrink));
        }
    }

    private void PlayMotif()
    {
        if (EcoAudio.MuteFlag) return;
        if (motif == null)
        {
            const int rate = 22050; float[] notes = { 392f, 523.25f, 659.25f, 783.99f }; float step = 0.22f, tail = 1.1f;
            int n = Mathf.CeilToInt((step * notes.Length + tail) * rate);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.RoundToInt(k * step * rate), length = Mathf.RoundToInt((k == notes.Length - 1 ? tail + step : step * 2.2f) * rate);
                for (int s = 0; s < length && start + s < n; s++)
                {
                    float time = s / (float)rate, env = Mathf.Min(1f, time * 60f) * Mathf.Exp(-time * 3.2f);
                    data[start + s] += 0.22f * env * (Mathf.Sin(2f * Mathf.PI * notes[k] * time) + 0.35f * Mathf.Sin(4f * Mathf.PI * notes[k] * time));
                }
            }
            motif = AudioClip.Create("dönüşüm motifi", n, 1, rate, false); motif.SetData(data, 0);
        }
        if (voice == null) { voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0f; voice.ignoreListenerPause = true; }
        voice.PlayOneShot(motif, EcoAudio.MasterVolume * Mathf.Max(EcoAudio.MusicVolume, 0.3f));
    }

    // ------------------------------------------------------------------ card

    private GUIStyle big, small;
    private void OnGUI()
    {
        if (!playing || index < 0) return;
        float t = Time.unscaledTime - startedAt;
        float alpha = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((Duration - t) / 0.5f);
        EcoTheme.Begin();
        var prev = GUI.color; GUI.color = new Color(1f, 1f, 1f, alpha);
        big ??= new GUIStyle(EcoTheme.Subtitle) { fontSize = 26, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        small ??= new GUIStyle(EcoTheme.Body) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        float width = Mathf.Min(760f, EcoTheme.Width - 40f);
        string title = (preview ? "ÖNİZLEME · " : "DÖNÜŞÜM · ") + (projects != null && index < projects.All.Count ? EcoUI.Upper(projects.All[index].name) : "");
        string child = "Çocuk: \"" + ChildLines[index] + "\"";
        float h1 = small.CalcHeight(new GUIContent(line1), width - 60f), h2 = line2.Length > 0 ? small.CalcHeight(new GUIContent(line2), width - 60f) : 0f;
        float h3 = small.CalcHeight(new GUIContent(child), width - 60f);
        float height = 30f + 36f + 10f + h1 + (h2 > 0f ? 4f + h2 : 0f) + 12f + h3 + 30f + 26f;
        // Lower third: the place itself stays visible in the middle of the screen.
        var box = new Rect((EcoTheme.Width - width) * 0.5f, Mathf.Max(110f, EcoTheme.Height - height - 190f), width, height);
        EcoTheme.Panel(box);
        float y = box.y + 28f;
        GUI.Label(new Rect(box.x + 30f, y, width - 60f, 36f), title, big); y += 46f;
        GUI.Label(new Rect(box.x + 30f, y, width - 60f, h1), line1, small); y += h1;
        if (h2 > 0f) { y += 4f; GUI.Label(new Rect(box.x + 30f, y, width - 60f, h2), line2, EcoTheme.Muted); y += h2; }
        y += 12f;
        GUI.Label(new Rect(box.x + 30f, y, width - 60f, h3), child, small); y += h3 + 8f;
        var hint = new GUIStyle(EcoTheme.Muted) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(box.x + 30f, y, width - 60f, 22f), "Geçmek için herhangi bir tuşa bas", hint);
        GUI.color = prev;
        EcoTheme.End();
    }

    /// <summary>For tests and the expert demo: play the moment of a project as if it had just finished.</summary>
    public void Preview(int project) { OnCompleted(project); preview = true; }
    public bool Playing => playing;
}
