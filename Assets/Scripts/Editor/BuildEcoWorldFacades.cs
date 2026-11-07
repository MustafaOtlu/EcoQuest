using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class BuildEcoWorldMap
{
    [MenuItem("EcoQuest/World/Detail building facades")]
    public static void ImproveBuildingFacades()
    {
        var world = GameObject.Find("EcoWorld");
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != TargetScene || world == null)
            throw new System.InvalidOperationException("Open EcoWorld in Edit Mode.");

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Detail building facades");
        int count = DetailBuildingFacades(world.transform, recordUndo: true);
        Undo.CollapseUndoOperations(undo);
        if (count > 0) EditorSceneManager.MarkSceneDirty(world.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("EcoWorld: " + count + " building facades detailed.");
    }

    private enum FacadeStyle { Home, Library, Clinic, Market, Industry, Research, Residence }

    private static int DetailBuildingFacades(Transform world, bool recordUndo = false)
    {
        int count = 0;
        foreach (Transform building in world.Find("City"))
        {
            var style = building.name switch
            {
                "Ev" => FacadeStyle.Home,
                "Kütüphane" => FacadeStyle.Library,
                "Sağlık Ocağı" => FacadeStyle.Clinic,
                "Mahalle Pazarı" => FacadeStyle.Market,
                _ => FacadeStyle.Residence
            };
            if (style == FacadeStyle.Residence && building.Find("Roof") == null) continue;
            string shell = style == FacadeStyle.Market || style == FacadeStyle.Library ? "Hall" : "Body";
            if (DetailFacade(building, shell, style, recordUndo)) count++;
        }
        foreach (Transform building in world.Find("Industry"))
            if (building.Find("Roof tooth") != null && DetailFacade(building, "Hall", FacadeStyle.Industry, recordUndo)) count++;
        if (DetailFacade(world.Find("Glacier/Glacier research station"), "Cabin", FacadeStyle.Research, recordUndo)) count++;
        return count;
    }

    private static bool DetailFacade(Transform building, string shellName, FacadeStyle style, bool recordUndo)
    {
        if (building == null || building.Find(SceneHitboxInitializer.FacadeDetailsName) != null) return false;
        var shell = building.Find(shellName);
        if (shell == null || !shell.TryGetComponent<MeshFilter>(out var shellMesh)) return false;

        var charcoal = Mat("FacadeCharcoal", new Color(0.23f, 0.29f, 0.30f), 0.18f);
        var stone = Mat("FacadeStone", new Color(0.60f, 0.62f, 0.59f), 0.13f);
        var ivory = Mat("FacadeIvory", new Color(0.91f, 0.87f, 0.76f), 0.16f);
        var glass = Mat("FacadeGlass", new Color(0.28f, 0.43f, 0.51f), 0.48f);
        var timber = Mat("FacadeTimber", new Color(0.48f, 0.33f, 0.23f), 0.17f);
        var metal = Mat("FacadeSheetMetal", new Color(0.42f, 0.49f, 0.50f), 0.22f);
        var bounds = new Bounds(shell.localPosition, shell.localScale);
        var min = bounds.min;
        var max = bounds.max;
        var parts = new FacadeParts(building, shellMesh.sharedMesh);
        var frame = style == FacadeStyle.Home || style == FacadeStyle.Market ? timber : charcoal;
        var door = building.Find("Door") ?? building.Find("Entrance") ?? building.Find("Gate");

        parts.Course(bounds, min.y + 0.31f, 0.50f, stone, door);
        parts.Course(bounds, max.y - 0.09f, 0.16f, style == FacadeStyle.Industry ? charcoal : ivory);
        foreach (float x in new[] { min.x + 0.07f, max.x - 0.07f })
            foreach (float z in new[] { min.z + 0.07f, max.z - 0.07f })
                parts.Box(frame, new Vector3(x, bounds.center.y + 0.17f, z), new Vector3(0.26f, bounds.size.y - 0.53f, 0.26f));

        foreach (Transform opening in building)
        {
            if (opening.name != "Window" && opening.name != "Window up" && opening.name != "Windows") continue;
            if (!opening.TryGetComponent<MeshRenderer>(out var pane)) continue;
            if (recordUndo) Undo.RecordObject(pane, "Update daytime window glass");
            pane.sharedMaterial = glass;
            parts.Frame(opening, frame, stone, window: true);
        }
        if (door != null)
        {
            parts.Frame(door, frame, stone, window: false);
            if (style == FacadeStyle.Industry)
            {
                for (float y = min.y + 0.25f; y < door.localPosition.y + door.localScale.y * 0.5f - 0.08f; y += 0.24f)
                    parts.Box(metal, new Vector3(door.localPosition.x, y, door.localPosition.z - door.localScale.z * 0.5f - 0.008f),
                        new Vector3(door.localScale.x - 0.12f, 0.035f, 0.03f));
            }
            else
            {
                var front = door.localPosition - Vector3.forward * (door.localScale.z * 0.5f + 0.024f);
                parts.Box(ivory, front + Vector3.right * (door.localScale.x * 0.32f), new Vector3(0.06f, 0.23f, 0.055f));
                if (style == FacadeStyle.Home || style == FacadeStyle.Research)
                    parts.Box(timber, front + Vector3.up * 0.42f, new Vector3(door.localScale.x * 0.69f, 0.58f, 0.04f));
            }
        }

        foreach (int side in new[] { -1, 1 })
        {
            float x = side < 0 ? min.x - 0.023f : max.x + 0.023f;
            var rotation = Quaternion.Euler(0f, -side * 90f, 0f);
            int floors = style == FacadeStyle.Home ? 2 : 1;
            for (int i = 0; i < floors; i++)
            {
                float y = min.y + bounds.size.y * (floors == 2 ? 0.31f + i * 0.46f : style == FacadeStyle.Industry ? 0.71f : 0.56f);
                float gap = bounds.size.z * 0.23f;
                parts.Window(new Vector3(x, y, bounds.center.z - gap), rotation, glass, frame, stone);
                parts.Window(new Vector3(x, y, bounds.center.z + gap), rotation, glass, frame, stone);
            }
        }
        float rearY = min.y + bounds.size.y * 0.57f;
        foreach (int side in new[] { -1, 1 })
            parts.Window(new Vector3(bounds.center.x + side * bounds.size.x * 0.24f, rearY, max.z + 0.023f),
                Quaternion.Euler(0f, 180f, 0f), glass, frame, stone);

        if (style == FacadeStyle.Home)
        {
            var roofTrim = Mat("FacadeTerracotta", new Color(0.58f, 0.26f, 0.20f), 0.16f);
            parts.Course(bounds, min.y + bounds.size.y * 0.54f, 0.13f, ivory);
            var leftRoof = building.Find("Roof L");
            var rightRoof = building.Find("Roof R");
            if (leftRoof != null && rightRoof != null)
            {
                parts.RoofSeams(leftRoof, roofTrim, 7);
                parts.RoofSeams(rightRoof, roofTrim, 7);
                float peak = leftRoof.localPosition.y + (leftRoof.localRotation * new Vector3(0f, leftRoof.localScale.y * 0.5f, leftRoof.localScale.z * 0.5f)).y;
                parts.Box(roofTrim, new Vector3(bounds.center.x, peak - 0.04f, bounds.center.z), new Vector3(leftRoof.localScale.x + 0.04f, 0.18f, 0.31f));
                parts.GableEnds(bounds, peak - 0.18f, shell.GetComponent<MeshRenderer>().sharedMaterial);
            }
            var chimney = building.Find("Chimney");
            if (chimney != null)
                parts.Box(charcoal, chimney.localPosition + Vector3.up * (chimney.localScale.y * 0.5f), new Vector3(0.95f, 0.16f, 0.95f));
        }
        else if (style == FacadeStyle.Library)
        {
            foreach (Transform column in building)
            {
                if (column.name != "Column") continue;
                foreach (float y in new[] { min.y + 0.19f, max.y - 0.14f })
                    parts.Box(ivory, new Vector3(column.localPosition.x, y, column.localPosition.z), new Vector3(0.71f, 0.20f, 0.71f));
            }
        }
        else if (style == FacadeStyle.Market)
        {
            parts.Box(timber, new Vector3(bounds.center.x, max.y - 0.84f, min.z - 0.08f), new Vector3(bounds.size.x - 0.22f, 0.29f, 0.20f));
            parts.Course(bounds, min.y + 1.35f, 0.12f, timber);
            foreach (int side in new[] { -1, 1 })
                parts.Window(new Vector3(bounds.center.x + side * bounds.size.x * 0.23f, min.y + 2.39f, min.z - 0.023f),
                    Quaternion.identity, glass, timber, stone, new Vector2(2.55f, 1.20f));
        }
        else if (style == FacadeStyle.Industry)
        {
            parts.Box(charcoal, new Vector3(bounds.center.x, max.y + 0.03f, bounds.center.z), new Vector3(bounds.size.x + 0.09f, 0.15f, bounds.size.z + 0.09f));
            foreach (Transform roof in building)
            {
                if (roof.name != "Roof tooth") continue;
                parts.CloseRoofTooth(roof, max.y, metal, glass);
                parts.RoofSeams(roof, metal, 4);
            }
            parts.Course(bounds, min.y + bounds.size.y * 0.45f, 0.21f, metal, door);
        }
        else if (style == FacadeStyle.Research)
        {
            for (float y = min.y + 0.7f; y < max.y - 0.18f; y += 0.32f)
                parts.Course(bounds, y, 0.043f, timber, door, depth: 0.027f);
        }
        else
        {
            parts.Course(bounds, min.y + bounds.size.y * 0.32f, 0.12f, stone, door);
        }

        parts.Bake();
        if (recordUndo) Undo.RegisterCreatedObjectUndo(parts.Root.gameObject, "Detail building facade");
        return true;
    }

    private sealed class FacadeParts
    {
        public readonly Transform Root;
        private readonly Mesh cube;
        private readonly Dictionary<Material, List<CombineInstance>> pieces = new();
        private readonly List<Mesh> temporaryMeshes = new();

        public FacadeParts(Transform building, Mesh cube)
        {
            Root = Group(SceneHitboxInitializer.FacadeDetailsName, building);
            this.cube = cube;
        }

        private void Add(Material material, Mesh mesh, Matrix4x4 transform)
        {
            if (!pieces.TryGetValue(material, out var batch)) pieces.Add(material, batch = new List<CombineInstance>());
            batch.Add(new CombineInstance { mesh = mesh, transform = transform });
        }

        public void Box(Material material, Vector3 center, Vector3 size, Quaternion? rotation = null)
            => Add(material, cube, Matrix4x4.TRS(center, rotation ?? Quaternion.identity, size));

        public void Course(Bounds shell, float height, float thickness, Material material, Transform doorway = null, float depth = 0.13f)
        {
            var min = shell.min;
            var max = shell.max;
            float offset = depth * 0.30f;
            Box(material, new Vector3(shell.center.x, height, max.z + offset), new Vector3(shell.size.x + depth, thickness, depth));
            Box(material, new Vector3(min.x - offset, height, shell.center.z), new Vector3(depth, thickness, shell.size.z));
            Box(material, new Vector3(max.x + offset, height, shell.center.z), new Vector3(depth, thickness, shell.size.z));

            void Front(float from, float to)
            {
                if (to > from) Box(material, new Vector3((from + to) * 0.5f, height, min.z - offset), new Vector3(to - from, thickness, depth));
            }
            if (doorway == null) Front(min.x - depth * 0.5f, max.x + depth * 0.5f);
            else
            {
                float gap = doorway.localScale.x * 0.5f + 0.10f;
                Front(min.x - depth * 0.5f, doorway.localPosition.x - gap);
                Front(doorway.localPosition.x + gap, max.x + depth * 0.5f);
            }
        }

        public void Window(Vector3 center, Quaternion rotation, Material glass, Material trim, Material stone, Vector2? size = null)
        {
            var dimensions = size ?? new Vector2(1.27f, 0.98f);
            var pane = new GameObject("Window", typeof(MeshFilter), typeof(MeshRenderer));
            pane.transform.SetParent(Root, false);
            pane.transform.SetLocalPositionAndRotation(center, rotation);
            pane.transform.localScale = new Vector3(dimensions.x, dimensions.y, 0.09f);
            pane.GetComponent<MeshFilter>().sharedMesh = cube;
            pane.GetComponent<MeshRenderer>().sharedMaterial = glass;
            Frame(pane.transform, trim, stone, window: true);
        }

        public void Frame(Transform opening, Material trim, Material stone, bool window)
        {
            Vector3 size = opening.localScale;
            Quaternion rotation = opening.localRotation;
            Vector3 center = opening.localPosition;
            const float bar = 0.11f;
            float x = size.x * 0.5f;
            float y = size.y * 0.5f;
            float face = -size.z * 0.5f - 0.012f;
            void Rail(Material material, float u, float v, Vector3 dimensions)
                => Box(material, center + rotation * new Vector3(u, v, face), dimensions, rotation);

            Rail(trim, -x, 0f, new Vector3(bar, size.y, 0.13f));
            Rail(trim, x, 0f, new Vector3(bar, size.y, 0.13f));
            Rail(trim, 0f, y, new Vector3(size.x + bar, bar, 0.13f));
            if (!window) return;
            Rail(trim, 0f, 0f, new Vector3(0.065f, size.y, 0.10f));
            Rail(trim, 0f, 0.08f, new Vector3(size.x, 0.055f, 0.10f));
            Rail(stone, 0f, -y, new Vector3(size.x + 0.27f, 0.13f, 0.25f));
        }

        public void RoofSeams(Transform roof, Material material, int count)
        {
            Vector3 size = roof.localScale;
            Quaternion rotation = roof.localRotation;
            for (int i = 0; i < count; i++)
            {
                float x = size.x * Mathf.Lerp(-0.46f, 0.46f, i / (count - 1f));
                var center = roof.localPosition + rotation * new Vector3(x, size.y * 0.5f + 0.016f, 0f);
                Box(material, center, new Vector3(0.055f, 0.042f, size.z - 0.05f), rotation);
            }
        }

        public void CloseRoofTooth(Transform roof, float ceiling, Material metal, Material glass)
        {
            Vector3 half = roof.localScale * 0.5f;
            var low = roof.localPosition + roof.localRotation * new Vector3(-half.x, -half.y, 0f);
            var high = roof.localPosition + roof.localRotation * new Vector3(half.x, -half.y, 0f);
            float baseHeight = Mathf.Min(ceiling - 0.03f, low.y);
            var z = Vector3.forward * (half.z - 0.045f);
            var lowBase = new Vector3(low.x, baseHeight, low.z);
            var highBase = new Vector3(high.x, baseHeight, high.z);
            var walls = new MeshBuilder();
            walls.Quad(lowBase - z, highBase - z, low - z, high - z, Color.white, Vector3.back);
            walls.Quad(lowBase + z, highBase + z, low + z, high + z, Color.white, Vector3.forward);
            walls.Quad(highBase - z, highBase + z, high - z, high + z, Color.white, Vector3.right);
            walls.Quad(lowBase - z, lowBase + z, low - z, low + z, Color.white, Vector3.left);
            var closure = walls.ToMesh("Sawtooth walls");
            temporaryMeshes.Add(closure);
            Add(metal, closure, Matrix4x4.identity);

            var strip = new Vector3(high.x + 0.014f, Mathf.Lerp(ceiling, high.y, 0.63f), high.z);
            Box(glass, strip, new Vector3(0.048f, (high.y - ceiling) * 0.39f, half.z * 1.69f));
        }

        public void GableEnds(Bounds shell, float peak, Material material)
        {
            var mesh = new Mesh { name = "Gable ends" };
            float min = shell.min.x - 0.012f;
            float max = shell.max.x + 0.012f;
            float baseHeight = shell.max.y - 0.06f;
            mesh.vertices = new[]
            {
                new Vector3(min, baseHeight, shell.min.z + 0.18f), new Vector3(min, peak, shell.center.z), new Vector3(min, baseHeight, shell.max.z - 0.18f),
                new Vector3(max, baseHeight, shell.min.z + 0.18f), new Vector3(max, peak, shell.center.z), new Vector3(max, baseHeight, shell.max.z - 0.18f)
            };
            mesh.triangles = new[] { 0, 2, 1, 3, 4, 5 };
            mesh.RecalculateNormals();
            temporaryMeshes.Add(mesh);
            Add(material, mesh, Matrix4x4.identity);
        }

        public void Bake()
        {
            var combined = new List<CombineInstance>();
            var materials = new List<Material>();
            foreach (var pair in pieces)
            {
                var batch = new Mesh();
                batch.CombineMeshes(pair.Value.ToArray());
                combined.Add(new CombineInstance { mesh = batch, transform = Matrix4x4.identity });
                materials.Add(pair.Key);
                temporaryMeshes.Add(batch);
            }

            var building = Root.parent;
            var mesh = new Mesh { name = building.name + " facade" };
            mesh.CombineMeshes(combined.ToArray(), mergeSubMeshes: false, useMatrices: false);
            foreach (var temporary in temporaryMeshes) Object.DestroyImmediate(temporary);
            var path = AssetDatabase.GenerateUniqueAssetPath(Folder + "Meshes/Facade_" + building.parent.name + "_" + building.GetSiblingIndex() + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            Root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            Root.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
        }
    }
}
