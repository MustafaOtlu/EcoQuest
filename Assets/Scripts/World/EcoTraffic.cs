using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD v5 §3 (valley traffic): a few townsfolk and cars use the connected road network of the assembled valley, and now
/// and then they drop litter (a can or a bottle). Litter is ordinary loose waste the vacuum collects for metal and
/// plastic, so the roads become a small, steady resource stream; it is flagged as litter so the "clear the waste piles"
/// goal is not undone by traffic. Presentation and economy only: the walkers have no colliders and nothing is saved here
/// (dropped litter is saved like any other waste).
/// </summary>
public sealed class EcoTraffic : MonoBehaviour
{
    public static EcoTraffic Current { get; private set; }
    public const int Walkers = 7, Cars = 4, LitterCap = 40;
    private const float Cell = 1.5f;
    private const float PlayerScale = 1.4f;
    // Ground speed at which the walk clip plays at normal rate (its stride grows with the 1.4 scale).
    private const float StrideSpeed = 1.3f * PlayerScale;

    // ------------------------------------------------------------------ road grid
    private int width, height;
    private Vector2 origin;
    private bool[] road;
    private float[] ground;
    private int[] edge; // distance in cells to the nearest non-road cell (1 = kerb)
    private readonly List<int> laneCells = new List<int>(), kerbCells = new List<int>();
    private bool built;
    private float buildAt;
    private System.Random random;

    private sealed class Mover
    {
        public Transform body;
        public bool car;
        public float speed, nextLitter, wait;
        public List<Vector3> path = new List<Vector3>();
        public int index;
        public Animator animator;
        public float stuck;
        // Throw pose (walkers): the right arm bones, the time into the throw (-1 = none) and where the litter lands.
        public Transform arm, forearm, hand, head;
        public float throwing = -1f;
        // Caught in the act: seconds into the guilty look at the player (-1 = none), then a hurried walk away.
        public float glance = -1f, hurryUntil;
        public bool released, throwMetal;
        public Vector3 throwTo;
    }
    private readonly List<Mover> movers = new List<Mover>();
    private Transform player;
    private float lastNotice = -999f;

    public static EcoTraffic Ensure()
    {
        if (Current != null) return Current;
        // An ordinary scene object, so it goes with the scene. With HideFlags.DontSave it survived every scene reload
        // (each run start and "Devam et"): the old townsfolk and cars stayed behind frozen, a new set was added on top,
        // and after a few reloads the valley held dozens of statues (and their colliders and animators).
        var go = new GameObject("Valley traffic");
        return go.AddComponent<EcoTraffic>();
    }

    private void OnEnable()
    {
        if (Current != null && Current != this) { Destroy(gameObject); return; }
        Current = this;
        buildAt = Time.unscaledTime + 1f; // let the valley assembler and the run finish their first frame
        int seed = EcoRun.Current != null ? EcoRun.Current.State.seed : 1;
        random = new System.Random(seed ^ 0x2f6b31);
    }

