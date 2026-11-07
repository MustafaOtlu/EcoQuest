using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class WeaponVisualEffects
{
    private sealed class IntakeWisps
    {
        private const int Capacity = 8;
        private const int Points = 7;
        private readonly Vector3[] vertices = new Vector3[Capacity * Points * 2];
        private readonly Color32[] colors = new Color32[Capacity * Points * 2];
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly MeshRenderer renderer;
        private int count, previous;

        public static IntakeWisps Create(Transform parent)
        {
            var shader = Resources.Load<Shader>("EcoUI/ToolAreaOverlay");
            return shader != null ? new IntakeWisps(parent, shader) : null;
        }

        private IntakeWisps(Transform parent, Shader shader)
        {
            var root = new GameObject("Intake air wisps", typeof(MeshFilter), typeof(MeshRenderer));
            root.layer = 2;
            root.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Short intake trails" };
            mesh.MarkDynamic();
            var indices = new int[Capacity * (Points - 1) * 6];
            int index = 0;
            for (int trail = 0; trail < Capacity; trail++)
            for (int segment = 0; segment < Points - 1; segment++)
            {
                int a = (trail * Points + segment) * 2;
                indices[index++] = a;
                indices[index++] = a + 1;
                indices[index++] = a + 2;
                indices[index++] = a + 2;
                indices[index++] = a + 1;
                indices[index++] = a + 3;
            }
            mesh.vertices = vertices;
            mesh.triangles = indices;
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(shader) { name = "Translucent intake air", renderQueue = 3050 };
            material.SetFloat("_Feather", 1f);
            material.SetFloat("_DepthTest", (float)CompareFunction.LessEqual);
            renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }

        public void Begin(Color tint)
        {
            count = 0;
            tint = Color.Lerp(tint, Color.white, 0.26f);
            tint.a = 0.65f;
            material.color = tint;
        }

        public void Add(IntakeMote mote, Vector3 mouth, float t, Vector3 head, Vector3 screenNormal, float opacity)
        {
            if (count >= Capacity) return;
            float tail = Mathf.Max(0, t - 0.17f);
            if (t - tail < 0.035f) return;
            var space = renderer.transform;
            for (int i = 0; i < Points; i++)
            {
                float fraction = i / (float)(Points - 1);
                float sample = Mathf.Lerp(tail, t, fraction);
                Vector3 point = i == Points - 1 ? head : IntakePoint(mote.Source, mouth, sample, mote.Radius, mote.Angle, mote.Turn);
                Vector3 next = IntakePoint(mote.Source, mouth, sample + 0.015f, mote.Radius, mote.Angle, mote.Turn);
                float weight = Mathf.Sin(fraction * Mathf.PI);
                Vector3 edge = Vector3.Cross(next - point, screenNormal).normalized * (0.024f * weight);
                int k = (count * Points + i) * 2;
                vertices[k] = space.InverseTransformPoint(point - edge);
                vertices[k + 1] = space.InverseTransformPoint(point + edge);
                colors[k] = colors[k + 1] = new Color32(255, 255, 255, (byte)(255f * opacity * weight));
            }
            count++;
        }

        public void Finish()
        {
            if (count < previous)
                System.Array.Clear(vertices, count * Points * 2, (previous - count) * Points * 2);
            previous = count;
            renderer.enabled = count > 0;
            if (count == 0) return;
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.RecalculateBounds();
        }

        public void Hide() => renderer.enabled = false;

        public void Dispose()
        {
            Destroy(mesh);
            Destroy(material);
        }
    }
}
