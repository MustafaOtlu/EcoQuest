using System.Collections.Generic;
using UnityEngine;

// GDD §10 "iklim zinciri": the ice loses height while the area-weighted world carbon index is above 20 and grows back
// slowly below it (recovery is slower than melting). The monitoring station only measures this (stationBonus stays 0).
public sealed class EcoGlacierMelt : MonoBehaviour
{
    public List<Transform> ice = new List<Transform>();
    [Range(0f, 100f)] public float safeCarbon = 20f;
    [Range(0f, 1f)] public float maxMelt = 0.7f;
    /// <summary>Set by the glacier monitoring station: shifts the safe carbon level up so the melt line stays back.</summary>
    public float stationBonus;
    private Vector3[] baseScale;
    private float[] baseY;
    private float meltNow, meltTarget, nextScan;
    /// <summary>Positive while the melt line is still advancing (target above current).</summary>
    public float MeltRate => Mathf.Max(0f, meltTarget - meltNow);

    private void Start()
    {
        baseScale = new Vector3[ice.Count];
        baseY = new float[ice.Count];
        for (int i = 0; i < ice.Count; i++)
            if (ice[i] != null) { baseScale[i] = ice[i].localScale; baseY[i] = ice[i].position.y; }
    }

    private void Update()
    {
        if (baseScale == null) return;
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 2f;
            float average = EcoRegion.WorldCarbon();
            meltTarget = Mathf.Clamp01((average - safeCarbon - stationBonus) / 60f) * maxMelt;
        }
        meltNow = Mathf.MoveTowards(meltNow, meltTarget, (meltTarget > meltNow ? 0.02f : 0.006f) * Time.deltaTime);
        for (int i = 0; i < ice.Count; i++)
        {
            var block = ice[i];
            if (block == null) continue;
            float k = 1f - meltNow;
            var scale = baseScale[i];
            block.localScale = new Vector3(scale.x * Mathf.Lerp(1f, 0.9f, meltNow), scale.y * k, scale.z * Mathf.Lerp(1f, 0.9f, meltNow));
            // Keep the base on the ground: scaling about the centre would otherwise float the block.
            var position = block.position;
            position.y = baseY[i] - (scale.y - scale.y * k) * 0.5f;
            block.position = position;
        }
    }
}
