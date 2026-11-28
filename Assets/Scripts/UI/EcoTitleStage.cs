using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The 3D set behind the title menu (GDD v5 §21, "Ana menü"): a forest clearing far away from the valley, the
/// player's own character holding the vacuum and turning it towards the cursor, and a camera that keeps the
/// character sharp while the trees behind are softly blurred. The vacuum draws dust and leaves into its mouth or
/// blows them out; every motion uses unscaled time because the world is paused while the menu is open.
/// Everything here is built at runtime and torn down again (cameras, lights, fog and ambient light are restored).
/// </summary>
[DefaultExecutionOrder(1000)] // after the Animator and the atmosphere scripts, so the pose and the lighting stick
public sealed class EcoTitleStage : MonoBehaviour
{
    private static readonly Vector3 Origin = new Vector3(4000f, 0f, 4000f);
    private static readonly Color Sky = new Color32(196, 226, 206, 255);

    public Camera Cam { get; private set; }

    private readonly List<Object> owned = new List<Object>();
    private readonly List<Behaviour> disabled = new List<Behaviour>();
    private Transform root, hero, tool;
    private EcoCatalogueToolVisual vacuum;
    private Animator animator;
    private Transform rightArm, rightForearm, rightHand, leftArm, leftForearm, leftHand, chest, rightMiddle, leftMiddle;
    private float height = 1.8f, toolScale = 1f, yaw, kick, work;
    private Vector3 baseForward, aimPoint, smoothedAim;
    private ParticleSystem specks, streaks;
    private float emitDebt;

    // What the menu changed in the scene, put back on Close.
    private bool fog; private AmbientMode ambientMode; private Color ambientSky, ambientEquator, ambientGround, ambientLight;

    public Vector3 Muzzle => vacuum != null && vacuum.Muzzle != null ? vacuum.Muzzle.position : tool != null ? tool.position : Origin + Vector3.up;
    public Vector2 MuzzleScreen => Cam != null ? (Vector2)Cam.WorldToScreenPoint(Muzzle) : Vector2.zero;

    public static EcoTitleStage Build()
    {
        var go = new GameObject("Title stage");
        var stage = go.AddComponent<EcoTitleStage>();
        stage.root = go.transform; go.transform.position = Origin;
        stage.Setup();
        return stage;
    }

    private void Setup()
    {
        // The valley's own cameras and sun step aside while the menu is up.
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (cam.enabled && cam.targetTexture == null) { cam.enabled = false; disabled.Add(cam); }
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.enabled && light.type == LightType.Directional) { light.enabled = false; disabled.Add(light); }
        fog = RenderSettings.fog; ambientMode = RenderSettings.ambientMode; ambientSky = RenderSettings.ambientSkyColor;
        ambientEquator = RenderSettings.ambientEquatorColor; ambientGround = RenderSettings.ambientGroundColor; ambientLight = RenderSettings.ambientLight;

