using System.Collections.Generic;
using UnityEngine;

// A named point of interest for the [M] map and the mini map (market, factory, station, dock…).
// Placed by the map builder on scene objects; gameplay scripts can add their own at runtime.
public sealed class EcoMapMarker : MonoBehaviour
{
    public enum Category { Service, Industry, Energy, Water, Nature, Research, Danger, Quest }
    private static readonly List<EcoMapMarker> Active = new List<EcoMapMarker>();
    public static IReadOnlyList<EcoMapMarker> All => Active;

    public string icon = "poi_quest";
    public string title = "";
    [TextArea] public string detail = "";
    public Category category = Category.Service;
    [Tooltip("Shown on the small corner map too (keep for the few most important places).")]
    public bool onMiniMap;

    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() => Active.Remove(this);

    public static EcoMapMarker Add(GameObject target, string icon, string title, string detail, Category category, bool onMiniMap = false)
    {
        var marker = target.GetComponent<EcoMapMarker>();
        if (marker == null) marker = target.AddComponent<EcoMapMarker>();
        marker.icon = icon; marker.title = title; marker.detail = detail; marker.category = category; marker.onMiniMap = onMiniMap;
        return marker;
    }
}
