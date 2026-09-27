using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class WeaponRuntimeCheck : MonoBehaviour
{
    public PlayerWeaponSystem weapons;
    public Transform view;
    string report = "";
    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Weapon Tests"); var check = root.AddComponent<WeaponRuntimeCheck>();
        var player = new GameObject("Player"); player.AddComponent<PlayerVitals>();
        player.AddComponent<PlayerController>().enabled = false;
        var cam = new GameObject("Main Camera"); cam.tag = "MainCamera"; cam.AddComponent<Camera>();
        cam.transform.SetParent(player.transform); cam.transform.localPosition = new Vector3(0,0.7f,0);
        check.view = cam.transform;
        var mount = new GameObject("Holder"); mount.transform.SetParent(cam.transform); mount.transform.localPosition = new Vector3(0.126f,-0.285f,0.372f);
        var holder = mount.AddComponent<FirstPersonWeaponHolder>();
        var vacuum = GameObject.CreatePrimitive(PrimitiveType.Cube); vacuum.transform.SetParent(mount.transform,false); vacuum.transform.localScale = Vector3.one * 0.1f;
        DestroyImmediate(vacuum.GetComponent<Collider>());
        var so = new SerializedObject(holder); so.FindProperty("equippedWeapon").objectReferenceValue = vacuum.transform; so.FindProperty("viewTransform").objectReferenceValue = cam.transform; so.ApplyModifiedPropertiesWithoutUndo();
        var weapons = mount.AddComponent<PlayerWeaponSystem>(); weapons.holder = holder;
        weapons.effectMaterial = new Material(Shader.Find("Standard"));
        string[] dirs = { "VacuumGun", "SeedGun", "Separator", "Scanner" };
        for(int i=1;i<4;i++)
        {
            string path=Directory.GetFiles("Assets/3D Assets/WEAPONS/"+dirs[i],"*.fbx").First().Replace('\\','/');
            weapons.modelPrefabs[i]=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(weapons.modelPrefabs[i],out string guid,out long id);
            if(id!=919132149155446097)throw new Exception("Unexpected FBX root ID: "+id);
        }
        check.weapons=weapons; mount.AddComponent<EcoQuestHUD>().weapons=weapons;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-0.5f,0);floor.transform.localScale=new Vector3(100,1,100);floor.AddComponent<PlantingSurface>();
        EditorSceneManager.SaveScene(root.scene,"Assets/WeaponValidation.unity"); EditorApplication.isPlaying=true;
    }
    IEnumerator Start()
    {
        Application.logMessageReceived += Error;
        yield return null; weapons.enabled=false; yield return null; Canvas.ForceUpdateCanvases(); var hud=GameObject.Find("EcoQuest HUD"); Check(hud!=null && hud.transform.parent==null && hud.GetComponent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay,"HUD creates independent overlay"); Check(hud.transform.Find("Toolbelt").childCount==5,"HUD creates five tool slots");
        for(int i=0;i<4;i++) { weapons.SelectTool(i); Check(weapons.Selected==(PlayerWeaponSystem.Tool)i && weapons.holder.EquippedWeapon.gameObject.activeSelf,"Select weapon "+i); }
        weapons.SelectTool(1);Vector3 scale=weapons.holder.EquippedWeapon.localScale;
        for(int i=0;i<30;i++){weapons.SelectTool(0);weapons.SelectTool(1);}
        Check(Vector3.Distance(scale,weapons.holder.EquippedWeapon.localScale)<0.00001f,"Switching preserves fitted scale");
        // Exercise a real ranged attack; only the player's stream may use a line renderer.
        int linesBefore=FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Length;
        var shotTemplate=new GameObject("Test enemy projectile template");
        shotTemplate.transform.position=Vector3.one*1000f;
        shotTemplate.AddComponent<EnemyProjectile>().enabled=false;
        var attacker=Enemy(EnemyBrain.Species.Tin);attacker.projectilePrefab=shotTemplate;attacker.enabled=true;
        float attackDeadline=Time.time+2f;
        while(attacker.State!=EnemyBrain.Behaviour.Attack && Time.time<attackDeadline)yield return null;
        Check(attacker.State==EnemyBrain.Behaviour.Attack,"Ranged enemy enters attack state");
        yield return new WaitForSeconds(attacker.windup+0.1f);
        Check(FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length==2,"Ranged attack still creates its projectile");
        Check(attacker.GetComponentsInChildren<LineRenderer>(true).Length==0 && FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Length==linesBefore,"Enemy attack creates no aiming or telegraph line");
        Destroy(attacker.gameObject);
        foreach(var shot in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))Destroy(shot.gameObject);
        yield return null;
        weapons.SelectTool(0);weapons.CycleMode();weapons.CycleMode();
        var flame=Enemy(EnemyBrain.Species.Flame);yield return new WaitForFixedUpdate();float hp=flame.Health,water=weapons.Water; weapons.Use(0.5f);
        Check(flame.Health<hp && weapons.Water<water,"Water damages Flame and consumes water hp="+hp+" -> "+flame.Health+" water="+water+" -> "+weapons.Water+" mode="+weapons.Mode+" view="+view.position+" fwd="+view.forward+" hits="+string.Join(",",Physics.RaycastAll(view.position,view.forward,20).Select(h=>h.collider.name)));
        Destroy(flame.gameObject);yield return null;
        var tin=Enemy(EnemyBrain.Species.Tin);yield return new WaitForFixedUpdate();
        var healthCanvas=tin.GetComponentInChildren<Canvas>();
        var healthFill=healthCanvas.transform.GetChild(1).GetComponent<UnityEngine.UI.Image>();
        float fullWidth=healthFill.rectTransform.sizeDelta.x;
        tin.TakeDamage(5f);yield return null;yield return null;
        Check(healthCanvas.enabled && healthFill.rectTransform.sizeDelta.x<fullWidth && Mathf.Abs(healthFill.rectTransform.sizeDelta.x-98f*tin.Health/tin.maximumHealth)<0.01f,"Enemy health bar visibly shrinks after damage without a fill sprite");
        hp=tin.Health;weapons.Use(0.2f);Check(tin.Health==hp,"Water does not damage Tin");
        weapons.SelectTool(1);weapons.CycleMode();int ammo=weapons.MetalAmmo;weapons.Use(0.1f);
        yield return new WaitForSeconds(0.3f);Check(tin.Health<hp && weapons.MetalAmmo==ammo-1,"Metal projectile hits Tin and consumes ammo");
        tin.TakeDamage(1000f);
        var drops=FindObjectsByType<EnemyLootDrop>(FindObjectsSortMode.None);
        var dropResources=drops.Select(d=>d.GetComponent<RecyclableResource>()).ToArray();
        Check(drops.Length==2 && dropResources.All(r=>r!=null && r.metal>0 && r.plastic==0) && dropResources.Sum(r=>r.metal)==6,"Tin death creates exactly two nonempty drops totaling six metal");
        tin.TakeDamage(1000f);
        Check(FindObjectsByType<EnemyLootDrop>(FindObjectsSortMode.None).Length==2,"Repeated lethal damage cannot duplicate loot");
        foreach(var drop in drops)Destroy(drop.gameObject);
        Destroy(tin.gameObject);yield return null;
        weapons.SelectTool(0);weapons.CycleMode();weapons.CycleMode();
        var smoke=Enemy(EnemyBrain.Species.Smoke);yield return new WaitForFixedUpdate();hp=smoke.Health;weapons.Use(0.5f);Check(smoke.Health<hp,"Disperse damages Smoke");
        Destroy(smoke.gameObject);yield return null;
        var acid=Enemy(EnemyBrain.Species.Acid);yield return new WaitForFixedUpdate();hp=acid.Health;weapons.Use(0.5f);Check(acid.Health<hp,"Disperse damages Acid");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,0.7f,2);wall.transform.localScale=new Vector3(2,2,0.2f);Physics.SyncTransforms();
        hp=acid.Health;weapons.Use(0.5f);Check(acid.Health==hp,"Wall blocks continuous weapon damage");
        Destroy(wall);Destroy(acid.gameObject);yield return null;
        weapons.SelectTool(2);var slime=Enemy(EnemyBrain.Species.Slime);yield return new WaitForFixedUpdate();hp=slime.Health;weapons.Use(0.5f);Check(slime.Health<hp,"Recycler damages Slime");
        Destroy(slime.gameObject);yield return null;
        weapons.SelectTool(0);weapons.CycleMode();weapons.CycleMode(); // Disperse -> Water -> Collect
        var scrap=GameObject.CreatePrimitive(PrimitiveType.Cube);scrap.transform.position=new Vector3(0,0.7f,3);scrap.AddComponent<RecyclableResource>();Physics.SyncTransforms();
        weapons.Use(0.5f);Check(weapons.CollectedMetalScrap==2 && weapons.Metal==0,"Vacuum stores raw scrap");
        yield return null;weapons.SelectTool(2);weapons.RecycleInventory();Check(weapons.Metal==2 && weapons.CollectedMetalScrap==0,"Recycler converts collected scrap");
        var source=GameObject.CreatePrimitive(PrimitiveType.Cube);source.transform.position=new Vector3(0,0.7f,3);source.AddComponent<WeaponWaterSource>();Physics.SyncTransforms();
        weapons.SelectTool(0);water=weapons.Water;weapons.Use(0.2f);Check(weapons.Water>water,"Vacuum refills from water source");
        weapons.SelectTool(3);weapons.Use(0.1f);Check(weapons.LastScan=="Temiz su","Scanner returns water info without UI");
        Destroy(source);yield return null;
        weapons.SelectTool(1);weapons.CycleMode();view.localRotation=Quaternion.Euler(30,0,0);Physics.SyncTransforms();int seeds=weapons.Seeds;
        weapons.Use(0.1f);yield return new WaitForSeconds(0.4f);Check(weapons.Seeds==seeds-1 && FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None).Length==1,"Seed mode plants without combat damage");
        var crop=FindFirstObjectByType<PlantedSeed>();
        crop.WaterPlant(1f,true);
        for(int i=0;i<70;i++){crop.WaterPlant(0.1f,true);crop.Simulate(1f);}
        Check(crop.IsMature,"Watered crop reaches maturity");
        int foodBefore=weapons.Food,seedsBefore=weapons.Seeds;
        Physics.SyncTransforms();
        var cropCollider=crop.GetComponentInChildren<Collider>();
        Check(cropCollider!=null,"Planted crop exposes an interaction collider");
        view.rotation=Quaternion.LookRotation(cropCollider.bounds.center-view.position);Physics.SyncTransforms();
        weapons.Interact();
        Check(weapons.Seeds==seedsBefore+3 && weapons.Food==foodBefore+1,"Aiming and interacting harvests seeds and food into player inventory");
        Check(!crop.TryHarvest(out _,out _),"Harvest cannot be claimed twice");
        yield return null;yield return new WaitForSeconds(0.12f);
        Check(FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None).Length==0,"Harvest removes the final crop from the world");
        var objective=hud.transform.Find("Objective").GetComponentInChildren<UnityEngine.UI.Text>();
        Check(objective.text.Contains("Görev tamamlandı"),"HUD keeps completion after harvesting the final living crop");
        var dry=new GameObject("Dry plant").AddComponent<PlantedSeed>();
        for(int i=0;i<80;i++)dry.Simulate(1f);
        Check(dry.IsWithered && dry.TryCompost(),"Unwatered crop withers and becomes compost");
        CheckSessionPersistence();
        weapons.SelectTool(4);Check(weapons.Selected==PlayerWeaponSystem.Tool.Hands && weapons.holder.EquippedWeapon==null,"Fifth slot unequips the weapon");
        yield return null; Check(hud.transform.Find("Toolbelt/ELLER").GetComponent<UnityEngine.UI.Image>().color.g > 0.4f,"HUD highlights selected hands slot"); view.localRotation=Quaternion.identity;
        weapons.GetComponentInParent<PlayerVitals>().ReceiveHit(1000);float energy=weapons.Energy;weapons.Use(1f);Check(weapons.Energy==energy,"Dead player cannot fire");
        File.WriteAllText("weapon-check-success.txt",report);EditorApplication.Exit(0);
    }
    void CheckSessionPersistence()
    {
        const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
        var keyField=typeof(PlayerWeaponSystem).GetField("sessionKey",fields);
        var dirtyField=typeof(PlayerWeaponSystem).GetField("dirtyWater",fields);
        Check(keyField!=null && dirtyField!=null,"Session persistence exposes an isolated save key");
        string originalKey=(string)keyField.GetValue(weapons);
        bool originalDirty=(bool)dirtyField.GetValue(weapons);
        string key="EcoQuest.Tests.Session."+Guid.NewGuid().ToString("N");
        bool hadKey=PlayerPrefs.HasKey(key);
        string priorValue=hadKey?PlayerPrefs.GetString(key):null;
        try
        {
            keyField.SetValue(weapons,key);dirtyField.SetValue(weapons,true);
            float energy=weapons.Energy,water=weapons.Water;
            int food=weapons.Food,seeds=weapons.Seeds,metal=weapons.Metal,ammo=weapons.MetalAmmo;
            weapons.SaveSession();
            Check(PlayerPrefs.HasKey(key),"Save uses the isolated test key");
            dirtyField.SetValue(weapons,false);weapons.LoadSession();
            Check((bool)dirtyField.GetValue(weapons) && Mathf.Approximately(weapons.Water,water),"Saving and loading preserves contaminated water");
            PlayerPrefs.SetString(key,"{\"water\":");
            bool threw=false;
            try { weapons.LoadSession(); } catch(ArgumentException) { threw=true; }
            Check(!threw && Mathf.Approximately(weapons.Energy,energy) && Mathf.Approximately(weapons.Water,water) && weapons.Food==food && weapons.Seeds==seeds && weapons.Metal==metal && weapons.MetalAmmo==ammo && (bool)dirtyField.GetValue(weapons),"Malformed save is rejected without throwing or changing inventory");
        }
        finally
        {
            keyField.SetValue(weapons,originalKey);dirtyField.SetValue(weapons,originalDirty);
            if(hadKey)PlayerPrefs.SetString(key,priorValue);else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
    EnemyBrain Enemy(EnemyBrain.Species species)
    {
        var g=new GameObject(species.ToString());g.transform.position=new Vector3(0,0.03f,4);
        var cc=g.AddComponent<CharacterController>();cc.height=1.4f;cc.center=Vector3.up*0.7f;cc.radius=0.3f;
        var e=g.AddComponent<EnemyBrain>();e.species=species;e.enabled=false;cc.Move(Vector3.zero);Physics.SyncTransforms();return e;
    }
    void Check(bool success,string label)
    {
        if(!success){File.WriteAllText("weapon-check-failed.txt",report+"FAIL "+label);EditorApplication.Exit(1);throw new Exception(label);}
        report+="PASS "+label+"\n";Debug.Log("WEAPON CHECK "+label);
    }
    void Error(string message,string stack,LogType type)
    {
        if(type==LogType.Error || type==LogType.Exception){File.WriteAllText("weapon-check-failed.txt",report+message+"\n"+stack);EditorApplication.Exit(1);}
    }
}

