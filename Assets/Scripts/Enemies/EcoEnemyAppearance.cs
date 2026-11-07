using UnityEngine;

// The animated presentation follows the real brain. Its poses never move the hitbox or attack origin.
internal sealed class EcoEnemyAppearance : MonoBehaviour
{
    private EnemyBrain enemy;
    private CharacterController motor;
    private Transform rig;
    private EcoEnemySculpture.Node[] parts;
    private float gait, travel, pulse, nextHit, phaseOffset, defeated;
    private float nextBlink = 2.2f;
    private float blink;

    internal static EcoEnemyAppearance Install(EnemyBrain enemy, CharacterController motor)
    {
        var art = EcoEnemySculpture.For(enemy.species);
        if (art == null) return null;

        // Capture the old renderers before the new hierarchy is attached.
        var previous = enemy.GetComponentsInChildren<Renderer>(true);
        var visual = enemy.gameObject.AddComponent<EcoEnemyAppearance>();
        visual.enemy = enemy;
        visual.motor = motor;
        visual.phaseOffset = Mathf.Repeat(enemy.GetInstanceID() * 0.1316f, 6.28f);
        visual.rig = new GameObject("EcoQuest creature rig").transform;
        visual.rig.SetParent(enemy.transform, false);
        visual.rig.localPosition = motor.center - Vector3.up * (motor.height * 0.5f);
        visual.rig.localScale = Vector3.one * motor.height;
        visual.parts = art.Mount(visual.rig);
        foreach (var renderer in previous) renderer.enabled = false;
        return visual;
    }

    internal void Hit()
    {
        if (Time.time < nextHit) return;
        nextHit = Time.time + 0.19f;
        pulse = 1f;
    }

    internal void Defeat(float progress) => defeated = progress;

