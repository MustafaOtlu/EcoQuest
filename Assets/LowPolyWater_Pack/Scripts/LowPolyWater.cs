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
        private Renderer surface;
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
            RefreshBounds();
            filter.sharedMesh = mesh;
            surface = GetComponent<Renderer>();
        }
        private void Update()
        {
            // Off screen the waves are not worth rebuilding the mesh and its normals every frame.
            if (mesh == null || surface != null && !surface.isVisible) return;
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
            RefreshBounds();
        }
        private void RefreshBounds()
        {
            mesh.RecalculateBounds();
            // A small margin keeps boundary vertices inside the box despite floating-point rounding.
            var bounds = mesh.bounds;
            // Room for a full wave either way, so the box stays valid while updates pause off screen.
            bounds.Expand(0.01f + 2f * Mathf.Abs(waveHeight));
            mesh.bounds = bounds;
        }
        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
