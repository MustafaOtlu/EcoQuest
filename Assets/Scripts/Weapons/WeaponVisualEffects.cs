using UnityEngine;
using UnityEngine.Rendering;

// Cosmetic particles never take part in weapon hit detection or consume gameplay randomness.
public sealed class WeaponVisualEffects : MonoBehaviour
{
    public enum Flow { Water, Collect, Disperse, Recycle }
    public Flow CurrentFlow { get; private set; }
    public ParticleSystem Stream { get; private set; }
    public ParticleSystem Impacts { get; private set; }
    private readonly System.Random random = new System.Random(72413);
    private float emissionRemainder, lastUse = -1, nextSplash;
    private bool flowing;

    private void Awake()
    {
        Stream = CreateParticles("Moving droplets and airflow", transform, 300);
        Impacts = CreateParticles("Weapon contact particles", transform, 120);
    }

    public void Show(Flow flow, Vector3 muzzle, Vector3 target, bool contact, Vector3 normal, float seconds)
    {
        if (!EcoRegion.Valid(seconds) || !Finite(muzzle) || !Finite(target)) return;
        Vector3 delta = target - muzzle;
        float distance = delta.magnitude;
        if (distance < 0.06f) return;
        if (!flowing || CurrentFlow != flow) { Stop(); emissionRemainder = 3; }
        CurrentFlow = flow; flowing = true; lastUse = Time.time;
        bool water = flow == Flow.Water, inward = flow == Flow.Collect || flow == Flow.Recycle;
        Vector3 direction = delta / distance;
        Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
        Vector3 up = Vector3.Cross(side, direction);
        float speed = water ? 22 : inward ? 12 : 17;
        Color color = water ? new Color(0.5f, 0.85f, 1, 0.85f)
            : flow == Flow.Recycle ? new Color(0.5f, 0.95f, 0.45f, 0.7f)
            : inward ? new Color(0.9f, 0.85f, 0.6f, 0.6f) : new Color(0.9f, 0.96f, 1, 0.5f);
        emissionRemainder += Mathf.Min(seconds, 0.1f) * (water ? 140 : 65);
        int count = Mathf.Min(20, Mathf.FloorToInt(emissionRemainder));
        emissionRemainder -= count;
        for (int i = 0; i < count; i++)
        {
            float fraction = inward ? Range(0.15f, 0.95f) : 0;
            float spread = water ? 0.025f : Mathf.Lerp(0.04f, 0.32f, fraction);
            float angle = Range(0, Mathf.PI * 2);
            Vector3 radial = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            Vector3 position = muzzle + direction * (distance * fraction) + radial * spread * Range(0.3f, 1);
            Vector3 destination = inward ? muzzle : target + radial * (water ? 0.045f : 0.15f);
            float travel = Mathf.Max(0.02f, Vector3.Distance(position, destination));
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = (destination - position).normalized * speed + (!water && inward ? Vector3.Cross(direction, radial) * 0.45f : Vector3.zero),
                startLifetime = Mathf.Clamp(travel / speed, 0.025f, 0.65f),
                startSize = water ? Range(0.035f, 0.065f) : Range(0.045f, 0.105f),
                startColor = color
            };
            Stream.Emit(particle, 1);
        }
        if (water && contact && Time.time >= nextSplash)
        {
            nextSplash = Time.time + 0.06f;
            Splash(target, normal, color, 7, 1.6f);
        }
    }

    public void Shot(Vector3 muzzle, Vector3 direction, bool metal)
    {
        if (!Finite(muzzle) || !Finite(direction)) return;
        Color color = metal ? new Color(1, 0.8f, 0.45f, 0.8f) : new Color(0.65f, 1, 0.3f, 0.85f);
        Splash(muzzle, direction.normalized, color, metal ? 7 : 5, 1.1f);
    }

    private void Splash(Vector3 position, Vector3 normal, Color color, int count, float speed)
    {
        if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
        normal.Normalize();
        for (int i = 0; i < count; i++)
        {
            Vector3 scatter = new Vector3(Range(-1, 1), Range(-1, 1), Range(-1, 1));
            if (Vector3.Dot(scatter, normal) < 0) scatter = -scatter;
            var particle = new ParticleSystem.EmitParams
            {
                position = position + normal * 0.025f,
                velocity = (normal + scatter).normalized * Range(speed * 0.4f, speed),
                startLifetime = Range(0.1f, 0.23f), startSize = Range(0.025f, 0.06f), startColor = color
            };
            Impacts.Emit(particle, 1);
        }
    }

    public void Stop(bool clear = true)
    {
        flowing = false; emissionRemainder = 0;
        if (!clear) return;
        if (Stream != null) Stream.Clear();
        if (Impacts != null) Impacts.Clear();
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
