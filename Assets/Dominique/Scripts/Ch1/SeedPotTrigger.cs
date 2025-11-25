using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SeedPotTrigger : MonoBehaviour
{
    [Tooltip("Must match your seed prefab tag (e.g., 'Seed').")]
    public string seedTag = "Seed";

    Manager_Ch1 manager;
    bool hasSeed = false;
    public bool IsSeeded => hasSeed;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true; // trigger volume over the soil
    }

    public void SetManager(Manager_Ch1 m) => manager = m;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[SeedPotTrigger] OnTriggerEnter: {other.name} (Tag: {other.tag}), hasSeed: {hasSeed}, seedTag: {seedTag}");
        
        if (hasSeed)
        {
            Debug.Log($"[SeedPotTrigger] Pot {gameObject.name} already has a seed, ignoring");
            return;                     // only the first seed counts
        }
        
        if (!other.CompareTag(seedTag))
        {
            Debug.Log($"[SeedPotTrigger] Tag mismatch. Expected '{seedTag}', got '{other.tag}'");
            return;  // must be a Seed
        }
        
        if (!other.attachedRigidbody)
        {
            Debug.Log($"[SeedPotTrigger] Object {other.name} has no Rigidbody, ignoring");
            return;    // ensure it's a physical object
        }

        hasSeed = true;
        Debug.Log($"[SeedPotTrigger] Seed detected in pot {gameObject.name}! Notifying manager.");
        
        if (manager)
        {
            manager.NotifySeedPlaced(this);
        }
        else
        {
            Debug.LogError($"[SeedPotTrigger] Manager is null on pot {gameObject.name}! Cannot notify seed placement.");
        }
    }
}
