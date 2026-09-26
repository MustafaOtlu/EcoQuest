using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(30)]
public sealed class EcoPowerGrid : MonoBehaviour
{
    public EcoWorldClock clock;
    private void Awake() { if (clock == null) clock = FindFirstObjectByType<EcoWorldClock>(); }
    private void Update() => Simulate(Time.deltaTime);

    public void Simulate(float seconds)
    {
        if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        foreach (var node in EcoPowerNode.Active)
            if (node != null) node.DeliveredPower = node.NetworkGeneration = node.NetworkDemand = node.NetworkStored = node.NetworkCapacity = 0f;
        var nodes = EcoPowerNode.Active.Where(n => n != null && n.Operational).OrderBy(n => n.GetInstanceID()).ToArray();
        var adjacency = new Dictionary<EcoPowerNode, List<EcoPowerNode>>();
        foreach (var node in nodes)
        {
            adjacency[node] = new List<EcoPowerNode>();
            node.DeliveredPower = 0f;
            node.storedEnergy = Mathf.Clamp(node.storedEnergy, 0f, Mathf.Max(0f, node.capacity));
        }
        foreach (var cable in EcoPowerCable.Active)
        {
            if (cable == null || !cable.IsConnected || !adjacency.ContainsKey(cable.a) || !adjacency.ContainsKey(cable.b)) continue;
            adjacency[cable.a].Add(cable.b); adjacency[cable.b].Add(cable.a);
        }
        var visited = new HashSet<EcoPowerNode>();
        foreach (var start in nodes)
        {
            if (!visited.Add(start)) continue;
            var circuit = new List<EcoPowerNode>();
            var queue = new Queue<EcoPowerNode>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue(); circuit.Add(node);
                foreach (var neighbor in adjacency[node]) if (visited.Add(neighbor)) queue.Enqueue(neighbor);
            }
            SupplyCircuit(circuit, seconds);
        }
    }

    private void SupplyCircuit(List<EcoPowerNode> circuit, float seconds)
    {
        float generated = 0f, stored = 0f, capacity = 0f, demand = 0f;
        var requests = new Dictionary<EcoPowerNode, float>();
        foreach (var node in circuit)
        {
            generated += Mathf.Max(0f, node.Generation(clock)) * seconds;
            if (node.kind == EcoPowerNode.Kind.Battery) { stored += node.storedEnergy; capacity += node.capacity; }
            float requested = Mathf.Max(0f, node.Demand(seconds));
            requests[node] = requested; demand += requested;
        }
        float fraction = demand > 0f ? Mathf.Clamp01((generated + stored) / demand) : 0f;
        float delivered = 0f;
        foreach (var node in circuit) delivered += node.Supply(requests[node] * fraction, seconds);
        float balance = generated - delivered;
        foreach (var battery in circuit)
        {
            if (battery.kind != EcoPowerNode.Kind.Battery) continue;
            if (balance >= 0f)
            {
                float amount = Mathf.Min(balance, battery.capacity - battery.storedEnergy);
                battery.storedEnergy += amount; balance -= amount;
            }
            else
            {
                float amount = Mathf.Min(-balance, battery.storedEnergy);
                battery.storedEnergy -= amount; balance += amount;
            }
        }
        stored = 0f;
        foreach (var node in circuit) if (node.kind == EcoPowerNode.Kind.Battery) stored += node.storedEnergy;
        foreach (var node in circuit)
        {
            node.NetworkGeneration = generated / seconds; node.NetworkDemand = demand / seconds;
            node.NetworkStored = stored; node.NetworkCapacity = capacity;
        }
    }
}
