using UnityEngine;
using UnityEngine.Rendering;

// Cosmetic particles never take part in weapon hit detection or consume gameplay randomness.
// Each mode has its own shape so it reads from the isometric camera at a glance:
//   collect  – a swirling funnel of dust and litter pulled into the nozzle;
//   blow     – white wind streaks fanning out, with a gust ring where they land;
//   water    – a real arcing jet (gravity) with a splash ring and mist on contact;
//   fill     – blue droplets drawn up into the nozzle; recycle – green sparks spiralling in.
// Success has its own feedback (Burst): steam when a fire or Alev is put out, a green sparkle when a plant drinks, a
// pop when litter is taken, a coloured burst when the right mode hits a threat, and a grey puff when the mode is wrong.
[DefaultExecutionOrder(160)]
public sealed partial class WeaponVisualEffects : MonoBehaviour
{
    public enum Flow { Water, Collect, Disperse, Recycle, Fill }
    public enum Burst { Hit, Fizzle, Steam, Grow, Pop, Splash }
    public Flow CurrentFlow { get; private set; }
    public bool Flowing => flowing;
    public ParticleSystem Stream { get; private set; }
    public ParticleSystem Impacts { get; private set; }
    private ParticleSystem streaks, jet, puffs;
    private readonly System.Random random = new System.Random(72413);
    private float emissionRemainder, lastUse = -1, nextSplash, nextRing;
    private readonly float[] nextBurst = new float[6];
    private bool flowing;

    private static readonly Color Dust = new Color32(215, 192, 149, 213);
    private static readonly Color Wind = new Color32(217, 246, 237, 207), WaterBlue = new Color32(83, 195, 232, 241);
    private static readonly Color Foam = new Color32(211, 243, 250, 219), Spark = new Color32(139, 224, 109, 232);