        var camGo = new GameObject("Title camera"); camGo.transform.SetParent(root, false);
        Cam = camGo.AddComponent<Camera>();
        Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = Sky;
        Cam.fieldOfView = 30f; Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 140f; Cam.depth = 50f;
        var data = Cam.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        camGo.transform.localPosition = new Vector3(-1.15f, 1.15f, -5.4f);
        camGo.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);

        var sun = new GameObject("Title sun").AddComponent<Light>(); sun.transform.SetParent(root, false);
        sun.type = LightType.Directional; sun.color = new Color(1f, 0.95f, 0.84f); sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

        // Soft blur on everything behind the character (URP Gaussian depth of field), a light vignette.
        var volumeGo = new GameObject("Title blur"); volumeGo.transform.SetParent(root, false);
        var volume = volumeGo.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 500f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile);
        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(6.5f); dof.gaussianEnd.Override(16f);
        dof.gaussianMaxRadius.Override(1.4f); dof.highQualitySampling.Override(true);
        var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.28f); vignette.smoothness.Override(0.5f);
        volume.sharedProfile = profile;

        BuildClearing();
        BuildHero();
        BuildParticles();
        ApplyLighting();
    }

    private void ApplyLighting()
    {
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.62f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.34f, 0.22f);
    }

    // ------------------------------------------------------------------ set

    private void BuildClearing()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Title ground";
        Destroy(ground.GetComponent<Collider>());
        ground.transform.SetParent(root, false); ground.transform.localScale = new Vector3(14f, 1f, 14f);
        var turf = Resources.Load<Material>("EcoArt/Turf");
        if (turf != null) ground.GetComponent<MeshRenderer>().sharedMaterial = turf;
        else ground.GetComponent<MeshRenderer>().material.color = new Color(0.32f, 0.5f, 0.24f);

        // A pale green haze behind the farthest trees instead of an empty sky.
        var haze = GameObject.CreatePrimitive(PrimitiveType.Quad); haze.name = "Title haze";
        Destroy(haze.GetComponent<Collider>());
        haze.transform.SetParent(root, false); haze.transform.localPosition = new Vector3(0f, 14f, 60f); haze.transform.localScale = new Vector3(160f, 40f, 1f);
        var hazeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave }; owned.Add(hazeMat);
        hazeMat.SetTexture("_BaseMap", HazeTexture()); hazeMat.SetColor("_BaseColor", Color.white);
        haze.GetComponent<MeshRenderer>().sharedMaterial = hazeMat;

        var hold = new GameObject("Title props (building)"); hold.SetActive(false); hold.transform.SetParent(root, false);
        var random = new System.Random(4021);
        float R() => (float)random.NextDouble();
        var tree = Resources.Load<GameObject>("EcoArt/Tree");
        var grass = Resources.Load<GameObject>("EcoArt/Grass");
        var patch = Resources.Load<GameObject>("EcoArt/GrassPatch");
        var rock = Resources.Load<GameObject>("EcoArt/Rock");
        // Trees in loose rows behind and beside the character, none in front of it.
        for (int i = 0; i < 70; i++)
        {
            float z = Mathf.Lerp(2.5f, 42f, Mathf.Pow(R(), 0.8f));
            float spread = 6f + z * 0.9f;
            float x = (R() * 2f - 1f) * spread;
            if (z < 6f && x > -4.5f && x < 3.5f) continue;
            Place(tree, hold.transform, new Vector3(x, 0f, z), R() * 360f, Mathf.Lerp(0.85f, 1.5f, R()));
        }
        for (int i = 0; i < 90; i++)
        {
            var p = new Vector3((R() * 2f - 1f) * 10f, 0f, Mathf.Lerp(-3f, 12f, R()));
            if (Mathf.Abs(p.x) < 0.7f && Mathf.Abs(p.z) < 0.7f) continue;
            Place(R() < 0.5f ? grass : patch, hold.transform, p, R() * 360f, Mathf.Lerp(0.8f, 1.4f, R()));
        }
        for (int i = 0; i < 9; i++)
            Place(rock, hold.transform, new Vector3((R() * 2f - 1f) * 9f, 0f, Mathf.Lerp(1f, 14f, R())), R() * 360f, Mathf.Lerp(0.6f, 1.3f, R()));
        hold.name = "Title props"; hold.SetActive(true);
    }

    /// <summary>A prefab copy with only its looks: scripts, colliders and lights are removed before it wakes up.</summary>
    private static void Place(GameObject prefab, Transform hold, Vector3 local, float spin, float scale)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, hold);
        go.transform.localPosition = local; go.transform.localRotation = Quaternion.Euler(0f, spin, 0f); go.transform.localScale *= scale;
        Strip(go);
    }

    private static void Strip(GameObject go)
    {
        foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(behaviour);
        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
        foreach (var body in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);
        foreach (var light in go.GetComponentsInChildren<Light>(true)) DestroyImmediate(light);
        foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(audio);
    }

    private Texture2D HazeTexture()
    {
        var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave }; owned.Add(tex);
        var top = new Color(0.78f, 0.9f, 0.86f); var bottom = new Color(0.45f, 0.62f, 0.4f);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, y / 63f)));
        tex.Apply(false, false);
        return tex;
    }

    // ------------------------------------------------------------------ character

    private void BuildHero()
    {
        var player = FindFirstObjectByType<PlayerController>();
        var source = player != null ? (player.characterAnimator != null ? player.characterAnimator : player.GetComponentInChildren<Animator>(true)) : null;
        var hold = new GameObject("Title hero (building)"); hold.SetActive(false); hold.transform.SetParent(root, false);
        if (source != null)
        {
            var bounds = new Bounds(source.transform.position, Vector3.zero); bool any = false;
            foreach (var r in source.GetComponentsInChildren<SkinnedMeshRenderer>()) { if (!any) bounds = r.bounds; else bounds.Encapsulate(r.bounds); any = true; }
            if (any && bounds.size.y > 0.5f && bounds.size.y < 4f) height = bounds.size.y;
            var copy = Instantiate(source.gameObject, hold.transform);
            copy.transform.localPosition = Vector3.zero; copy.transform.localRotation = Quaternion.identity; copy.transform.localScale = source.transform.lossyScale;
            // The game's held tool, its holder and the flashlight stay with the real player.
            foreach (var holder in copy.GetComponentsInChildren<FirstPersonWeaponHolder>(true)) DestroyImmediate(holder.gameObject);
            foreach (var t in copy.GetComponentsInChildren<EcoCatalogueToolVisual>(true)) if (t != null) DestroyImmediate(t.gameObject);
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(behaviour);
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
            foreach (var light in copy.GetComponentsInChildren<Light>(true)) DestroyImmediate(light);
            hero = copy.transform;
            animator = copy.GetComponent<Animator>();
            if (animator != null) { animator.updateMode = AnimatorUpdateMode.UnscaledTime; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        }
        hold.name = "Title hero"; hold.SetActive(true);
        if (hero == null) return;
        rightArm = Bone(HumanBodyBones.RightUpperArm, "RightArm"); rightForearm = Bone(HumanBodyBones.RightLowerArm, "RightForeArm"); rightHand = Bone(HumanBodyBones.RightHand, "RightHand");
        leftArm = Bone(HumanBodyBones.LeftUpperArm, "LeftArm"); leftForearm = Bone(HumanBodyBones.LeftLowerArm, "LeftForeArm"); leftHand = Bone(HumanBodyBones.LeftHand, "LeftHand");
        chest = Bone(HumanBodyBones.UpperChest, "Spine2") ?? Bone(HumanBodyBones.Chest, "Spine1");
        rightMiddle = Bone(HumanBodyBones.RightMiddleProximal, "RightHandMiddle1"); leftMiddle = Bone(HumanBodyBones.LeftMiddleProximal, "LeftHandMiddle1");

        // Facing the camera, turned towards the menu on the left.
        baseForward = new Vector3(-0.62f, 0f, -1f).normalized;
        yaw = Mathf.Atan2(baseForward.x, baseForward.z) * Mathf.Rad2Deg;
        hero.rotation = Quaternion.Euler(0f, yaw, 0f);

        vacuum = EcoCatalogueToolVisual.Create(PlayerWeaponSystem.Tool.Vacuum, null, root);
        vacuum.gameObject.SetActive(true);
        tool = vacuum.transform;
        var toolBounds = new Bounds(tool.position, Vector3.zero); bool found = false;
        foreach (var r in tool.GetComponentsInChildren<Renderer>()) { if (!found) toolBounds = r.bounds; else toolBounds.Encapsulate(r.bounds); found = true; }
        float length = found ? Mathf.Max(toolBounds.size.x, toolBounds.size.y, toolBounds.size.z) : 0.5f;
        toolScale = height * 0.4f / Mathf.Max(0.05f, length);
        tool.localScale = Vector3.one * toolScale;
        aimPoint = smoothedAim = Chest + baseForward * 3f;
    }

    private Transform Bone(HumanBodyBones id, string suffix)
    {
        if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
        {
            var bone = animator.GetBoneTransform(id);
            if (bone != null) return bone;
        }
        foreach (var t in hero.GetComponentsInChildren<Transform>(true))
            if (t.name == suffix || t.name.EndsWith(":" + suffix)) return t;
        return null;
    }

    private Vector3 Chest => chest != null ? chest.position : Origin + Vector3.up * height * 0.72f;

    // ------------------------------------------------------------------ aiming

    /// <summary>The world point the vacuum points at for a screen position: on a plane between the camera and the
    /// character, so the mouth always faces out of the screen towards the cursor.</summary>
    public Vector3 WorldAt(Vector2 screen)
    {
        if (Cam == null) return Origin;
        var ray = Cam.ScreenPointToRay(screen);
        float depth = Vector3.Dot(Origin + Vector3.up * height * 0.7f - Cam.transform.position, Cam.transform.forward) - 2.6f;
        var plane = new Plane(-Cam.transform.forward, Cam.transform.position + Cam.transform.forward * Mathf.Max(1f, depth));
        return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : ray.GetPoint(3f);
    }

    public void AimAt(Vector2 screen) => aimPoint = WorldAt(screen);
    public void Kick() => kick = 1f;

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        ApplyLighting();
        if (hero == null || tool == null) return;
        smoothedAim = Vector3.Lerp(smoothedAim, aimPoint, 1f - Mathf.Exp(-14f * dt));

        // The body turns towards the cursor (within reason), the arms follow the tool.
        Vector3 flat = Vector3.ProjectOnPlane(smoothedAim - hero.position, Vector3.up);
        if (flat.sqrMagnitude > 0.01f)
        {
            float wanted = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float baseYaw = Mathf.Atan2(baseForward.x, baseForward.z) * Mathf.Rad2Deg;
            wanted = baseYaw + Mathf.Clamp(Mathf.DeltaAngle(baseYaw, wanted), -70f, 70f);
            yaw = Mathf.LerpAngle(yaw, wanted, 1f - Mathf.Exp(-8f * dt));
        }
        hero.rotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 c = Chest;
        Vector3 aim = (smoothedAim - c).normalized;
        Vector3 forward = hero.forward;
        if (Vector3.Dot(aim, forward) < 0.25f) aim = (aim + forward * (0.25f - Vector3.Dot(aim, forward))).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float h = height / 1.8f;
        Vector3 grip = c + aim * 0.26f * h + right * 0.1f * h - Vector3.up * 0.16f * h;

        kick = Mathf.MoveTowards(kick, 0f, dt * 4f);
        tool.rotation = Quaternion.LookRotation(aim, Vector3.up);
        tool.localScale = Vector3.one * toolScale;
        var palm = vacuum.Palm != null ? vacuum.Palm : tool;
        tool.position += grip - palm.position - aim * (0.07f * h * Mathf.Sin(kick * Mathf.PI));
        work = Mathf.MoveTowards(work, 0f, dt * 2f);
        vacuum.MenuSpin(work, dt);

        Solve(rightArm, rightForearm, rightHand, palm.position, rightArm != null ? rightArm.position + right * 0.35f - Vector3.up * 0.5f - aim * 0.2f : Vector3.zero);
        Vector3 support = vacuum.Support != null ? vacuum.Support.position : palm.position + aim * 0.2f * h;
        Solve(leftArm, leftForearm, leftHand, support, leftArm != null ? leftArm.position - right * 0.35f - Vector3.up * 0.5f : Vector3.zero);
        Point(rightHand, rightMiddle, (aim * 0.5f - Vector3.up * 0.2f - right * 0.6f).normalized);
        Point(leftHand, leftMiddle, (aim * 0.4f - Vector3.up * 0.3f + right * 0.6f).normalized);
    }

    /// <summary>Two-bone IK: the upper bone bends towards <paramref name="hint"/> so the end reaches <paramref name="target"/>.</summary>
    private static void Solve(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint)
    {
        if (a == null || b == null || c == null) return;
        float la = Vector3.Distance(a.position, b.position), lb = Vector3.Distance(b.position, c.position);
        Vector3 toTarget = target - a.position;
        float d = Mathf.Clamp(toTarget.magnitude, 0.01f, la + lb - 0.001f);
        Vector3 dir = toTarget.normalized;
        float cos = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
        Vector3 axis = Vector3.Cross(dir, hint - a.position);
        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(dir, Vector3.down);
        Vector3 elbow = a.position + Quaternion.AngleAxis(Mathf.Acos(cos) * Mathf.Rad2Deg, axis.normalized) * dir * la;
        a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
    }

    private static void Point(Transform hand, Transform finger, Vector3 direction)
    {
        if (hand == null || finger == null || direction.sqrMagnitude < 0.01f) return;
        hand.rotation = Quaternion.FromToRotation(finger.position - hand.position, direction) * hand.rotation;
    }

    // ------------------------------------------------------------------ vacuum effect

    private void BuildParticles()
    {
        var source = Resources.Load<Material>("EcoEnvironment/Particle");
        Material Mat() { var m = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); m.hideFlags = HideFlags.DontSave; owned.Add(m); return m; }
        specks = MakeSystem("Title vacuum specks", Mat(), false);
        streaks = MakeSystem("Title vacuum streaks", Mat(), true);
    }

    private ParticleSystem MakeSystem(string name, Material material, bool stretch)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.useUnscaledTime = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false; main.startSpeed = 0f; main.maxParticles = 900; main.gravityModifier = 0f; main.loop = false;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.15f)));
        var color = ps.colorOverLifetime; color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        color.color = g;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.maxParticleSize = 0.2f;
        if (stretch) { renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = 0.06f; renderer.lengthScale = 1.2f; }
        ps.Play();
        return ps;
    }

    private static readonly Color[] Bits =
    {
        new Color32(232, 214, 176, 255), new Color32(120, 196, 96, 255), new Color32(255, 255, 255, 230), new Color32(78, 150, 70, 255), new Color32(214, 190, 140, 255)
    };

    /// <summary>Dust and leaf flecks drawn from around <paramref name="from"/> into the vacuum's mouth.</summary>
    public void Suck(Vector3 from, float dt, float rate = 180f)
    {
        work = 1f;
        Vector3 mouth = Muzzle;
        emitDebt += rate * dt;
        while (emitDebt >= 1f)
        {
            emitDebt -= 1f;
            float life = Random.Range(0.3f, 0.55f);
            Vector3 p = from + Random.insideUnitSphere * 0.32f;
            Vector3 v = (mouth - p) / life;
            var e = new ParticleSystem.EmitParams { position = p, velocity = v, startLifetime = life, startColor = Bits[Random.Range(0, Bits.Length)], startSize = Random.Range(0.05f, 0.12f) };
            if (Random.value < 0.35f) { e.startSize *= 0.6f; e.startColor = new Color(1f, 1f, 1f, 0.6f); streaks.Emit(e, 1); }
            else specks.Emit(e, 1);
        }
    }

    /// <summary>A gust out of the vacuum's mouth towards <paramref name="to"/>.</summary>
    public void Blow(Vector3 to, float dt, float rate = 120f)
    {
        work = 1f;
        Vector3 mouth = Muzzle;
        emitDebt += rate * dt;
        while (emitDebt >= 1f)
        {
            emitDebt -= 1f;
            float life = Random.Range(0.25f, 0.45f);
            Vector3 target = to + Random.insideUnitSphere * 0.35f;
            var e = new ParticleSystem.EmitParams { position = mouth + Random.insideUnitSphere * 0.03f, velocity = (target - mouth) / life, startLifetime = life, startColor = Bits[Random.Range(0, Bits.Length)], startSize = Random.Range(0.05f, 0.11f) };
            if (Random.value < 0.4f) { e.startColor = new Color(1f, 1f, 1f, 0.6f); streaks.Emit(e, 1); } else specks.Emit(e, 1);
        }
    }

    /// <summary>A small puff at the mouth: something was swallowed or shot out.</summary>
    public void Pop()
    {
        Vector3 mouth = Muzzle;
        for (int i = 0; i < 22; i++)
        {
            var e = new ParticleSystem.EmitParams { position = mouth, velocity = Random.insideUnitSphere * 1.6f, startLifetime = Random.Range(0.25f, 0.45f), startColor = Bits[Random.Range(0, Bits.Length)], startSize = Random.Range(0.06f, 0.13f) };
            specks.Emit(e, 1);
        }
        Kick();
    }

    // ------------------------------------------------------------------ teardown

    public void Close()
    {
        foreach (var b in disabled) if (b != null) b.enabled = true;
        disabled.Clear();
        RenderSettings.fog = fog; RenderSettings.ambientMode = ambientMode; RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator; RenderSettings.ambientGroundColor = ambientGround; RenderSettings.ambientLight = ambientLight;
        foreach (var o in owned) if (o != null) Destroy(o);
        owned.Clear();
        Destroy(gameObject);
    }
}
