using UnityEngine;
using UnityEngine.Rendering;

namespace LowPolyWater
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LowPolyWater : MonoBehaviour
    {
        public float waveHeight = 0.5f;
        public float waveFrequency = 0.5f;
        public float waveLength = 0.75f;
        public Vector3 waveOriginPosition = Vector3.zero;

        private Mesh mesh;
        private Vector3[] originalVertices, vertices;

        private void Start()
        {
            var filter = GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            if (source == null) { enabled = false; return; }
            var sourceVertices = source.vertices;
            var sourceUV = source.uv;
            var triangles = source.triangles;
            originalVertices = new Vector3[triangles.Length];
            var uv = new Vector2[triangles.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                int index = triangles[i];
                originalVertices[i] = sourceVertices[index];
                if (sourceUV.Length == sourceVertices.Length) uv[i] = sourceUV[index];
                triangles[i] = i;
            }
            vertices = (Vector3[])originalVertices.Clone();
            // Each water object owns its animated mesh; the imported asset stays untouched.
            mesh = new Mesh { name = source.name + " (water instance)",
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
        }
        private void Update()
        {
            if (mesh == null) return;
            float length = Mathf.Max(0.001f, Mathf.Abs(waveLength));
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = originalVertices[i];
                float dx = vertex.x - waveOriginPosition.x, dz = vertex.z - waveOriginPosition.z;
                float phase = Mathf.Sqrt(dx * dx + dz * dz) / length;
                vertex.y += waveHeight * Mathf.Sin(Mathf.PI * 2 * (Time.time * waveFrequency + phase));
                vertices[i] = vertex;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
