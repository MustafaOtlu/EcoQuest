using System.Collections.Generic;
using UnityEngine;

// GDD "iklim zinciri": higher carbon footprint -> glaciers shrink. Scenery ice blocks lose
// height as the average regional carbon index rises and grow back when it falls.
public sealed class EcoGlacierMelt : MonoBehaviour
{
    public List<Transform> ice = new List<Transform>();
    [Range(0f, 100f)] public float safeCarbon = 12f;
    [Range(0f, 1f)] public float maxMelt = 0.7f;
    private Vector3[] baseScale;
    private float[] baseY;
    private float meltNow, meltTarget, nextScan;

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
            var regions = FindObjectsByType<EcoRegion>(FindObjectsSortMode.None);
            float sum = 0f; int count = 0;
            foreach (var region in regions) if (region != null) { sum += region.carbon; count++; }
            float average = count > 0 ? sum / count : 0f;
            meltTarget = Mathf.Clamp01((average - safeCarbon) / 60f) * maxMelt;
        }
        meltNow = Mathf.MoveTowards(meltNow, meltTarget, 0.02f * Time.deltaTime);
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
