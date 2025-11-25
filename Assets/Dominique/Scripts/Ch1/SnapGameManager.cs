using UnityEngine;
using UnityEngine.Events;

public class SnapGameManager : MonoBehaviour
{
    [SerializeField] private int totalSlots = 6;
    [SerializeField] private UnityEvent onAllSnapped;

    // Removed static instance to prevent conflicts between scenes
    // Each scene's SnapGameManager will work independently
    int filled;

    void Awake()
    {
        // Reset state when scene loads (in case of additive loading)
        filled = 0;
    }

    public void FlagSlotFilled()
    {
        if (++filled >= totalSlots)
            onAllSnapped?.Invoke();
    }
}