    private void LateUpdate()
    {
        if (enemy == null || Time.deltaTime <= 0f) return;

        float speed = motor.enabled ? Vector3.ProjectOnPlane(motor.velocity, Vector3.up).magnitude : 0;
        travel = Mathf.MoveTowards(travel, Mathf.Clamp01(speed / Mathf.Max(0.1f, enemy.chaseSpeed)), Time.deltaTime * 6f);
        gait += Time.deltaTime * Mathf.Lerp(3f, 11f, travel);
        pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 5.8f);
        float clock = Time.time + phaseOffset;
        float breath = Mathf.Sin(clock * 3.1f);
        float elapsed = enemy.AttackAge;
        bool attacking = enemy.State == EnemyBrain.Behaviour.Attack;
        float anticipation = attacking ? Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(0.08f, enemy.windup)) : 0;
        float follow = attacking ? Mathf.Max(0, elapsed - enemy.windup) : 0;
        float accent = attacking && elapsed >= enemy.windup
            ? Mathf.SmoothStep(0, 1, follow / 0.055f) * Mathf.Exp(-follow * 5.5f) : 0;
        anticipation *= 1f - Mathf.Clamp01(follow * 9f);
        float stunned = enemy.IsStunned ? 1f : 0;
        float hit = Mathf.Sin(pulse * Mathf.PI * 0.7f);
        float s = Mathf.Sin(gait);
        float c = Mathf.Cos(gait);
        float gaitBob = Mathf.Abs(s) * travel;

        blink -= Time.deltaTime;
        nextBlink -= Time.deltaTime;
        if (nextBlink <= 0f)
        {
            blink = 0.18f;
            nextBlink = 2.7f + 0.7f * Mathf.Sin(clock * 0.6f);
        }
        float blinkAmount = blink > 0f ? Mathf.Sin(blink * Mathf.PI / 0.18f) : 0;

        switch (enemy.species)
        {
            case EnemyBrain.Species.Slime:
                Body(new Vector3(s * travel * 0.021f, 0.012f * breath + gaitBob * 0.036f, accent * 0.12f),
                    new Vector3(-16f * accent + 12f * anticipation + hit * 7f, 0, s * travel * 6f),
                    0.025f * breath + anticipation * 0.075f - accent * 0.12f + hit * 0.08f);
                Arm(EcoEnemySculpture.Joint.LeftArm, c * travel * 22f - accent * 44f, -10f * anticipation);
                Arm(EcoEnemySculpture.Joint.RightArm, -c * travel * 22f - accent * 44f, 10f * anticipation);
                WalkFoot(EcoEnemySculpture.Joint.LeftFoot, s, c, 0.048f, 0.113f);
                WalkFoot(EcoEnemySculpture.Joint.RightFoot, -s, -c, 0.048f, 0.113f);
                Pose(EcoEnemySculpture.Joint.Crest, Vector3.up * (0.009f * breath), new Vector3(-9f * accent, 0, 4f * s * travel));
                break;
            case EnemyBrain.Species.Flame:
                Body(new Vector3(s * travel * 0.019f, gaitBob * 0.054f + 0.008f * breath, accent * 0.072f),
                    new Vector3(10f * anticipation - 20f * accent, 0, s * travel * 7f - hit * 13f),
                    anticipation * 0.035f - accent * 0.07f);
                WalkFoot(EcoEnemySculpture.Joint.LeftFoot, s, c, 0.072f, 0.105f);
                WalkFoot(EcoEnemySculpture.Joint.RightFoot, -s, -c, 0.072f, 0.105f);
                Arm(EcoEnemySculpture.Joint.LeftArm, 24f * c * travel + 40f * anticipation - 60f * accent, -14f * anticipation);
                Arm(EcoEnemySculpture.Joint.RightArm, -24f * c * travel + 40f * anticipation - 60f * accent, 14f * anticipation);
                Pose(EcoEnemySculpture.Joint.Crest, Vector3.up * (breath * 0.017f + anticipation * 0.07f), new Vector3(18f * anticipation - 31f * accent, 0, Mathf.Sin(clock * 7f) * 8f),
                    new Vector3(1f + anticipation * 0.1f, 1f + 0.06f * breath + anticipation * 0.15f, 1f));
                Pose(EcoEnemySculpture.Joint.Trail, new Vector3(Mathf.Sin(clock * 4f) * 0.026f, Mathf.Sin(clock * 5.8f) * 0.039f, 0), new Vector3(0, clock * 45f, 0));
                break;
            case EnemyBrain.Species.Smoke:
                Body(new Vector3(0, breath * 0.034f - anticipation * 0.025f, accent * 0.17f),
                    new Vector3(-travel * 13f + 18f * anticipation - 29f * accent, 0, Mathf.Sin(clock * 2.3f) * 8f + hit * 14f),
                    0.043f * breath - accent * 0.17f);
                Pose(EcoEnemySculpture.Joint.Trail, Vector3.back * (travel * 0.09f + accent * 0.19f), new Vector3(travel * 26f, Mathf.Sin(clock * 2.7f) * 23f, Mathf.Sin(clock * 3.7f) * 13f));
                Pose(EcoEnemySculpture.Joint.Crest, Vector3.up * (0.023f * breath), new Vector3(5f * breath, 8f * breath, 8f * breath));
                break;
            case EnemyBrain.Species.Acid:
                Body(new Vector3(Mathf.Sin(clock * 2.2f) * 0.025f, breath * 0.036f + anticipation * 0.04f, accent * 0.095f),
                    new Vector3(11f * anticipation - 15f * accent, 0, Mathf.Sin(clock * 2.4f) * 7f + hit * 12f),
                    anticipation * 0.075f - accent * 0.1f);
                for (int i = 0; i < 3; i++)
                {
                    float cycle = Mathf.Repeat(clock * 1.9f + i * 0.31f, 1);
                    float growth = Mathf.Sin(cycle * Mathf.PI);
                    float size = Mathf.Lerp(0.45f, 1.02f, growth);
                    Pose(EcoEnemySculpture.Joint.LeftArm + i, Vector3.down * (cycle * 0.103f + accent * 0.052f),
                        new Vector3(0, 0, Mathf.Sin(clock * 5f + i) * 7f), new Vector3(size, size * (1 + growth * 0.24f), size));
                }
                break;
            case EnemyBrain.Species.Tin:
                Body(new Vector3(c * travel * 0.022f, gaitBob * 0.031f - stunned * 0.07f, accent * -0.048f),
                    new Vector3(4f + travel * 6f + stunned * 14f - accent * 15f, 0, s * travel * 8f + hit * 8f - stunned * 12f), 0);
                WalkFoot(EcoEnemySculpture.Joint.LeftFoot, s, c, 0.067f, 0.115f);
                WalkFoot(EcoEnemySculpture.Joint.RightFoot, -s, -c, 0.067f, 0.115f);
                Arm(EcoEnemySculpture.Joint.LeftArm, 23f * c * travel - anticipation * 49f + accent * 28f + stunned * 25f, -stunned * 14f);
                Arm(EcoEnemySculpture.Joint.RightArm, -23f * c * travel - anticipation * 49f + accent * 28f + stunned * 25f, stunned * 14f);
                Pose(EcoEnemySculpture.Joint.Crest, Vector3.zero, new Vector3(9f * breath + 31f * stunned, s * 8f * travel, -13f * hit));
                break;
        }

        Pose(EcoEnemySculpture.Joint.Eyes, Vector3.zero, Vector3.zero, new Vector3(1, Mathf.Lerp(1f, 0.16f, Mathf.Max(stunned * 0.65f, blinkAmount)), 1));
        var filter = parts[(int)EcoEnemySculpture.Joint.Filter].Transform;
        if (filter != null && filter.gameObject.activeSelf != enemy.FilterInstalled) filter.gameObject.SetActive(enemy.FilterInstalled);
        if (defeated > 0f) PlayDefeat();
    }

    private void WalkFoot(EcoEnemySculpture.Joint foot, float stride, float lift, float height, float distance)
    {
        Pose(foot, new Vector3(0, Mathf.Max(0f, lift) * travel * height, stride * travel * distance),
            new Vector3(stride * travel * 17f, 0, 0));
    }

    private void Arm(EcoEnemySculpture.Joint arm, float swing, float spread)
        => Pose(arm, Vector3.zero, new Vector3(swing, 0, spread));

    private void Body(Vector3 offset, Vector3 euler, float squash)
        => Pose(EcoEnemySculpture.Joint.Body, offset, euler, new Vector3(1f + squash * 0.62f, 1f - squash, 1f + squash * 0.62f));

    private void Pose(EcoEnemySculpture.Joint key, Vector3 offset, Vector3 euler, Vector3? scale = null)
    {
        var part = parts[(int)key];
        if (part.Transform == null) return;
        part.Transform.localPosition = part.Rest + offset;
        part.Transform.localRotation = Quaternion.Euler(euler);
        part.Transform.localScale = scale ?? Vector3.one;
    }

    private void PlayDefeat()
    {
        float down = Mathf.SmoothStep(0f, 1f, defeated);
        float vanish = Mathf.InverseLerp(0.42f, 1f, defeated);
        float tilt = enemy.species == EnemyBrain.Species.Tin ? 64f : 19f;
        Body(Vector3.down * (enemy.species == EnemyBrain.Species.Smoke || enemy.species == EnemyBrain.Species.Acid ? down * 0.34f : down * 0.11f),
            new Vector3(-tilt * down, 0, 14f * down), down * 0.46f);
        rig.localScale = Vector3.one * motor.height * Mathf.Lerp(1f, 0.035f, vanish * vanish);
    }
}
