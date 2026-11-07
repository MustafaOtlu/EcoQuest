using UnityEngine;

public sealed partial class WeaponVisualEffects
{
    private const int IntakeLimit = 144;
    private static readonly Color Air = new Color32(165, 220, 192, 150);
    private static readonly Color Scrap = new Color32(232, 205, 155, 249);
    private static readonly Color Enamel = new Color32(134, 181, 175, 238);
    private readonly IntakeMote[] intake = new IntakeMote[IntakeLimit];
    private readonly ParticleSystem.Particle[] intakeParticles = new ParticleSystem.Particle[IntakeLimit];
    private FirstPersonWeaponHolder muzzleHolder;
    private Vector3 lastIntakeMuzzle;
    private int intakeCount;
    private Material intakeMaterial;
    private Texture2D intakeStamp;
    private IntakeWisps wisps;

    private struct IntakeMote
    {
        public Vector3 Source;
        public float Age, Duration, Radius, Angle, Turn, Size, Spin;
        public Color Tint;
        public bool Wisp;
    }

    internal void BindMuzzle(FirstPersonWeaponHolder holder) => muzzleHolder = holder;

    private void PrepareIntake()
    {
        var renderer = Stream.GetComponent<ParticleSystemRenderer>();
        renderer.maxParticleSize = 0.024f;
        wisps = IntakeWisps.Create(transform);
        if (renderer.sharedMaterial == null) return;
        intakeStamp = new Texture2D(32, 32, TextureFormat.RGBA32, false)
        {
            name = "Intake cut-paper fleck",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float u = (x + 0.5f - 16f) / 16f, v = (y + 0.5f - 16f) / 16f;
            float contour = Mathf.Abs(u) + 1.65f * Mathf.Abs(v + u * 0.18f);
            byte alpha = (byte)(255 * Mathf.Clamp01((0.97f - contour) * 9f));
            byte value = (byte)Mathf.Lerp(180f, 255f, Mathf.Clamp01(0.5f + v));
            pixels[y * 32 + x] = new Color32(value, value, value, alpha);
        }
        intakeStamp.SetPixels32(pixels);
        intakeStamp.Apply(false, true);
        intakeMaterial = new Material(renderer.sharedMaterial) { name = "Matte intake flecks" };
        intakeMaterial.SetTexture("_BaseMap", intakeStamp);
        renderer.sharedMaterial = intakeMaterial;
    }

    private void Pull(Flow flow, Vector3 muzzle, Vector3 target, float distance, bool contact, float seconds)
    {
        lastIntakeMuzzle = muzzle;
        int count = Take(seconds * (contact ? 78f : 43f));
        for (int i = 0; i < count && intakeCount < intake.Length; i++)
        {
            bool fragment = contact && Range(0f, 1f) < 0.38f;
            float fraction = fragment ? Range(0.86f, 1f) : Range(0.38f, 1f);
            var tint = flow == Flow.Fill ? Color.Lerp(Foam, WaterBlue, Range(0.32f, 0.9f))
                : flow == Flow.Recycle ? Color.Lerp(Air, Spark, Range(0.42f, 1f))
                : fragment ? Color.Lerp(Enamel, Scrap, Range(0f, 1f)) : Air;
            AddIntake(Vector3.Lerp(muzzle, target, fraction),
                Mathf.Clamp(distance * fraction / Range(8.5f, 12.2f), 0.16f, 0.60f),
                Mathf.Min(distance * 0.15f, contact ? 0.48f : 0.39f) * fraction * Range(0.25f, 1f),
                fragment ? Range(0.11f, 0.161f) : Range(0.086f, 0.139f), tint, !fragment && Range(0f, 1f) < 0.58f);
        }
    }

    private void Gather(Vector3 source)
    {
        var mouth = IntakeMuzzle;
        float distance = Vector3.Distance(source, mouth);
        Color tint = CurrentFlow == Flow.Recycle ? Spark : Scrap;
        for (int i = 0; i < 9 && intakeCount < intake.Length; i++)
            AddIntake(source, Mathf.Clamp(distance / Range(9f, 13f), 0.17f, 0.48f),
                Mathf.Min(distance * 0.17f, Range(0.08f, 0.32f)), Range(0.11f, 0.18f), tint, i % 3 == 0);
    }

    private void AddIntake(Vector3 source, float duration, float radius, float size, Color tint, bool wisp)
    {
        intake[intakeCount++] = new IntakeMote
        {
            Source = source, Duration = duration, Radius = radius, Angle = Range(0f, 2f * Mathf.PI),
            Turn = Range(2.2f, 3.65f), Size = size, Spin = Range(-145f, 145f), Tint = tint, Wisp = wisp
        };
    }

    private Vector3 IntakeMuzzle => muzzleHolder != null && muzzleHolder.IsHandMounted && muzzleHolder.EquippedWeapon != null
        ? muzzleHolder.MuzzlePosition : lastIntakeMuzzle;

    // A bounded path is recomputed against the animated nozzle every frame.
    // Its longitudinal distance and radius both reach zero at t=1.
    internal static Vector3 IntakePoint(Vector3 source, Vector3 mouth, float t, float radius, float angle, float turn)
    {
        t = Mathf.Clamp01(t);
        Vector3 axis = source - mouth;
        if (axis.sqrMagnitude < 0.0001f) return mouth;
        Vector3 forward = axis.normalized;
        Vector3 right = Vector3.Cross(forward, Mathf.Abs(forward.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 up = Vector3.Cross(forward, right);
        float remaining = 1f - t;
        float coil = angle + turn * t;
        Vector3 radial = right * Mathf.Cos(coil) + up * Mathf.Sin(coil);
        return mouth + axis * (remaining * (1f + 0.25f * t)) + radial * (radius * remaining * remaining);
    }

    private void LateUpdate()
    {
        if (intakeCount == 0 || Time.deltaTime <= 0f) return;
        Vector3 mouth = IntakeMuzzle;
        var view = EcoIsoCamera.Cam;
        Vector3 screenNormal = view != null ? view.transform.forward : Vector3.down;
        wisps?.Begin(CurrentFlow == Flow.Fill ? WaterBlue : CurrentFlow == Flow.Recycle ? Spark : Air);
        for (int i = 0; i < intakeCount;)
        {
            var mote = intake[i];
            float t = mote.Age / mote.Duration;
            if (t >= 1f)
            {
                intake[i] = intake[--intakeCount];
                continue;
            }
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 12f))
                * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) * 6f));
            Color color = mote.Tint;
            color.a *= alpha;
            Vector3 head = IntakePoint(mote.Source, mouth, t, mote.Radius, mote.Angle, mote.Turn);
            intakeParticles[i] = new ParticleSystem.Particle
            {
                position = head,
                startColor = color, startSize = mote.Size * Mathf.Lerp(1f, 0.36f, t),
                startLifetime = 1f, remainingLifetime = 0.98f,
                rotation = mote.Angle * Mathf.Rad2Deg + mote.Spin * t,
                randomSeed = (uint)(i + 1)
            };
            if (mote.Wisp && alpha > 0.08f) wisps?.Add(mote, mouth, t, head, screenNormal, alpha);
            mote.Age += Time.deltaTime;
            intake[i++] = mote;
        }
        Stream.SetParticles(intakeParticles, intakeCount);
        wisps?.Finish();
    }

    private void OnDestroy()
    {
        if (intakeMaterial != null) Destroy(intakeMaterial);
        if (intakeStamp != null) Destroy(intakeStamp);
        wisps?.Dispose();
    }
}
