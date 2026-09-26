using UnityEngine;

// Compatibility for older scenes; MainScene contains an authored field station prefab.
public sealed class EcoPracticeArea : MonoBehaviour
{
    public Transform player;
    public Material material;
    private void Start()
    {
        if (player == null || GameObject.Find("Eco Field Station") != null || GameObject.Find("FieldStation") != null) return;
        var station = EcoWorldArt.Spawn("FieldStation", player.position, transform);
        if (station != null) station.name = "Eco Field Station";
    }
}
