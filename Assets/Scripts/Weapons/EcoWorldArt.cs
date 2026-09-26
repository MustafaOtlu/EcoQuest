using UnityEngine;

// Prepared prefabs share ground-level pivots and metre-based dimensions.
public static class EcoWorldArt
{
    public static GameObject Spawn(string model, Vector3 position, Transform parent = null)
    {
        var prefab = Resources.Load<GameObject>("EcoArt/" + model);
        if (prefab == null)
        {
            Debug.LogError("EcoQuest art prefab is missing: " + model);
            return null;
        }
        return Object.Instantiate(prefab, position, Quaternion.identity, parent);
    }
}
