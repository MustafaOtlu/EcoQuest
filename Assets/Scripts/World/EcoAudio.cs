using UnityEngine;
using UnityEngine.InputSystem;

// GDD §5/§15.2: asset-free procedural audio. Ambient layers follow the region under the player (wind, water, birds,
// industrial hum, rain) so the world audibly recovers as pollution falls; short one-shots confirm key events.
// [F2] opens a small settings panel (master volume, mute, ambient on/off). Settings persist in PlayerPrefs.
public sealed class EcoAudio : MonoBehaviour
{
    private const int Rate = 22050;
    private const string VolumeKey = "EcoQuest.volume", MuteKey = "EcoQuest.mute";
    public static float MasterVolume { get; private set; } = 0.7f;
    // GDD §13: separate buses. Music, ambience, effects and interface sounds each have their own slider.
    public static float MusicVolume { get; private set; } = 0.6f;
    public static float AmbientVolume { get; private set; } = 0.8f;
    public static float EffectsVolume { get; private set; } = 0.8f;
    public static float InterfaceVolume { get; private set; } = 0.6f;
    private const string MusicKey = "EcoQuest.vol.music", AmbientKey = "EcoQuest.vol.ambient", EffectsKey = "EcoQuest.vol.sfx", InterfaceKey = "EcoQuest.vol.ui";
    private AudioSource musicDark, musicBright;

    private AudioSource wind, water, rain, hum, oneShot;
    private AudioClip chirpA, chirpB, hit, chime, alarm, tick;
    private PlayerController player;
    private PlayerVitals vitals;
    private PlayerWeaponSystem weapons;
    private float lastEnergy = -1f, nextChirp, nextScan, nextHealth, health = 0.5f;
    private string lastFeedback = "";
    private int lastCompleted = -1;
    private bool bossSeen, panelOpen;
    private EcoRegion region;
    private GUIStyle title, body;

