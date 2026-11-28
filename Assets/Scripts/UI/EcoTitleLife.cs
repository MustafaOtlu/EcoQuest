using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Life on the title set (GDD v5 §21, "Geri bildirim 17"): a townsperson strolls about behind the character and now
/// and then throws a can or bottle on the grass with the same arm swing as the valley's walkers (EcoTraffic), and the
/// game's birds circle over a few of the trees. The world is paused under the title, so everything here runs on
/// unscaled time and nothing is collectable, solid or saved.
/// </summary>
public sealed class EcoTitleLife : MonoBehaviour
{
    private const float PlayerScale = 1.4f, StrideSpeed = 1.3f * PlayerScale, WalkSpeed = 1.05f;
    private const float ThrowSeconds = 0.95f, ReleaseAt = 0.4f, GlanceSeconds = 1.1f;
    private const int LitterCap = 5;

    private EcoTitleStage stage;
    private Transform hero;

    // The walker.
    private Transform body, arm, forearm, hand, head;
    private Animator animator;
    private Vector3 target;
    private float throwing = -1f, glance = -1f, nextThrow, retarget;
    private bool released;
    private Vector3 throwTo;
    private bool throwMetal;

    // Litter: flying copies and the pieces lying on the grass.
    private sealed class Toss { public Transform piece; public Vector3 from, to, axis; public float age, spin; public bool landed; }
    private readonly List<Toss> tosses = new List<Toss>();
    private readonly List<Transform> lying = new List<Transform>();
    private readonly List<Transform> fading = new List<Transform>();

    // Birds circling over trees.
    private sealed class Bird { public Transform body; public Transform[] wings; public Vector3 centre; public float radius, phase, speed, height; }
    private readonly List<Bird> birds = new List<Bird>();

    private static readonly Color Dust = new Color32(205, 184, 146, 220);
    private static readonly Color Ping = new Color32(255, 236, 120, 235);

    internal static EcoTitleLife Add(EcoTitleStage stage, Transform root, Transform hero, List<Transform> trees)
    {
        var life = root.gameObject.AddComponent<EcoTitleLife>();
        life.stage = stage; life.hero = hero;
        life.BuildWalker(root);
        life.BuildBirds(root, trees);
        return life;
    }

    // ------------------------------------------------------------------ build

