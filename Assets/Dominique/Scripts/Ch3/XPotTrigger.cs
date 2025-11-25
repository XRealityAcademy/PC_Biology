using UnityEngine;

[RequireComponent(typeof(Collider))]
public class XPotTrigger : MonoBehaviour
{
    [Tooltip("Must match your X/Compound X prefab tag (e.g., 'X' or 'CompoundX').")]
    public string xTag = "X";

    [Tooltip("The required amount of X for this pot (e.g., 0, 2, 4, 6, 8, 10 grams).")]
    public float requiredAmount = 0f;

    [Tooltip("How much X this pot currently has.")]
    public float currentAmount = 0f;

    Manager_Ch3 manager;
    public bool HasCorrectAmount => Mathf.Approximately(currentAmount, requiredAmount);
    public bool HasAnyX => currentAmount > 0f;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true; // trigger volume over the pot
    }

    void Awake()
    {
        // Auto-find manager if not assigned
        if (!manager)
            manager = FindFirstObjectByType<Manager_Ch3>();
    }

    public void SetManager(Manager_Ch3 m) => manager = m;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[XPotTrigger] OnTriggerEnter: {other.name} (Tag: {other.tag}), currentAmount: {currentAmount}, requiredAmount: {requiredAmount}, xTag: {xTag}");
        
        if (!other.CompareTag(xTag))
        {
            Debug.Log($"[XPotTrigger] Tag mismatch. Expected '{xTag}', got '{other.tag}'");
            return;  // must be an X object
        }
        
        if (!other.attachedRigidbody)
        {
            Debug.Log($"[XPotTrigger] Object {other.name} has no Rigidbody, ignoring");
            return;    // ensure it's a physical object
        }

        // Get the amount from the X object (if it has a component that stores amount)
        XAmount xAmount = other.GetComponent<XAmount>();
        if (xAmount == null)
        {
            xAmount = other.GetComponentInParent<XAmount>();
        }
        
        float amountToAdd = xAmount != null ? xAmount.amount : 1f; // Default to 1 if no XAmount component
        
        currentAmount += amountToAdd;
        Debug.Log($"[XPotTrigger] X detected in pot {gameObject.name}! Added {amountToAdd}. Current: {currentAmount}/{requiredAmount}");
        
        if (manager)
        {
            manager.NotifyXPlaced(this);
        }
        else
        {
            Debug.LogError($"[XPotTrigger] Manager is null on pot {gameObject.name}! Cannot notify X placement.");
        }
    }
}

