using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class EcoWaterPipe : MonoBehaviour
{
    internal static readonly HashSet<EcoWaterPipe> Active = new HashSet<EcoWaterPipe>();
    public EcoWaterNode from, to;
    public EcoWaterNode.Fluid fluid;
    public bool connected = true, playerBuilt;
    public int materialCost;
    public float litresPerSecond = 8;
    public float LastFlow { get; internal set; }
    public bool IsConnected => connected && isActiveAndEnabled && from != null && to != null && from != to && from.CanSend(fluid) && to.CanReceive(fluid);
    private LineRenderer line;
    private void OnEnable() => Active.Add(this);
    private void OnDisable() { Active.Remove(this); LastFlow = 0; }
    private void Awake() => line = GetComponent<LineRenderer>();
    private void LateUpdate()
    {
        if (line == null) return;
        line.enabled = connected && from != null && to != null;
        if (!line.enabled) return;
        line.useWorldSpace = true; line.positionCount = 4;
        var a = from.SocketPosition; var b = to.SocketPosition;
        float ground = Mathf.Min(from.transform.position.y, to.transform.position.y) + 0.1f;
        line.SetPositions(new[] { a, new Vector3(a.x, ground, a.z), new Vector3(b.x, ground, b.z), b });
        line.startColor = line.endColor = fluid == EcoWaterNode.Fluid.Clean ? new Color(0.15f, 0.7f, 1) : new Color(0.65f, 0.4f, 0.15f);
    }
}
