using UnityEngine;

public static class EcoHydroArt
{
    public static GameObject Create(Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("Küçük akarsu santrali"); root.transform.SetPositionAndRotation(position, rotation);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0,0.18f,0), new Vector3(4,0.36f,3), new Color(.37f,.48f,.43f));
        Part(root.transform, PrimitiveType.Cube, new Vector3(-.6f,1.1f,0), new Vector3(1.7f,1.8f,1.8f), new Color(.14f,.34f,.3f));
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(1,1,0), new Vector3(1.4f,.18f,1.4f), new Color(.18f,.23f,.25f)).localRotation = Quaternion.Euler(90,0,0);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0,.5f,1.5f), new Vector3(3,.15f,.4f), new Color(.2f,.67f,.74f));
        var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up; collider.size = new Vector3(4,2,3);
        var power = root.AddComponent<EcoPowerNode>(); power.kind = EcoPowerNode.Kind.Hydro; power.ratedPower = 6;
        root.AddComponent<EcoHydroPlant>(); return root;
    }
    private static Transform Part(Transform parent, PrimitiveType type, Vector3 p, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type); go.transform.SetParent(parent, false); go.transform.localPosition=p; go.transform.localScale=scale;
        Object.Destroy(go.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color=color; go.GetComponent<Renderer>().sharedMaterial=mat; return go.transform;
    }
}
