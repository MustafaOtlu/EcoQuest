using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class EcoPowerCable : MonoBehaviour
{
    internal static readonly HashSet<EcoPowerCable> Active = new HashSet<EcoPowerCable>();
    public EcoPowerNode a, b;
    public bool connected = true;
    public bool playerBuilt;
    public int materialCost;
    public bool IsConnected => connected && isActiveAndEnabled && a != null && b != null && a != b && a.Operational && b.Operational;
    private LineRenderer wire;
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
    private void Awake() => wire = GetComponent<LineRenderer>();
    private void LateUpdate()
    {
        if (wire == null) return;
        wire.enabled = a != null && b != null && connected;
        if (!wire.enabled) return;
        wire.useWorldSpace = true; wire.positionCount = 4;
        wire.SetPosition(0, a.SocketPosition);
        wire.SetPosition(1, new Vector3(a.SocketPosition.x, Mathf.Min(a.transform.position.y, b.transform.position.y) + 0.04f, a.SocketPosition.z));
        wire.SetPosition(2, new Vector3(b.SocketPosition.x, Mathf.Min(a.transform.position.y, b.transform.position.y) + 0.04f, b.SocketPosition.z));
        wire.SetPosition(3, b.SocketPosition);
    }
}
