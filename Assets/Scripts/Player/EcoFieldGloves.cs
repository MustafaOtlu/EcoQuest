using System.Collections.Generic;
using UnityEngine;

internal static class EcoFieldGloves
{
    public static void Fit(Transform hand, Transform middle, Transform index, Transform little, Quaternion palmFrame,
        List<Object> assets)
    {
        if (hand == null || middle == null || index == null || little == null) return;
        var end = hand.InverseTransformPoint(middle.position);
        var width = Vector3.Distance(hand.InverseTransformPoint(index.position), hand.InverseTransformPoint(little.position));
        if (end.sqrMagnitude < .000001f || width < .001f) return;

        var cloth = Paint(new Color(.28f, .34f, .22f), "Zeytin rengi iş eldiveni", assets);
        var leather = Paint(new Color(.45f, .29f, .16f), "Eldiven deri pedi", assets);
        var seam = Paint(new Color(.63f, .53f, .34f), "Eldiven manşeti", assets);
        var palm = new EcoToolMesh();
        var direction = end.normalized;
        palm.Box(cloth, end * .54f, new Vector3(width * 1.48f, width * .55f, end.magnitude * 1.20f),
            width * .18f, palmFrame);
        palm.Box(leather, end * .55f + palmFrame * new Vector3(0, width * .25f, 0),
            new Vector3(width * 1.20f, width * .19f, end.magnitude * .77f), width * .11f, palmFrame);
        palm.Box(seam, direction * (-end.magnitude * .06f), new Vector3(width * 1.05f, width * .61f, end.magnitude * .30f),
            width * .16f, palmFrame);
        palm.Build(hand, "Dikişli iş eldiveni", assets);

        // Follow the original skeleton's knuckles. The plates move with the fingers in grip and reach poses.
        foreach (var bone in hand.GetComponentsInChildren<Transform>())
        {
            if (bone.childCount == 0 || !IsFingerJoint(bone.name)) continue;
            var child = bone.GetChild(0);
            var segment = child.localPosition;
            if (segment.sqrMagnitude < .000001f) continue;
            int joint = bone.name[bone.name.Length - 1] - '0';
            float ratio = joint == 1 ? .174f : joint == 2 ? .148f : .126f;
            if (bone.name.Contains("Thumb")) ratio *= 1.12f;
            float scale = Mathf.Max(.001f, Mathf.Abs(bone.lossyScale.x));
            float radius = width * Mathf.Abs(hand.lossyScale.x) * ratio / scale;
            var finger = new EcoToolMesh();
            finger.Cylinder(joint == 1 ? leather : cloth, segment * .50f, segment,
                radius, segment.magnitude + radius * .61f, 6);
            finger.Build(bone, "Eldiven parmak pedi", assets);
        }
    }

    private static bool IsFingerJoint(string name)
    {
        if (!name.Contains("Hand") || name.Length == 0) return false;
        char joint = name[name.Length - 1];
        return joint >= '1' && joint <= '3' && (name.Contains("Thumb") || name.Contains("Index") ||
            name.Contains("Middle") || name.Contains("Ring") || name.Contains("Pinky"));
    }

    private static Material Paint(Color color, string name, List<Object> assets)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { color = color, name = name };
        material.SetFloat("_Smoothness", .22f);
        assets.Add(material);
        return material;
    }
}
