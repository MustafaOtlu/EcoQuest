using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Small batched, bevelled surfaces for the player-held props. The moving mechanisms use separate batches.
internal sealed class EcoToolMesh
{
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Material> materials = new List<Material>();
    private readonly List<List<int>> faces = new List<List<int>>();

    public MeshRenderer Build(Transform parent, string name, List<Object> generated)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.subMeshCount = faces.Count;
        for (int i = 0; i < faces.Count; i++) mesh.SetTriangles(faces[i], i, false);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generated.Add(mesh);
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = materials.ToArray();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return renderer;
    }

    private List<int> Triangles(Material material)
    {
        int index = materials.IndexOf(material);
        if (index >= 0) return faces[index];
        materials.Add(material);
        var list = new List<int>();
        faces.Add(list);
        return list;
    }

    public void Box(Material material, Vector3 center, Vector3 size, float bevel, Quaternion? rotation = null)
    {
        var orientation = rotation ?? Quaternion.identity;
        Vector3 half = size * 0.5f;
        bevel = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.55f);
        var outline = new Vector3[4, 8];
        for (int row = 0; row < 4; row++)
        {
            bool cap = row == 0 || row == 3;
            float x = half.x - (cap ? bevel * .68f : 0f);
            float y = half.y - (cap ? bevel * .68f : 0f);
            float cut = bevel * (cap ? .38f : 1f);
            float z = (row < 2 ? -1f : 1f) * (half.z - (cap ? 0f : bevel));
            outline[row, 0] = new Vector3(x - cut, -y, z);
            outline[row, 1] = new Vector3(x, -y + cut, z);
            outline[row, 2] = new Vector3(x, y - cut, z);
            outline[row, 3] = new Vector3(x - cut, y, z);
            outline[row, 4] = new Vector3(-x + cut, y, z);
            outline[row, 5] = new Vector3(-x, y - cut, z);
            outline[row, 6] = new Vector3(-x, -y + cut, z);
            outline[row, 7] = new Vector3(-x + cut, -y, z);
            for (int i = 0; i < 8; i++) outline[row, i] = center + orientation * outline[row, i];
        }
        var tris = Triangles(material);
        for (int r = 0; r < 3; r++)
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                Quad(tris, outline[r, i], outline[r, next], outline[r + 1, next], outline[r + 1, i]);
            }
        for (int i = 1; i < 7; i++)
        {
            Triangle(tris, outline[0, 0], outline[0, i + 1], outline[0, i]);
            Triangle(tris, outline[3, 0], outline[3, i], outline[3, i + 1]);
        }
    }

    public void Bar(Material material, Vector3 from, Vector3 to, float width, float thickness, float bevel)
    {
        var direction = to - from;
        Box(material, (from + to) * .5f, new Vector3(width, thickness, direction.magnitude), bevel, Along(direction));
    }

    public void Ring(Material material, Vector3 center, Vector3 axis, float inner, float outer, float length, int sides = 12)
    {
        float end = length * .5f;
        float edge = Mathf.Min((outer - inner) * .28f, end * .46f);
        Lathe(material, center, axis, sides,
            new Vector2(inner, -end), new Vector2(outer - edge, -end), new Vector2(outer, -end + edge),
            new Vector2(outer, end - edge), new Vector2(outer - edge, end), new Vector2(inner, end), new Vector2(inner, -end));
    }

    public void Cylinder(Material material, Vector3 center, Vector3 axis, float radius, float length, int sides = 12)
    {
        float end = length * .5f;
        float edge = Mathf.Min(radius * .12f, end * .4f);
        Lathe(material, center, axis, sides,
            new Vector2(0f, -end), new Vector2(radius - edge, -end), new Vector2(radius, -end + edge),
            new Vector2(radius, end - edge), new Vector2(radius - edge, end), new Vector2(0f, end));
    }

    public void Lathe(Material material, Vector3 center, Vector3 axis, int sides, params Vector2[] profile)
    {
        var rotation = Along(axis);
        var tris = Triangles(material);
        for (int r = 0; r < profile.Length - 1; r++)
        {
            var from = profile[r];
            var to = profile[r + 1];
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Radial(i, sides);
                Vector3 b = Radial(i + 1, sides);
                Vector3 v0 = center + rotation * (a * from.x + Vector3.forward * from.y);
                Vector3 v1 = center + rotation * (b * from.x + Vector3.forward * from.y);
                Vector3 v2 = center + rotation * (b * to.x + Vector3.forward * to.y);
                Vector3 v3 = center + rotation * (a * to.x + Vector3.forward * to.y);
                if (from.x == 0f) Triangle(tris, v0, v2, v3);
                else if (to.x == 0f) Triangle(tris, v0, v1, v2);
                else Quad(tris, v0, v1, v2, v3);
            }
        }
    }

    private static Vector3 Radial(int side, int total)
    {
        float angle = side * (2f * Mathf.PI / total);
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
    }

    private static Quaternion Along(Vector3 direction)
    {
        var axis = direction.normalized;
        return Quaternion.LookRotation(axis, Mathf.Abs(axis.y) > .96f ? Vector3.forward : Vector3.up);
    }

    private void Triangle(List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int offset = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        tris.Add(offset); tris.Add(offset + 1); tris.Add(offset + 2);
    }

    private void Quad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int offset = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        tris.Add(offset); tris.Add(offset + 1); tris.Add(offset + 2);
        tris.Add(offset); tris.Add(offset + 2); tris.Add(offset + 3);
    }
}