    private void BuildWalker(Transform root)
    {
        var prefab = Resources.Load<GameObject>(Random.value < 0.5f ? "EcoTraffic/Townsfolk Female" : "EcoTraffic/Townsfolk Male");
        if (prefab == null) prefab = Resources.Load<GameObject>("EcoTraffic/Townsfolk Female");
        if (prefab == null) return;
        var hold = new GameObject("Title walker (building)"); hold.SetActive(false); hold.transform.SetParent(root, false);
        var go = Instantiate(prefab, hold.transform);
        go.name = "Title walker";
        Strip(go);
        go.transform.localScale = Vector3.one * PlayerScale;
        if (prefab.name.Contains("Male"))
        {
            // Same tint as the valley's male walkers, so the character stays the one in the brown coat.
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.78f, 0.88f, 1f));
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
        }
        body = go.transform;
        body.position = PickSpot(Vector3.zero);
        target = PickSpot(body.position);
        body.rotation = Quaternion.LookRotation(Flat(target - body.position, Vector3.forward));
        animator = go.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.speed = WalkSpeed / StrideSpeed;
        }
        foreach (var t in go.GetComponentsInChildren<Transform>())
        {
            if (t.name == "mixamorig:RightArm") arm = t;
            else if (t.name == "mixamorig:RightForeArm") forearm = t;
            else if (t.name == "mixamorig:RightHand") hand = t;
            else if (t.name == "mixamorig:Head") head = t;
        }
        hold.name = "Title walker"; hold.SetActive(true);
        nextThrow = Time.unscaledTime + Random.Range(3f, 6f);
    }

    private void BuildBirds(Transform root, List<Transform> trees)
    {
        var prefab = Resources.Load<GameObject>("EcoArt/Bird");
        if (prefab == null || trees == null) return;
        // Trees in view on the right half, near enough to read: two or three of them get a bird or two.
        var picks = new List<Transform>();
        foreach (var tree in trees)
        {
            if (tree == null) continue;
            var v = stage.Cam.WorldToViewportPoint(tree.position + Vector3.up * 4f);
            if (v.z > 8f && v.z < 22f && v.x > 0.58f && v.x < 0.95f && v.y > 0.35f && v.y < 0.95f) picks.Add(tree);
        }
        for (int i = picks.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var s = picks[i]; picks[i] = picks[j]; picks[j] = s; }
        var hold = new GameObject("Title birds (building)"); hold.SetActive(false); hold.transform.SetParent(root, false);
        int count = 0;
        for (int i = 0; i < picks.Count && i < 3; i++)
        {
            float top = TreeTop(picks[i]);
            int flock = i == 0 ? 2 : 1;
            for (int k = 0; k < flock; k++)
            {
                var go = Instantiate(prefab, hold.transform);
                go.name = "Title bird";
                Strip(go);
                go.transform.localScale *= 1.6f;
                birds.Add(new Bird
                {
                    body = go.transform, centre = new Vector3(picks[i].position.x, top, picks[i].position.z),
                    radius = Random.Range(1.3f, 2.2f), phase = Random.value * 6.28f + k * 2.1f, speed = Random.Range(0.45f, 0.65f) * (Random.value < 0.5f ? 1f : -1f),
                    height = Random.Range(0.4f, 1.1f),
                    wings = System.Array.FindAll(go.GetComponentsInChildren<Transform>(), t => t.name.StartsWith("Wing")),
                });
                count++;
            }
        }
        hold.name = "Title birds"; hold.SetActive(true);
    }

    private static float TreeTop(Transform tree)
    {
        float top = tree.position.y + 4f; bool any = false;
        foreach (var r in tree.GetComponentsInChildren<Renderer>()) { top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y; any = true; }
        return top;
    }

    private static void Strip(GameObject go)
    {
        foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(behaviour);
        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(audio);
    }

    /// <summary>A spot on the grass behind the character that shows on the right half of the screen.</summary>
    private Vector3 PickSpot(Vector3 near)
    {
        var origin = stage.transform.position;
        Vector3 best = origin + new Vector3(1.8f, 0f, 2.6f);
        for (int i = 0; i < 24; i++)
        {
            var p = origin + new Vector3(Random.Range(-0.8f, 4.2f), 0f, Random.Range(1.3f, 4.6f));
            if (near != Vector3.zero && (p - near).sqrMagnitude < 1.6f * 1.6f) continue;
            if (InView(p, 0.56f) && InView(p + Vector3.up * 1.7f, 0.56f)) return p;
        }
        return best;
    }

    private bool InView(Vector3 p, float left)
    {
        if (stage == null || stage.Cam == null) return true;
        var v = stage.Cam.WorldToViewportPoint(p);
        return v.z > 0f && v.x > left && v.x < 0.96f && v.y > 0.04f && v.y < 0.96f;
    }

    private static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-4f ? v.normalized : fallback;
    }

    // ------------------------------------------------------------------ update

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        Walk(dt);
        UpdateTosses(dt);
        UpdateBirds(dt);
    }

    private void Walk(float dt)
    {
        if (body == null) return;
        float now = Time.unscaledTime;
        // A thrower slows down for the swing, then hurries a little.
        float speed = throwing >= 0f ? WalkSpeed * 0.55f : glance >= 0f ? WalkSpeed * 1.25f : WalkSpeed;
        Vector3 to = target - body.position; to.y = 0f;
        if (to.magnitude < 0.3f || now > retarget) { target = PickSpot(body.position); retarget = now + 14f; }
        var wanted = Quaternion.LookRotation(Flat(target - body.position, body.forward));
        body.rotation = Quaternion.RotateTowards(body.rotation, wanted, 120f * dt);
        // Walks where it faces, so it curves round instead of sliding sideways.
        body.position += body.forward * speed * dt;
        // Never into the character.
        if (hero != null)
        {
            var away = body.position - hero.position; away.y = 0f;
            if (away.magnitude < 1.1f) body.position = hero.position + away.normalized * 1.1f;
        }
        if (animator != null) animator.speed = speed / StrideSpeed;

        if (throwing < 0f && now >= nextThrow) TryThrow();
    }

    private void TryThrow()
    {
        nextThrow = Time.unscaledTime + 2f; // not now: try again shortly
        if (!InView(body.position, 0.56f) || !InView(body.position + Vector3.up * 1.7f, 0.56f)) return;
        var spot = body.position + body.forward * Random.Range(1.6f, 2.3f) + body.right * Random.Range(0.2f, 0.6f);
        spot.y = stage.transform.position.y + 0.02f;
        if (!InView(spot, 0.58f)) return;
        if (hero != null && (spot - hero.position).sqrMagnitude < 0.8f * 0.8f) return;
        throwTo = spot; throwMetal = Random.value < 0.45f;
        throwing = 0f; released = false;
        nextThrow = Time.unscaledTime + Random.Range(7f, 12f);
    }

    private void LateUpdate()
    {
        if (body == null) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (glance >= 0f) Glance(dt);
        if (throwing < 0f) return;
        // EcoTraffic's throw: after the walk clip has posed the skeleton, wind the right arm up behind the shoulder,
        // swing it forward, let go at the front of the swing, then ease back into the walk.
        throwing += dt;
        float t = throwing;
        if (arm != null && forearm != null && hand != null)
        {
            var fwd = body.forward; var up = Vector3.up; var right = body.right;
            var wind = (-fwd * 0.45f + up * 0.85f + right * 0.25f).normalized;
            var front = (fwd * 0.9f + up * 0.35f + right * 0.1f).normalized;
            float swing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, ReleaseAt, t));
            var dir = Vector3.Slerp(wind, front, swing);
            float weight = t < 0.22f ? Mathf.SmoothStep(0f, 1f, t / 0.22f)
                : t < 0.62f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.62f) / (ThrowSeconds - 0.62f));
            Aim(arm, forearm.position, dir, weight);
            Aim(forearm, hand.position, dir, weight * 0.85f);
        }
        if (!released && t >= ReleaseAt)
        {
            released = true;
            var start = hand != null ? hand.position : body.position + Vector3.up * 1.3f + body.forward * 0.4f;
            Throw(start, throwTo, throwMetal);
            glance = 0f; // a guilty look at the character
        }
        if (t >= ThrowSeconds) throwing = -1f;
    }

    private void Glance(float dt)
    {
        glance += dt;
        if (glance >= GlanceSeconds) { glance = -1f; return; }
        if (head == null || hero == null) return;
        var to = hero.position + Vector3.up * 1.4f - head.position;
        var facing = body.forward;
        var look = Vector3.RotateTowards(facing, to.normalized, 75f * Mathf.Deg2Rad, 0f);
        float k = glance / GlanceSeconds;
        float weight = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k / 0.2f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, k)));
        head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(facing, look), weight) * head.rotation;
    }

    private static void Aim(Transform bone, Vector3 child, Vector3 dir, float weight)
    {
        var current = child - bone.position;
        if (current.sqrMagnitude < 1e-6f || weight <= 0f) return;
        var wanted = Vector3.Slerp(current.normalized, dir, weight);
        bone.rotation = Quaternion.FromToRotation(current.normalized, wanted) * bone.rotation;
    }

    // ------------------------------------------------------------------ litter

    private const float FlightSeconds = 0.55f, Arc = 0.7f, HopSeconds = 0.28f;

    private void Throw(Vector3 from, Vector3 to, bool metal)
    {
        var prefab = Resources.Load<GameObject>(metal ? "EcoArt/MetalScrap" : "EcoArt/PlasticScrap");
        if (prefab == null) return;
        var hold = new GameObject("Title litter (building)"); hold.SetActive(false); hold.transform.SetParent(transform, false);
        var go = Instantiate(prefab, hold.transform);
        go.name = "Title litter";
        Strip(go);
        go.transform.SetParent(transform, true);
        Destroy(hold);
        go.transform.position = from;
        go.transform.localScale *= 1.25f;
        tosses.Add(new Toss { piece = go.transform, from = from, to = to, axis = Random.onUnitSphere, spin = Random.Range(540f, 900f) });
    }

    private void UpdateTosses(float dt)
    {
        for (int i = tosses.Count - 1; i >= 0; i--)
        {
            var toss = tosses[i];
            if (toss.piece == null) { tosses.RemoveAt(i); continue; }
            toss.age += dt;
            if (!toss.landed)
            {
                float t = Mathf.Clamp01(toss.age / FlightSeconds);
                toss.piece.position = Vector3.Lerp(toss.from, toss.to, t) + Vector3.up * (Arc * 4f * t * (1f - t));
                toss.piece.Rotate(toss.axis, toss.spin * dt, Space.World);
                if (t >= 1f) { toss.landed = true; stage.Ring(toss.to, Dust, 10, 0.9f, 0.12f, 0.45f); }
                continue;
            }
            float h = (toss.age - FlightSeconds) / HopSeconds;
            toss.piece.position = toss.to + Vector3.up * (0.18f * 4f * Mathf.Clamp01(h) * (1f - Mathf.Clamp01(h)));
            toss.piece.Rotate(toss.axis, toss.spin * 0.3f * dt, Space.World);
            if (h < 1f) continue;
            toss.piece.localScale /= 1.25f;
            stage.Ring(toss.to + Vector3.up * 0.05f, Ping, 14, 1.6f, 0.09f, 0.35f);
            lying.Add(toss.piece);
            tosses.RemoveAt(i);
            // Only a handful lie about: the oldest shrinks away.
            if (lying.Count > LitterCap) { fading.Add(lying[0]); lying.RemoveAt(0); }
        }
        for (int i = fading.Count - 1; i >= 0; i--)
        {
            var piece = fading[i];
            if (piece == null) { fading.RemoveAt(i); continue; }
            piece.localScale *= Mathf.Exp(-6f * dt);
            if (piece.localScale.x < 0.02f) { Destroy(piece.gameObject); fading.RemoveAt(i); }
        }
    }

    // ------------------------------------------------------------------ birds

    private void UpdateBirds(float dt)
    {
        float time = Time.unscaledTime;
        foreach (var bird in birds)
        {
            if (bird.body == null) continue;
            // EcoHabitat's flight: a slow circle with a gentle bob and quick wing beats.
            float phase = bird.phase + time * bird.speed;
            float sign = Mathf.Sign(bird.speed);
            bird.body.position = bird.centre + new Vector3(Mathf.Cos(phase) * bird.radius, bird.height + Mathf.Sin(phase * 2f) * 0.3f, Mathf.Sin(phase) * bird.radius);
            bird.body.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(phase), 0f, Mathf.Cos(phase)) * sign);
            foreach (var wing in bird.wings)
                if (wing != null) wing.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 12f + bird.phase) * 30f * (wing.name.EndsWith("L") ? 1f : -1f));
        }
    }
}