    private void Awake()
    {
        MasterVolume = PlayerPrefs.GetFloat(VolumeKey, 0.7f);
        MuteFlag = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, 0.6f); AmbientVolume = PlayerPrefs.GetFloat(AmbientKey, 0.8f);
        EffectsVolume = PlayerPrefs.GetFloat(EffectsKey, 0.8f); InterfaceVolume = PlayerPrefs.GetFloat(InterfaceKey, 0.6f);
        Apply();
        musicDark = Loop(Pad(new[] { 110f, 130.8f, 164.8f }, 8f), 0f);      // A minor: a damaged world
        musicBright = Loop(Pad(new[] { 130.8f, 164.8f, 196f, 246.9f }, 8f), 0f); // C major 7: a recovering world
        wind = Loop(Noise(2.5f, 0.06f, 0.9f), 0f);
        water = Loop(Noise(2.5f, 0.25f, 0.6f), 0f);
        rain = Loop(Noise(2.5f, 0.7f, 0.5f), 0f);
        hum = Loop(Hum(2f), 0f);
        oneShot = gameObject.AddComponent<AudioSource>(); oneShot.spatialBlend = 0f; oneShot.playOnAwake = false;
        chirpA = Chirp(2600f, 3600f, 0.12f); chirpB = Chirp(3400f, 2400f, 0.16f);
        hit = Thump(0.18f, 140f); chime = Chime(new[] { 523f, 659f, 784f }, 1.1f); alarm = Chime(new[] { 196f, 147f }, 1.3f);
        tick = Chirp(1400f, 1700f, 0.05f);
    }

    private void Apply() { AudioListener.volume = MuteFlag ? 0f : MasterVolume; }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && EcoKeys.Pressed(EcoKeys.Action.Settings)) panelOpen = !panelOpen;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && panelOpen) { EcoUIFlow.ConsumeEscape(); panelOpen = false; }
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.4f;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (vitals == null) vitals = FindFirstObjectByType<PlayerVitals>();
            if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
            region = player != null ? EcoRegion.At(player.transform.position) : null;
        }
        var clock = EcoWorldClock.Current;
        float day = clock != null ? Mathf.Clamp01(clock.SolarFactor) : 1f;
        float air = region != null ? region.airPollution / 100f : 0f;
        float green = region != null ? region.vegetation : 0.3f;
        bool lake = region != null && region.regionName.Contains("Göl");
        float k = Time.unscaledDeltaTime * 2f;
        float a = AmbientVolume;
        Fade(wind, (0.05f + 0.22f * (clock != null ? clock.windStrength : 0.4f)) * (1f - 0.4f * air) * a, k);
        Fade(water, (lake ? 0.16f : 0.015f) * a, k);
        Fade(rain, (clock != null ? clock.RainIntensity * 0.3f : 0f) * a, k);
        Fade(hum, air * 0.28f * a, k);
        // Music follows the average health of the world: bright chord when clean, dark when polluted.
        if (Time.unscaledTime >= nextHealth)
        {
            nextHealth = Time.unscaledTime + 2f; float sum = 0f; int count = 0;
            foreach (var r in FindObjectsByType<EcoRegion>(FindObjectsSortMode.None)) { sum += (r.airPollution + r.waterPollution) * 0.5f; count++; }
            health = count > 0 ? 1f - Mathf.Clamp01(sum / count / 60f) : 0.5f;
        }
        Fade(musicDark, 0.16f * MusicVolume * (1f - health), k);
        Fade(musicBright, 0.16f * MusicVolume * health, k);

        // Birds sing only in daylight, in green and clean places - the audible reward for restoring an area.
        if (Time.time >= nextChirp)
        {
            nextChirp = Time.time + Random.Range(1.5f, 5f);
            float chance = day * green * (1f - air) * (clock != null ? 1f - clock.RainIntensity : 1f);
            if (Random.value < chance) oneShot.PlayOneShot(Random.value < 0.5f ? chirpA : chirpB, 0.25f * EffectsVolume * AmbientVolume);
        }

        if (vitals != null)
        {
            if (lastEnergy >= 0f && vitals.Energy < lastEnergy - 0.5f) oneShot.PlayOneShot(hit, 0.6f * EffectsVolume);
            lastEnergy = vitals.Energy;
        }
        var projects = EcoProjects.Current;
        if (projects != null)
        {
            int done = projects.CompletedCount;
            if (lastCompleted >= 0 && done > lastCompleted) oneShot.PlayOneShot(chime, 0.7f * EffectsVolume);
            lastCompleted = done;
        }
        var finale = FindFirstObjectByType<EcoFinale>();
        if (finale != null)
        {
            if (finale.Fighting && !bossSeen) { bossSeen = true; oneShot.PlayOneShot(alarm, 0.8f * EffectsVolume); }
            if (!finale.Fighting) { if (bossSeen && finale.Won) oneShot.PlayOneShot(chime, 0.9f * EffectsVolume); bossSeen = false; }
        }
        if (weapons != null && weapons.Feedback != null && weapons.Feedback != lastFeedback && Time.time < weapons.FeedbackUntil)
        { lastFeedback = weapons.Feedback; oneShot.PlayOneShot(tick, 0.18f * InterfaceVolume); }
    }

    private static void Fade(AudioSource source, float target, float k) { source.volume = Mathf.MoveTowards(source.volume, target, k * 0.3f); }

    // ------------------------------------------------------------------ synthesis

    private AudioSource Loop(AudioClip clip, float volume)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip; source.loop = true; source.volume = volume; source.spatialBlend = 0f; source.playOnAwake = false; source.Play();
        return source;
    }

    // Low-pass filtered noise; 'cutoff' 0..1 (higher = brighter). Crossfaded ends make the loop seamless.
    private static AudioClip Noise(float seconds, float cutoff, float gain)
    {
        int n = (int)(seconds * Rate); var data = new float[n];
        var random = new System.Random(1234 + (int)(cutoff * 1000f)); float y = 0f;
        for (int i = 0; i < n; i++) { float x = (float)(random.NextDouble() * 2.0 - 1.0); y += (x - y) * cutoff; data[i] = y * gain; }
        int fade = Rate / 5;
        for (int i = 0; i < fade; i++) { float t = i / (float)fade; data[i] = Mathf.Lerp(data[n - fade + i], data[i], t); }
        var clip = AudioClip.Create("noise", n - fade, 1, Rate, false);
        var trimmed = new float[n - fade]; System.Array.Copy(data, trimmed, n - fade); clip.SetData(trimmed, 0);
        return clip;
    }

    // Slow evolving pad: summed sines with a gentle tremolo; whole periods keep the loop seamless.
    private static AudioClip Pad(float[] notes, float seconds)
    {
        int n = (int)(seconds * Rate); var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, v = 0f;
            for (int j = 0; j < notes.Length; j++)
            {
                float f = Mathf.Round(notes[j] * seconds) / seconds; // integer cycles over the loop
                v += Mathf.Sin(2f * Mathf.PI * f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (j + 1) / seconds * t));
            }
            data[i] = v / notes.Length * 0.6f;
        }
        var clip = AudioClip.Create("pad", n, 1, Rate, false); clip.SetData(data, 0); return clip;
    }

    private static AudioClip Hum(float seconds)
    {
        int n = (int)(seconds * Rate); var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            data[i] = (Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 165f * t) * 0.1f) * 0.5f;
        }
        var clip = AudioClip.Create("hum", n, 1, Rate, false); clip.SetData(data, 0); return clip; // whole periods: loops cleanly
    }

    private static AudioClip Chirp(float from, float to, float seconds)
    {
        int n = (int)(seconds * Rate); var data = new float[n]; float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n; float f = Mathf.Lerp(from, to, t);
            phase += 2f * Mathf.PI * f / Rate;
            data[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t) * 0.6f;
        }
        var clip = AudioClip.Create("chirp", n, 1, Rate, false); clip.SetData(data, 0); return clip;
    }

    private static AudioClip Thump(float seconds, float frequency)
    {
        int n = (int)(seconds * Rate); var data = new float[n]; var random = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n; float env = Mathf.Exp(-6f * t);
            data[i] = (Mathf.Sin(2f * Mathf.PI * frequency * (1f - 0.5f * t) * i / Rate) * 0.8f + (float)(random.NextDouble() * 2.0 - 1.0) * 0.25f) * env;
        }
        var clip = AudioClip.Create("hit", n, 1, Rate, false); clip.SetData(data, 0); return clip;
    }

    private static AudioClip Chime(float[] notes, float seconds)
    {
        int n = (int)(seconds * Rate); var data = new float[n];
        float step = seconds / (notes.Length + 1f);
        for (int j = 0; j < notes.Length; j++)
        {
            int start = (int)(j * step * Rate);
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)Rate;
                data[i] += (Mathf.Sin(2f * Mathf.PI * notes[j] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[j] * t)) * Mathf.Exp(-3.2f * t) * 0.28f;
            }
        }
        var clip = AudioClip.Create("chime", n, 1, Rate, false); clip.SetData(data, 0); return clip;
    }

    // ------------------------------------------------------------------ settings panel

    private static void SaveBus(string key, float value) { PlayerPrefs.SetFloat(key, value); }

    /// <summary>Draws the volume sliders (shared by the [F2] panel and the pause menu). Returns the height used.</summary>
    public static float DrawSliders(float x, float y, float width, GUIStyle label)
    {
        string[] names = { "Ana ses", "Müzik", "Ortam", "Efekt", "Arayüz" };
        float[] values = { MasterVolume, MusicVolume, AmbientVolume, EffectsVolume, InterfaceVolume };
        for (int i = 0; i < 5; i++)
        {
            GUI.Label(new Rect(x, y + i * 30f, 130f, 26f), names[i] + " %" + Mathf.RoundToInt(values[i] * 100f), label);
            float v = GUI.HorizontalSlider(new Rect(x + 140f, y + i * 30f + 8f, width - 140f, 16f), values[i], 0f, 1f);
            if (!Mathf.Approximately(v, values[i]))
            {
                if (i == 0) { MasterVolume = v; SaveBus(VolumeKey, v); AudioListener.volume = MuteFlag ? 0f : v; }
                else if (i == 1) { MusicVolume = v; SaveBus(MusicKey, v); }
                else if (i == 2) { AmbientVolume = v; SaveBus(AmbientKey, v); }
                else if (i == 3) { EffectsVolume = v; SaveBus(EffectsKey, v); }
                else { InterfaceVolume = v; SaveBus(InterfaceKey, v); }
            }
        }
        bool mute = GUI.Button(new Rect(x, y + 154f, width, 26f), (MuteFlag ? "✓ " : "✗ ") + "Sesi kapat") ? !MuteFlag : MuteFlag;
        if (mute != MuteFlag) { MuteFlag = mute; PlayerPrefs.SetInt(MuteKey, mute ? 1 : 0); AudioListener.volume = mute ? 0f : MasterVolume; }
        return 182f;
    }
    public static bool MuteFlag { get; private set; }

    /// <summary>Opens/closes the sound panel (also bound to [F2] and the HUD's gear button).</summary>
    public void TogglePanel() => panelOpen = !panelOpen;
    /// <summary>True while any sound panel is open (the uGUI HUD hides behind it).</summary>
    public static bool AnyPanelOpen { get; private set; }
    private void LateUpdate() => AnyPanelOpen = panelOpen;
    private void OnDisable() => AnyPanelOpen = false;

    private void OnGUI()
    {
        if (!panelOpen) return; // the HUD's quick-action column shows the [F2] settings button
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        }
        EcoTheme.Begin();
        var panel = EcoTheme.Window(520f, 390f, "SES AYARLARI", "Ana ses, müzik, ortam, efekt ve arayüz ayrı ayarlanır.", "ui_gear");
        DrawSliders(panel.x + 28f, panel.y + 100f, panel.width - 56f, EcoTheme.Body);
        if (EcoTheme.IconButton(new Rect(panel.x + 28f, panel.yMax - 58f, panel.width - 56f, 40f), "Kapat  " + EcoKeys.Tag(EcoKeys.Action.Settings) + " / [Esc]", "ui_close")) panelOpen = false;
        EcoTheme.End();
    }
}