    private void Awake()
    {
        Stream = CreateParticles("Nozzle-bound intake", transform, IntakeLimit);
        PrepareIntake();
        Impacts = CreateParticles("Weapon contact particles", transform, 260);
        streaks = CreateParticles("Wind streaks", transform, 220);
        var r = streaks.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.023f; r.lengthScale = 1.65f;
        jet = CreateParticles("Water jet", transform, 400);
        var jm = jet.main; jm.gravityModifier = 1f;
        puffs = CreateParticles("Soft puffs", transform, 120);
        var pm = puffs.main; pm.gravityModifier = -0.08f;
        var size = puffs.sizeOverLifetime;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(0.4f, 1f), new Keyframe(1, 1.25f)));
        puffs.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.065f;
    }

    public void Show(Flow flow, Vector3 muzzle, Vector3 target, bool contact, Vector3 normal, float seconds)
    {
        if (!EcoRegion.Valid(seconds) || !Finite(muzzle) || !Finite(target)) return;
        Vector3 delta = target - muzzle;
        float distance = delta.magnitude;
        if (distance < 0.06f) return;
        // Restarting the same mode keeps the particles already in the air (a short release no longer blinks the stream).
        if (CurrentFlow != flow) Stop();
        if (!flowing || CurrentFlow != flow) emissionRemainder = 4;
        CurrentFlow = flow; flowing = true; lastUse = Time.time;
        Vector3 direction = delta / distance;
        Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
        Vector3 up = Vector3.Cross(side, direction);
        float dt = Mathf.Min(seconds, 0.1f);
        switch (flow)
        {
            case Flow.Water: WaterJet(muzzle, target, contact, normal, dt); break;
            case Flow.Disperse: Blow(muzzle, target, direction, side, up, distance, contact, normal, dt); break;
            default: Pull(flow, muzzle, target, distance, contact, dt); break;
        }
    }

    // Blow / anti-vacuum: stretched streaks widening from the nozzle; a gust ring where the air meets something.
    private void Blow(Vector3 muzzle, Vector3 target, Vector3 direction, Vector3 side, Vector3 up, float distance, bool contact, Vector3 normal, float dt)
    {
        int count = Take(dt * 62f);
        for (int i = 0; i < count; i++)
        {
            float angle = Range(0, Mathf.PI * 2);
            Vector3 radial = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            Vector3 aimAt = target + radial * Range(0.06f, 0.36f);
            Vector3 position = muzzle + radial * Range(0.01f, 0.06f);
            float speed = Range(14f, 20f);
            Emit(streaks, new ParticleSystem.EmitParams
            {
                position = position, velocity = (aimAt - position).normalized * speed,
                startLifetime = Mathf.Clamp(Vector3.Distance(position, aimAt) / speed, 0.05f, 0.5f),
                startSize = Range(0.043f, 0.079f), startColor = Wind
            });
        }
        if (contact && Time.time >= nextRing)
        {
            nextRing = Time.time + 0.18f;
            Vector3 face = contact && normal.sqrMagnitude > 0.01f ? normal.normalized : -direction;
            Ring(target + face * 0.05f, face, Wind, 9, 1.75f, 0.07f, streaks);
        }
    }

    // Water: droplets thrown on a ballistic arc that lands on the target, so the jet visibly curves.
    private void WaterJet(Vector3 muzzle, Vector3 target, bool contact, Vector3 normal, float dt)
    {
        int count = Take(dt * 112f);
        float g = -Physics.gravity.y;
        float distance = Vector3.Distance(muzzle, target);
        float flight = Mathf.Clamp(distance / 11f, 0.08f, 0.6f);
        for (int i = 0; i < count; i++)
        {
            Vector3 jitter = new Vector3(Range(-1, 1), Range(-1, 1), Range(-1, 1)) * 0.10f;
            Vector3 land = target + jitter;
            Vector3 velocity = (land - muzzle) / flight + Vector3.up * (0.5f * g * flight);
            Emit(jet, new ParticleSystem.EmitParams
            {
                position = muzzle, velocity = velocity, startLifetime = flight * Range(0.97f, 1.03f),
                startSize = Range(0.07f, 0.116f), startColor = i % 4 == 0 ? Foam : WaterBlue
            });
        }
        if (contact && Time.time >= nextSplash)
        {
            nextSplash = Time.time + 0.12f;
            Vector3 face = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            Splash(target, face, Foam, 5, 1.9f);
            Ring(target + face * 0.03f, face, new Color(0.55f, 0.85f, 0.96f, 0.63f), 8, 1.35f, 0.064f, Impacts);
        }
    }

    /// <summary>Feedback tied to a result, not to pressing the button: what the player achieved shows, a wrong mode looks different.</summary>
    public void Pulse(Burst kind, Vector3 at, Color? tint = null)
    {
        if (!Finite(at)) return;
        int k = (int)kind;
        float gap = kind == Burst.Pop || kind == Burst.Splash ? 0f : 0.18f;
        if (Time.time < nextBurst[k]) return;
        nextBurst[k] = Time.time + gap;
        switch (kind)
        {
            case Burst.Hit:
                Splash(at, Vector3.up, tint ?? Dust, 6, 1.65f);
                break;
            case Burst.Fizzle:
                for (int i = 0; i < 4; i++)
                    Emit(puffs, new ParticleSystem.EmitParams
                    {
                        position = at + new Vector3(Range(-0.15f, 0.15f), Range(0f, 0.2f), Range(-0.15f, 0.15f)),
                        velocity = new Vector3(Range(-0.3f, 0.3f), Range(0.3f, 0.7f), Range(-0.3f, 0.3f)),
                        startLifetime = 0.55f, startSize = Range(0.16f, 0.24f), startColor = new Color(0.55f, 0.56f, 0.55f, 0.55f)
                    });
                break;
            case Burst.Steam:
                for (int i = 0; i < 3; i++)
                    Emit(puffs, new ParticleSystem.EmitParams
                    {
                        position = at + new Vector3(Range(-0.25f, 0.25f), Range(0f, 0.15f), Range(-0.25f, 0.25f)),
                        velocity = new Vector3(Range(-0.2f, 0.2f), Range(1.0f, 1.8f), Range(-0.2f, 0.2f)),
                        startLifetime = Range(0.38f, 0.68f), startSize = Range(0.16f, 0.26f), startColor = new Color(0.86f, 0.96f, 0.97f, 0.42f)
                    });
                break;
            case Burst.Grow:
                for (int i = 0; i < 6; i++)
                    Emit(Impacts, new ParticleSystem.EmitParams
                    {
                        position = at + new Vector3(Range(-0.2f, 0.2f), Range(0.05f, 0.3f), Range(-0.2f, 0.2f)),
                        velocity = new Vector3(0f, Range(0.6f, 1.2f), 0f),
                        startLifetime = Range(0.5f, 0.8f), startSize = Range(0.06f, 0.1f), startColor = Spark
                    });
                break;
            case Burst.Pop:
                Gather(at);
                break;
            case Burst.Splash:
                Splash(at, Vector3.up, Foam, 10, 2.6f);
                break;
        }
    }

    public void Shot(Vector3 muzzle, Vector3 direction, bool metal)
    {
        if (!Finite(muzzle) || !Finite(direction)) return;
        Color color = metal ? new Color(1, 0.8f, 0.45f, 0.9f) : new Color(0.65f, 1, 0.3f, 0.9f);
        Splash(muzzle, direction.normalized, color, metal ? 10 : 8, 2f);
        Emit(puffs, new ParticleSystem.EmitParams
        {
            position = muzzle + direction.normalized * 0.05f, velocity = direction.normalized * 1.2f,
            startLifetime = 0.35f, startSize = 0.16f, startColor = new Color(color.r, color.g, color.b, 0.5f)
        });
    }

    private void Splash(Vector3 position, Vector3 normal, Color color, int count, float speed)
    {
        if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
        normal.Normalize();
        for (int i = 0; i < count; i++)
        {
            Vector3 scatter = new Vector3(Range(-1, 1), Range(-1, 1), Range(-1, 1));
            if (Vector3.Dot(scatter, normal) < 0) scatter = -scatter;
            Emit(Impacts, new ParticleSystem.EmitParams
            {
                position = position + normal * 0.025f,
                velocity = (normal + scatter).normalized * Range(speed * 0.4f, speed),
                startLifetime = Range(0.15f, 0.32f), startSize = Range(0.05f, 0.1f), startColor = color
            });
        }
    }

    // A flat expanding ring in the plane facing `normal` (gusts, splashes, pops).
    private void Ring(Vector3 centre, Vector3 normal, Color color, int count, float speed, float size, ParticleSystem system)
    {
        Vector3 a = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
        Vector3 b = Vector3.Cross(normal, a);
        float offset = Range(0, Mathf.PI * 2);
        for (int i = 0; i < count; i++)
        {
            float t = offset + i * Mathf.PI * 2f / count;
            Vector3 dir = a * Mathf.Cos(t) + b * Mathf.Sin(t);
            Emit(system, new ParticleSystem.EmitParams
            {
                position = centre + dir * 0.05f, velocity = dir * speed, startLifetime = 0.28f, startSize = size, startColor = color
            });
        }
    }

    // Keep the marks compact even when the isometric camera zooms in.
    private const float SizeScale = 1.2f;
    private static void Emit(ParticleSystem system, ParticleSystem.EmitParams particle) { particle.startSize *= SizeScale; system.Emit(particle, 1); }

    private int Take(float amount)
    {
        emissionRemainder += amount;
        int count = Mathf.Min(24, Mathf.FloorToInt(emissionRemainder));
        emissionRemainder -= count;
        return count;
    }

    public void Stop(bool clear = true)
    {
        flowing = false; emissionRemainder = 0;
        if (!clear) return;
        intakeCount = 0;
        if (Stream != null) Stream.Clear();
        if (streaks != null) streaks.Clear();
        if (jet != null) jet.Clear();
    }
    private void Update() { if (flowing && Time.time - lastUse > 0.15f) Stop(false); }
    private void OnDisable() => Stop();
    private float Range(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    private static bool Finite(Vector3 value) => EcoRegion.Finite(value.x) && EcoRegion.Finite(value.y) && EcoRegion.Finite(value.z);

    internal static ParticleSystem CreateParticles(string label, Transform parent, int limit)
    {
        var root = new GameObject(label); root.transform.SetParent(parent, false);
        var particles = root.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false; main.loop = true; main.duration = 1; main.maxParticles = limit;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startSpeed = 0; main.startLifetime = 0.2f; main.startSize = 0.05f; main.gravityModifier = 0;
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.enabled = false;
        var fade = particles.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.65f), new Keyframe(0.25f, 1), new Keyframe(1, 0.25f)));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Resources.Load<Material>("EcoEnvironment/Particle");
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.minParticleSize = 0; renderer.maxParticleSize = 0.035f;
        particles.Play(); // Manual emission still needs simulation to move and expire the particles.
        return particles;
    }
}
