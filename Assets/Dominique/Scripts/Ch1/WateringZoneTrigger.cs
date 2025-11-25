using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WateringZoneTrigger : MonoBehaviour
{
    [Tooltip("Hook up your scene's Manager_Ch1 here.")]
    public Manager_Ch1 manager;

    [Header("Detection")]
    [Tooltip("Tag on the watering can's tip mesh/cube.")]
    public string acceptedTag = "WaterCanTip";

    bool fired = false;

    void Reset()
    {
        // Make this a trigger so a *non-trigger* WaterCanTip collider can enter it
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        if (!manager) manager = FindFirstObjectByType<Manager_Ch1>();
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[WateringZoneTrigger] OnTriggerEnter: {other.name} (Tag: {other.tag}), fired: {fired}, acceptedTag: {acceptedTag}");
        
        if (!other.CompareTag(acceptedTag))
        {
            Debug.Log($"[WateringZoneTrigger] Tag mismatch. Expected '{acceptedTag}', got '{other.tag}'");
            return;
        }

        Debug.Log($"[WateringZoneTrigger] Water can tip detected! Calling NotifyWateringDone()");

        // Tell your existing manager: watering is done → jump to 13
        // (This uses the method name from your current Manager_Ch1.)
        if (manager)
        {
            manager.NotifyWateringDone();
            
            // Only mark as fired if dialog 11 was successfully triggered
            // This allows re-triggering if seeds weren't ready yet
            // We'll check if dialog 11 was played by checking if it's no longer waiting
            // For now, we'll allow multiple triggers - the manager will handle the logic
        }
        else
        {
            Debug.LogError($"[WateringZoneTrigger] Manager is null! Cannot notify watering done.");
        }
    }
}
