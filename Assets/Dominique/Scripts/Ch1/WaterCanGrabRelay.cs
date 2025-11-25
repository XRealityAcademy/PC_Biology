using UnityEngine;

public class WaterCanGrabRelay : MonoBehaviour
{
    [SerializeField] private Manager_Ch1 manager;

    void Awake()
    {
        if (!manager) manager = FindFirstObjectByType<Manager_Ch1>();
    }

    // Hook this to XR Interaction Toolkit "Select Entered" event on the watering can
    public void OnGrabbed()
    {
        if (!manager)
        {
            Debug.LogWarning($"{name}: Manager_Ch1 not set/found; cannot notify grab.");
            return;
        }
        manager.NotifyWaterCanGrabbed(); // ensure this method exists in Manager_Ch1
    }

    // Keep your setter if you wire it from code
    public void SetManager(Manager_Ch1 m) => manager = m;
}