    private void OnDisable() { if (Current == this) Current = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;

    private float Next() => (float)random.NextDouble();

    private static bool IsRoad(Collider c)
    {
        for (var t = c.transform; t != null; t = t.parent) if (t.name == "Roads") return true;
        return false;
    }

    private int attempts;

    private void BuildGrid()
    {
        built = true;
        // The run starts behind the paused morning card: no physics step has run since the tiles were placed and
        // turned, so their colliders still sit where they were instantiated. Raycasting then found a few metres of road
        // around the start and everybody piled up there, unable to go anywhere.
        Physics.SyncTransforms();
        laneCells.Clear(); kerbCells.Clear();
        var map = EcoGround.HasMap ? EcoGround.MapRect : new Rect(0, 0, 150, 150);
        origin = map.position;
        width = Mathf.CeilToInt(map.width / Cell); height = Mathf.CeilToInt(map.height / Cell);
        road = new bool[width * height]; ground = new float[width * height]; edge = new int[width * height];
        var hits = new RaycastHit[8];
        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
            {
                var top = new Vector3(origin.x + (x + 0.5f) * Cell, 120f, origin.y + (z + 0.5f) * Cell);
                int n = Physics.RaycastNonAlloc(top, Vector3.down, hits, 240f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MinValue; bool isRoad = false;
                for (int i = 0; i < n; i++)
                {
                    if (hits[i].point.y <= best) continue;
                    // The highest solid hit decides: a house or tree on the road blocks the cell.
                    best = hits[i].point.y; isRoad = IsRoad(hits[i].collider);
                }
                int k = x + z * width;
                road[k] = isRoad && n > 0; ground[k] = best;
            }
        // Distance to the kerb by breadth-first search from every non-road cell.
        var queue = new Queue<int>();
        for (int k = 0; k < road.Length; k++) { edge[k] = road[k] ? int.MaxValue : 0; if (!road[k]) queue.Enqueue(k); }
        while (queue.Count > 0)
        {
            int k = queue.Dequeue(), x = k % width, z = k / width;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), nz = z + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                int nk = nx + nz * width;
                if (edge[nk] > edge[k] + 1) { edge[nk] = edge[k] + 1; queue.Enqueue(nk); }
            }
        }
        // Keep only the largest connected network so nobody is stranded on an isolated piece of asphalt.
        var component = new int[road.Length]; int bestId = 0, bestSize = 0, id = 0;
        for (int k = 0; k < road.Length; k++)
        {
            if (!road[k] || component[k] != 0) continue;
            id++; int size = 0; queue.Enqueue(k); component[k] = id;
            while (queue.Count > 0)
            {
                int c = queue.Dequeue(); size++;
                foreach (int nk in Neighbours(c)) if (road[nk] && component[nk] == 0) { component[nk] = id; queue.Enqueue(nk); }
            }
            if (size > bestSize) { bestSize = size; bestId = id; }
        }
        for (int k = 0; k < road.Length; k++)
        {
            if (road[k] && component[k] != bestId) road[k] = false;
            if (!road[k]) continue;
            if (edge[k] >= 2) laneCells.Add(k);
            if (edge[k] == 1) kerbCells.Add(k);
        }
    }

