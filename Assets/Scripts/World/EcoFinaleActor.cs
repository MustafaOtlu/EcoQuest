using UnityEngine;

// Marks encounter-only actors and keeps their projectiles inside the same lifetime.
public sealed class EcoFinaleActor : MonoBehaviour
{
    public EcoFinale Owner { get; private set; }
    public void Initialize(EcoFinale owner) => Owner = owner;
    public void AttachTransient(GameObject instance)
    {
        if (instance != null && Owner != null && Owner.EncounterRoot != null)
            instance.transform.SetParent(Owner.EncounterRoot, true);
    }
}
