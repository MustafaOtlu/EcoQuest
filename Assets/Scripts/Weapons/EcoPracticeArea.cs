using UnityEngine;

// Small, removable practice supplies for the current test scene, not an infinite inventory grant.
public sealed class EcoPracticeArea : MonoBehaviour
{
    public Transform player;
    public Material material;
    private void Start()
    {
        if(player==null)return;
        var root=new GameObject("Practice Supplies");root.transform.SetParent(transform,false);
        var water=Create(root.transform,"Clean Water Supply",PrimitiveType.Cylinder,new Vector3(2.2f,0,2.5f),new Vector3(0.65f,0.45f,0.65f),new Color(0.15f,0.55f,0.8f));
        water.AddComponent<WeaponWaterSource>();
        for(int i=0;i<3;i++)
        {
            var scrap=Create(root.transform,"Recyclable Scrap",PrimitiveType.Cube,new Vector3(3.5f+i*0.6f,0,2.5f),Vector3.one*0.3f,new Color(0.5f,0.42f,0.28f));
            var resource=scrap.AddComponent<RecyclableResource>();resource.metal=2;resource.plastic=1;
        }
    }
    private GameObject Create(Transform parent,string name,PrimitiveType primitive,Vector3 offset,Vector3 scale,Color color)
    {
        Vector3 position=player.position+offset;
        if(Physics.Raycast(position+Vector3.up*5,Vector3.down,out var hit,15,~0,QueryTriggerInteraction.Ignore))position.y=hit.point.y;
        position.y+=(primitive==PrimitiveType.Cylinder?scale.y:scale.y*0.5f)+0.02f;
        var go=GameObject.CreatePrimitive(primitive);go.name=name;go.transform.SetParent(parent,true);go.transform.position=position;go.transform.localScale=scale;
        var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
        var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",color);renderer.SetPropertyBlock(properties);return go;
    }
}