    private IEnumerable<int> Neighbours(int k)
    {
        int x = k % width, z = k / width;
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                yield return nx + nz * width;
            }
    }

    private Vector3 CellPoint(int k) => new Vector3(origin.x + (k % width + 0.5f) * Cell, ground[k], origin.y + (k / width + 0.5f) * Cell);
    private int CellAt(Vector3 p)
    {
        int x = Mathf.FloorToInt((p.x - origin.x) / Cell), z = Mathf.FloorToInt((p.z - origin.y) / Cell);
        return x < 0 || z < 0 || x >= width || z >= height ? -1 : x + z * width;
    }

    /// <summary>A* over road cells. Cars pay to leave the middle of the road, walkers pay to leave the kerb.</summary>
    private bool FindPath(int from, int to, bool car, List<Vector3> path)
    {
        path.Clear();
        var cost = new Dictionary<int, float> { [from] = 0f };
        var came = new Dictionary<int, int>();
        var open = new SortedSet<(float, int)> { (0f, from) };
        var target = CellPoint(to);
        int guard = 0;
        while (open.Count > 0 && guard++ < 20000)
        {
            var current = open.Min; open.Remove(current);
            int k = current.Item2;
            if (k == to)
            {
                for (int c = to; ; c = came[c]) { path.Add(CellPoint(c)); if (c == from) break; }
                path.Reverse();
                return true;
            }
            foreach (int nk in Neighbours(k))
            {
                if (!road[nk]) continue;
                int e = edge[nk];
                float penalty = car ? (e >= 3 ? 0f : e == 2 ? 0.6f : 6f) : (e == 1 ? 0f : e == 2 ? 0.8f : 2.5f);
                bool diagonal = nk % width != k % width && nk / width != k / width;
                float g = cost[k] + (diagonal ? 1.414f : 1f) + penalty;
                if (cost.TryGetValue(nk, out float old) && old <= g) continue;
                if (cost.ContainsKey(nk)) open.Remove((old, nk));
                cost[nk] = g; came[nk] = k;
                open.Add((g + Vector3.Distance(CellPoint(nk), target) / Cell, nk));
            }
        }
        return false;
    }

    private void Plan(Mover m, bool turnBack = false)
    {
        var cells = m.car ? laneCells : kerbCells;
        if (cells.Count == 0) return;
        int from = CellAt(m.body.position);
        if (from < 0 || !road[from])
        {
            // Start from the nearest suitable cell; the first leg walks there.
            float best = float.MaxValue;
            foreach (int c in cells)
            {
                float d = (CellPoint(c) - m.body.position).sqrMagnitude;
                if (d < best) { best = d; from = c; }
            }
        }
        for (int attempt = 0; attempt < (turnBack ? 24 : 8); attempt++)
        {
            int to = cells[random.Next(cells.Count)];
            if ((CellPoint(to) - CellPoint(from)).sqrMagnitude < 30f * 30f) continue;
            if (turnBack && attempt < 20 && Vector3.Dot(CellPoint(to) - m.body.position, m.body.forward) > 0f) continue;
            if (FindPath(from, to, m.car, m.path)) { m.index = 0; return; }
        }
    }

    private void Spawn()
    {
        var male = Resources.Load<GameObject>("EcoTraffic/Townsfolk Male");
        var female = Resources.Load<GameObject>("EcoTraffic/Townsfolk Female");
        var car = Resources.Load<GameObject>("EcoTraffic/Car");
        Color[] paints = { new Color(0.78f, 0.22f, 0.18f), new Color(0.2f, 0.42f, 0.7f), new Color(0.92f, 0.82f, 0.3f),
            new Color(0.88f, 0.88f, 0.86f), new Color(0.3f, 0.55f, 0.35f), new Color(0.45f, 0.45f, 0.5f) };
        Color[] outfits = { new Color(0.78f, 0.88f, 1f), new Color(0.82f, 1f, 0.86f), new Color(0.92f, 0.85f, 1f) };
        for (int i = 0; i < Walkers + Cars; i++)
        {
            bool isCar = i >= Walkers;
            var cells = isCar ? laneCells : kerbCells;
            var prefab = isCar ? car : (i % 3 == 2 ? male : female);
            if (prefab == null || cells.Count == 0) continue;
            var start = CellPoint(cells[random.Next(cells.Count)]);
            var go = Instantiate(prefab, start, Quaternion.Euler(0, Next() * 360f, 0), transform);
            go.name = isCar ? "Traffic car" : "Townsfolk";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            // Solid, so the player bumps into people and cars instead of walking through them; kinematic, so they
            // are moved by this script and never shoved around by physics.
            if (isCar) { var box = go.AddComponent<BoxCollider>(); box.center = new Vector3(0f, 0.55f, 0f); box.size = new Vector3(1.2f, 1.05f, 2.4f); }
            else { var capsule = go.AddComponent<CapsuleCollider>(); capsule.center = new Vector3(0f, 0.58f, 0f); capsule.height = 1.15f; capsule.radius = 0.2f; }
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (!isCar)
            {
                // The player model stands at 1.4 scale (about 1.6 m); townsfolk match it, give or take a few percent.
                go.transform.localScale = Vector3.one * PlayerScale * Mathf.Lerp(0.94f, 1.06f, Next());
                // The male walker shares the player's model: a light cool tint keeps the player the one in the brown coat.
                if (prefab == male)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", outfits[random.Next(outfits.Length)]);
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
                }
            }
            else
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", paints[random.Next(paints.Length)]);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                    if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Car Paint")) r.SetPropertyBlock(block);
            }
            var m = new Mover
            {
                body = go.transform, car = isCar, animator = go.GetComponentInChildren<Animator>(),
                speed = isCar ? Mathf.Lerp(5f, 7f, Next()) : Mathf.Lerp(1.35f, 1.75f, Next()),
                // The first drop comes a while after the run starts, then every one to three minutes.
                nextLitter = Time.time + Mathf.Lerp(20f, 90f, Next()),
            };
            if (m.animator != null) m.animator.speed = m.speed / StrideSpeed;
            if (!isCar)
                foreach (var t in go.GetComponentsInChildren<Transform>())
                {
                    if (t.name == "mixamorig:RightArm") m.arm = t;
                    else if (t.name == "mixamorig:RightForeArm") m.forearm = t;
                    else if (t.name == "mixamorig:RightHand") m.hand = t;
                    else if (t.name == "mixamorig:Head") m.head = t;
                }
            movers.Add(m);
            Plan(m);
        }
    }

    private void Update()
    {
        if (!built)
        {
            if (Time.unscaledTime < buildAt || Time.timeScale <= 0f) return;
            BuildGrid();
            // A valley always has well over a thousand lane cells; far fewer means the world was not ready yet.
            if (laneCells.Count < 300 && ++attempts < 6) { built = false; buildAt = Time.unscaledTime + 2f; return; }
            Spawn();
            return;
        }
        if (player == null) { var p = FindFirstObjectByType<PlayerController>(); if (p != null) player = p.transform; }
        float dt = Time.deltaTime;
        foreach (var m in movers)
        {
            if (m.body == null) continue;
            if (m.index >= m.path.Count) { if ((m.wait -= dt) <= 0f) { Plan(m); m.wait = m.car ? 0.5f : Mathf.Lerp(1f, 4f, Next()); } continue; }
            var pos = m.body.position;
            // Cars round corners wide and can pass a waypoint without coming within reach of it. The index then lagged
            // behind while the look-ahead point sat right under the car: it neither drove nor turned, and the cars
            // behind queued up for good. A waypoint counts as passed once the mover is beyond it along the path.
            while (m.index + 1 < m.path.Count)
            {
                var a = m.path[m.index]; var ab = m.path[m.index + 1] - a; ab.y = 0f;
                var ap = pos - a; ap.y = 0f;
                if (ab.sqrMagnitude > 0.0001f && Vector3.Dot(ap, ab) >= ab.sqrMagnitude) m.index++; else break;
            }
            // Aim a little ahead along the path so corners are rounded rather than stepped.
            int look = Mathf.Min(m.path.Count - 1, m.index + (m.car ? 2 : 1));
            var aim = m.path[look]; aim.y = pos.y;
            var flat = aim - pos;
            float off = (m.path[m.index] - pos).sqrMagnitude;
            if (off > 6f * 6f && m.index > 0) { Plan(m); continue; } // knocked off the route: walk back to the nearest road
            if (flat.sqrMagnitude < 0.0001f || off < (m.car ? 2.2f : 0.6f) * (m.car ? 2.2f : 0.6f)) { m.index++; continue; }
            float speed = m.speed;
            // Slow down while throwing, so the swing reads as a deliberate act.
            if (m.throwing >= 0f || m.glance >= 0f) speed *= 0.4f;
            else if (Time.time < m.hurryUntil) speed *= 1.9f; // embarrassed: off at a near trot
            bool blocked = m.car ? Blocked(m, pos, flat.normalized) : player != null && Ahead(pos, flat.normalized, player.position, 0.9f, 0.55f);
            if (blocked) speed = 0f;
            // A car facing away from where it must go slows down and turns first instead of sweeping through.
            if (m.car) speed *= Mathf.Clamp01(Vector3.Dot(m.body.forward, flat.normalized) * 1.4f - 0.2f);
            // Standing still for 4 s for any reason (someone in the road, a turn that does not resolve): take another
            // way. Blocked movers turn back; the rest simply plan afresh from where they are.
            if (speed < 0.05f) { if ((m.stuck += dt) > 4f) { m.stuck = 0f; Plan(m, blocked); continue; } }
            else m.stuck = 0f;
            if (m.animator != null) m.animator.speed = speed > 0f ? speed / StrideSpeed : 0f;
            var forward = Vector3.RotateTowards(m.body.forward, flat.normalized, (m.car ? 2.4f : 5f) * dt, 0f);
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) m.body.rotation = Quaternion.LookRotation(forward);
            pos += m.body.forward * speed * dt;
            int cell = CellAt(pos);
            if (cell >= 0 && road[cell]) pos.y = Mathf.Lerp(pos.y, ground[cell], 1f - Mathf.Exp(-10f * dt));
            m.body.position = pos;
            if (Time.time >= m.nextLitter)
            {
                m.nextLitter = Time.time + (m.car ? Mathf.Lerp(80f, 170f, Next()) : Mathf.Lerp(60f, 150f, Next()));
                DropLitter(m);
            }
        }
    }

    /// <summary>Cars wait for the player and for the car in front instead of driving through them.</summary>
    private bool Blocked(Mover self, Vector3 pos, Vector3 heading)
    {
        if (player != null && Ahead(pos, heading, player.position, 3.4f)) return true;
        foreach (var other in movers)
            if (other != self && other.body != null && other.car && Ahead(pos, heading, other.body.position, 3.6f)
                && Vector3.Dot(other.body.forward, heading) > -0.2f) return true;
        return false;
    }

    private static bool Ahead(Vector3 pos, Vector3 heading, Vector3 other, float range, float halfWidth = 1.1f)
    {
        var d = other - pos; d.y = 0f;
        float along = Vector3.Dot(d, heading);
        return along > 0f && along < range && Mathf.Abs(Vector3.Cross(heading, d).y) < halfWidth;
    }

    public static int LitterCount()
    {
        int n = 0;
        foreach (var r in FindObjectsByType<RecyclableResource>(FindObjectsSortMode.None)) if (r != null && r.litter && !r.Claimed) n++;
        return n;
    }

    private const float ThrowSeconds = 0.95f, ReleaseAt = 0.4f;

    private void LateUpdate()
    {
        // After the walk clip has posed the skeleton: wind the right arm up behind the shoulder, swing it forward,
        // let go at the front of the swing, then ease back into the walk.
        float dt = Time.deltaTime;
        foreach (var m in movers)
        {
            if (m.body == null) continue;
            if (m.glance >= 0f) Glance(m, dt);
            if (m.throwing < 0f) continue;
            m.throwing += dt;
            float t = m.throwing;
            if (m.arm != null && m.forearm != null && m.hand != null)
            {
                var fwd = m.body.forward; var up = Vector3.up; var right = m.body.right;
                var wind = (-fwd * 0.45f + up * 0.85f + right * 0.25f).normalized;
                var front = (fwd * 0.9f + up * 0.35f + right * 0.1f).normalized;
                float swing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, ReleaseAt, t));
                var dir = Vector3.Slerp(wind, front, swing);
                float weight = t < 0.22f ? Mathf.SmoothStep(0f, 1f, t / 0.22f)
                    : t < 0.62f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.62f) / (ThrowSeconds - 0.62f));
                Aim(m.arm, m.forearm.position, dir, weight);
                Aim(m.forearm, m.hand.position, dir, weight * 0.85f);
            }
            if (!m.released && t >= ReleaseAt)
            {
                m.released = true;
                var start = m.hand != null ? m.hand.position : m.body.position + Vector3.up * 1.3f + m.body.forward * 0.4f;
                EcoLitterToss.Throw(start, m.throwTo, m.throwMetal, 0.55f, 0.7f, transform);
                Notice(m, m.throwTo);
                // Seen by the player: a guilty look over the shoulder with a "!" overhead, then hurry off.
                if (player != null && (player.position - m.body.position).sqrMagnitude < GuiltyRange * GuiltyRange)
                {
                    m.glance = 0f; m.hurryUntil = Time.time + GlanceSeconds + 4f;
                    var anchor = m.head != null ? m.head : m.body;
                    EcoFloatingText.Follow(anchor, Vector3.up * (m.head != null ? 0.45f : 2.1f), "!", EcoFloatingText.Alert, 1.1f);
                }
            }
            if (t >= ThrowSeconds) m.throwing = -1f;
        }
    }

    private const float GuiltyRange = 14f, GlanceSeconds = 1.1f;

    /// <summary>Turns the head towards the player for a moment, easing in and out.</summary>
    private void Glance(Mover m, float dt)
    {
        m.glance += dt;
        if (m.glance >= GlanceSeconds) { m.glance = -1f; return; }
        if (m.head == null || player == null) return;
        var to = player.position + Vector3.up * 1.4f - m.head.position;
        var facing = m.body.forward;
        // No owl necks: at most 75 degrees off the shoulders.
        var look = Vector3.RotateTowards(facing, to.normalized, 75f * Mathf.Deg2Rad, 0f);
        float t = m.glance / GlanceSeconds;
        float weight = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 0.2f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t)));
        m.head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(facing, look), weight) * m.head.rotation;
    }

    /// <summary>Turns a bone so the segment from it to <paramref name="child"/> points along <paramref name="dir"/>.</summary>
    private static void Aim(Transform bone, Vector3 child, Vector3 dir, float weight)
    {
        var current = child - bone.position;
        if (current.sqrMagnitude < 1e-6f || weight <= 0f) return;
        var wanted = Vector3.Slerp(current.normalized, dir, weight);
        bone.rotation = Quaternion.FromToRotation(current.normalized, wanted) * bone.rotation;
    }

    private void DropLitter(Mover m)
    {
        if (m.throwing >= 0f || LitterCount() >= LitterCap) return;
        // Walkers throw it a couple of metres ahead and to one side; a car flicks it out of the window onto the verge.
        var side = m.body.right * (Next() < 0.5f ? -1f : 1f);
        var spot = m.car ? m.body.position + side * 1.8f - m.body.forward * 1.4f
            : m.body.position + m.body.forward * Mathf.Lerp(2f, 2.8f, Next()) + m.body.right * Mathf.Lerp(0.2f, 0.7f, Next());
        // The highest surface that is not a car or a walker (otherwise a can could land on a car roof).
        RaycastHit hit = default; bool found = false;
        foreach (var h in Physics.RaycastAll(spot + Vector3.up * 6f, Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.GetComponentInParent<EcoTraffic>() == null && (!found || h.point.y > hit.point.y)) { hit = h; found = true; }
        if (!found) return;
        if (hit.normal.y < 0.7f || hit.collider.GetComponentInParent<Rigidbody>() != null) return;
        bool metal = Next() < 0.45f;
        if (m.car)
        {
            var window = m.body.position + Vector3.up * 0.95f + side * 0.55f + m.body.forward * 0.2f;
            EcoLitterToss.Throw(window, hit.point, metal, 0.6f, 0.55f, transform);
            Notice(m, hit.point);
            return;
        }
        m.throwing = 0f; m.released = false; m.throwTo = hit.point; m.throwMetal = metal;
    }

    private void Notice(Mover m, Vector3 point)
    {
        if (player == null || (player.position - point).sqrMagnitude >= 22f * 22f || Time.time - lastNotice <= 90f) return;
        lastNotice = Time.time;
        var weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        if (weapons != null) weapons.ShowFeedback(m.car ? "Bir araç camdan çöp attı: süpürgeyle topla" : "Bir yaya yere çöp attı: süpürgeyle topla");
    }

    /// <summary>A single can (metal) or bottle (plastic). Shared by the traffic and the run's opening scatter.</summary>
    public static GameObject Litter(Vector3 ground, bool metal)
    {
        var drop = EcoWorldArt.Spawn(metal ? "MetalScrap" : "PlasticScrap", ground + Vector3.up * 0.03f);
        if (drop == null) return null;
        drop.name = "Litter";
        drop.transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
        var resource = drop.GetComponent<RecyclableResource>() ?? drop.AddComponent<RecyclableResource>();
        resource.metal = metal ? 2 : 0; resource.plastic = metal ? 0 : 2; resource.organic = 0;
        resource.processingSeconds = 0.8f; resource.requiredRecyclerRank = 0; resource.litter = true;
        return drop;
    }
}
