using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

// A save identity belongs to a world object, never to its current position or Unity instance ID.
[DisallowMultipleComponent]
public sealed class EcoSaveIdentity : MonoBehaviour
{
    [SerializeField] private string persistentId;
    [SerializeField] private bool initialObject;
    public string Id => persistentId;
    public bool InitialObject => initialObject;
    internal void Assign(string id, bool initial) { persistentId = id; initialObject = initial; }

    internal static EcoSaveIdentity Ensure(GameObject target, bool initial = false)
    {
        var identity = target.GetComponent<EcoSaveIdentity>() ?? target.AddComponent<EcoSaveIdentity>();
        if (string.IsNullOrEmpty(identity.persistentId))
            identity.Assign(initial ? ScenePath(target.transform) : "runtime:" + Guid.NewGuid().ToString("N"), initial);
        return identity;
    }

    internal static string ScenePath(Transform target)
    {
        string path = "";
        for (var node = target; node != null; node = node.parent)
            path = "/" + node.GetSiblingIndex() + ":" + node.name + path;
        return target.gameObject.scene.path + path;
    }

    // This adapter deliberately serializes only value fields. Unity object references and instance IDs cannot
    // survive a restart; references between structures are saved separately as persistent endpoint identities.
    [Serializable] internal sealed class Value
    {
        public string name, text;
        public double number;
        public long integer;
        public bool flag, relativeTime;
        public Vector3 vector;
        public Quaternion rotation;
        public int[] integers;
        public float[] numbers;
        public bool[] flags;
        public string[] texts;
    }

    [Serializable] internal sealed class State
    {
        public string type;
        public bool enabled;
        public Value[] fields;
    }

    // Explicit private state is part of schema 2. Keeping these fields here also preserves older components'
    // public API and avoids reflection over collections of live scene references, animation or navigation state.
    private static readonly Dictionary<string, string[]> PrivateState = new Dictionary<string, string[]>
    {
        { "EnemyBrain", new[] { "Health", "State", "FilterInstalled", "stunnedUntil", "home", "patrolGoal", "nextAttack", "provokedUntil", "lastKnownPosition", "chasingPlayer" } },
        { "PlayerVitals", new[] { "Energy", "IsRecovering", "recoveryElapsed", "slowedUntil", "smokeUntil", "shieldUntil" } },
        { "PlantedSeed", new[] { "dryTime", "SlimeRemaining", "ContaminationExposure" } },
        { "EcoPowerNode", new[] { "SmokeRemaining", "smokeEfficiency" } },
        { "EcoWaterNode", new[] { "SlimeRemaining", "slimeRateMultiplier" } },
        { "EcoEnemySpawnZone", new[] { "SpawnedToday", "BudgetDay", "elapsed", "nextSpawn", "initialized" } },
        { "EcoFactory", new[] { "filtered", "converted", "cleanConversion", "conversionInstalled" } },
        { "EcoGlacierStation", new[] { "Active" } },
        { "EcoGlacierMelt", new[] { "meltNow", "meltTarget" } },
        { "EcoFinale", new[] { "terminalEnergy", "terminalWater" } },
        { "WeaponWaterSource", new[] { "localPollution" } },
        { "EcoProjects", new[] { "wasBurning", "charged", "chargedEnergy", "ChargedEnergy" } },
        { "EcoChargingStation", new[] { "TotalDeliveredEnergy" } },
        { "EcoStory", new[] { "completed", "cleanHarvest", "replanted", "cleanFish", "intro", "finalConversation", "requestedHint" } },
        { "EcoDiscoveries", new[] { "discovered" } },
        { "EcoEquipmentUpgrades", new[] { "ranks", "levels" } },
    };
    private static readonly HashSet<string> RelativeTimes = new HashSet<string>
        { "stunnedUntil", "nextAttack", "provokedUntil", "slowedUntil", "smokeUntil", "shieldUntil" };

    internal static State Capture(Component component)
    {
        var type = component.GetType();
        var values = new List<Value>();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral) continue;
            string name = PropertyName(field.Name);
            if (!field.IsPublic && (!PrivateState.TryGetValue(type.Name, out var allowed) || Array.IndexOf(allowed, name) < 0)) continue;
            var value = Encode(field.Name, field.FieldType, field.GetValue(component));
            if (value != null) values.Add(value);
        }
        return new State { type = type.FullName, enabled = !(component is Behaviour b) || b.enabled, fields = values.ToArray() };
    }

    internal static void Apply(Component component, State state)
    {
        var type = component.GetType();
        foreach (var value in state.fields ?? Array.Empty<Value>())
        {
            var field = type.GetField(value.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field == null || field.IsInitOnly || field.IsStatic) continue;
            object decoded = Decode(field.FieldType, value);
            if (decoded != null) field.SetValue(component, decoded);
        }
        if (component is Behaviour behaviour) behaviour.enabled = state.enabled;
    }

    internal static Value Encode(string name, Type type, object raw)
    {
        if (raw == null) return null;
        var value = new Value { name = name };
        if (type == typeof(float) || type == typeof(double))
        {
            value.number = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            if (double.IsNaN(value.number) || double.IsInfinity(value.number)) return null;
            value.relativeTime = RelativeTimes.Contains(PropertyName(name));
            if (value.relativeTime) value.number = Math.Max(0, value.number - Time.time);
        }
        else if (type.IsEnum || type == typeof(int) || type == typeof(long)) value.integer = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
        else if (type == typeof(bool)) value.flag = (bool)raw;
        else if (type == typeof(string)) value.text = (string)raw;
        else if (type == typeof(Vector3)) value.vector = (Vector3)raw;
        else if (type == typeof(Quaternion)) value.rotation = (Quaternion)raw;
        else if (type == typeof(int[])) value.integers = (int[])((int[])raw).Clone();
        else if (type == typeof(float[])) value.numbers = (float[])((float[])raw).Clone();
        else if (type == typeof(bool[])) value.flags = (bool[])((bool[])raw).Clone();
        else if (type == typeof(string[])) value.texts = (string[])((string[])raw).Clone();
        else return null;
        return value;
    }

    internal static object Decode(Type type, Value value)
    {
        if (type == typeof(float)) return (float)(value.number + (value.relativeTime && value.number > 0 ? Time.time : 0));
        if (type == typeof(double)) return value.number + (value.relativeTime && value.number > 0 ? Time.time : 0);
        if (type.IsEnum) return Enum.ToObject(type, value.integer);
        if (type == typeof(int)) return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value.integer));
        if (type == typeof(long)) return value.integer;
        if (type == typeof(bool)) return value.flag;
        if (type == typeof(string)) return value.text ?? "";
        if (type == typeof(Vector3)) return value.vector;
        if (type == typeof(Quaternion)) return value.rotation;
        if (type == typeof(int[])) return value.integers ?? Array.Empty<int>();
        if (type == typeof(float[])) return value.numbers ?? Array.Empty<float>();
        if (type == typeof(bool[])) return value.flags ?? Array.Empty<bool>();
        if (type == typeof(string[])) return value.texts ?? Array.Empty<string>();
        return null;
    }

    private static string PropertyName(string name)
    {
        int end = name.IndexOf('>');
        return name.StartsWith("<", StringComparison.Ordinal) && end > 1 ? name.Substring(1, end - 1) : name;
    }
}
