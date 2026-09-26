using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(40)]
public sealed class EcoWaterGrid : MonoBehaviour
{
    private void Update() => Simulate(Time.deltaTime);
    public void Simulate(float seconds)
    {
        if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        var available = new Dictionary<EcoWaterNode, Vector2>();
        var space = new Dictionary<EcoWaterNode, float>();
        foreach (var node in EcoWaterNode.Active)
        {
            if (node == null || !node.Operational) continue;
            available[node] = new Vector2(node.dirtyWater, node.cleanWater); space[node] = node.Space;
        }
        // Snapshot budgets prevent cycles from circulating this frame's incoming water.
        // First divide scarce output between branches, then reserve shared inlet capacity.
        var requests = new Dictionary<EcoWaterPipe, float>();
        var outgoing = new Dictionary<EcoWaterNode, Vector2>();
        var incoming = new Dictionary<EcoWaterNode, float>();
        foreach (var pipe in EcoWaterPipe.Active.Where(p => p != null))
        {
            pipe.LastFlow = 0;
            if (!pipe.IsConnected || !available.ContainsKey(pipe.from) || !space.ContainsKey(pipe.to)) continue;
            int channel = (int)pipe.fluid;
            float amount = Mathf.Min(Mathf.Max(0, pipe.litresPerSecond) * seconds, available[pipe.from][channel], space[pipe.to]);
            if (amount <= 0 || float.IsNaN(amount)) continue;
            requests[pipe] = amount;
            outgoing.TryGetValue(pipe.from, out var total); total[channel] += amount; outgoing[pipe.from] = total;
        }
        foreach (var pipe in requests.Keys.ToArray())
        {
            int channel = (int)pipe.fluid;
            requests[pipe] *= Mathf.Min(1, available[pipe.from][channel] / outgoing[pipe.from][channel]);
            incoming.TryGetValue(pipe.to, out float total); incoming[pipe.to] = total + requests[pipe];
        }
        foreach (var request in requests)
        {
            var pipe = request.Key;
            float amount = request.Value * Mathf.Min(1, space[pipe.to] / incoming[pipe.to]);
            float taken = pipe.from.Take(pipe.fluid, amount);
            amount = pipe.to.Add(pipe.fluid, taken);
            if (taken > amount) pipe.from.Add(pipe.fluid, taken - amount);
            pipe.LastFlow = amount / seconds;
        }
    }
}
