using UnityEngine;

/// <summary>
/// Helper script to convert SeedPotTrigger GameObjects to use CompoundXPot instead.
/// Attach this to a GameObject in your scene, configure it, and click the button in the Inspector.
/// </summary>
public class SeedPotToCompoundXPotConverter : MonoBehaviour
{
    [Header("Seed Zone Triggers")]
    [Tooltip("Drag the Trigger_Dialog_SeedZone GameObjects here (01 through 06)")]
    public GameObject[] seedZoneTriggers = new GameObject[6];

    [Header("Manager Reference")]
    [Tooltip("Drag your Manager_Ch3 GameObject here")]
    public Manager_Ch3 manager;

    [Header("Required Counts")]
    [Tooltip("Required counts for each pot (Pot1: 0, Pot2: 1, Pot3: 3, Pot4: 5, Pot5: 7, Pot6: 9)")]
    public int[] requiredCounts = new int[] { 0, 1, 3, 5, 7, 9 };

    [Header("Tag Settings")]
    [Tooltip("Tag that CompoundX objects use (should be 'CompoundX')")]
    public string compoundXTag = "CompoundX";

    [ContextMenu("Convert SeedPotTriggers to CompoundXPot")]
    public void ConvertToCompoundXPot()
    {
        if (manager == null)
        {
            Debug.LogError("Manager_Ch3 is not assigned!");
            return;
        }

        if (seedZoneTriggers == null || seedZoneTriggers.Length != 6)
        {
            Debug.LogError("Need exactly 6 seed zone triggers!");
            return;
        }

        if (requiredCounts == null || requiredCounts.Length != 6)
        {
            Debug.LogError("Need exactly 6 required counts!");
            return;
        }

        CompoundXPot[] pots = new CompoundXPot[6];

        for (int i = 0; i < 6; i++)
        {
            if (seedZoneTriggers[i] == null)
            {
                Debug.LogWarning($"SeedZoneTrigger {i + 1} is null, skipping...");
                continue;
            }

            GameObject go = seedZoneTriggers[i];

            // Remove SeedPotTrigger component if it exists
            SeedPotTrigger seedTrigger = go.GetComponent<SeedPotTrigger>();
            if (seedTrigger != null)
            {
                Debug.Log($"Removing SeedPotTrigger from {go.name}");
#if UNITY_EDITOR
                DestroyImmediate(seedTrigger);
#else
                Destroy(seedTrigger);
#endif
            }

            // Remove XPotTrigger if it exists (cleanup)
            XPotTrigger xTrigger = go.GetComponent<XPotTrigger>();
            if (xTrigger != null)
            {
                Debug.Log($"Removing XPotTrigger from {go.name}");
#if UNITY_EDITOR
                DestroyImmediate(xTrigger);
#else
                Destroy(xTrigger);
#endif
            }

            // Add or get CompoundXPot component
            CompoundXPot pot = go.GetComponent<CompoundXPot>();
            if (pot == null)
            {
                pot = go.AddComponent<CompoundXPot>();
                Debug.Log($"Added CompoundXPot to {go.name}");
            }

            // Configure the pot
            pot.requiredCount = requiredCounts[i];
            pot.compoundXTag = compoundXTag;

            // Ensure collider is a trigger
            Collider col = go.GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogWarning($"{go.name} has no Collider! Adding BoxCollider...");
                col = go.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;

            pots[i] = pot;
            Debug.Log($"Configured {go.name}: requiredCount = {requiredCounts[i]}, tag = {compoundXTag}");
        }

        // Assign to manager
        manager.pots = pots;
        Debug.Log("✓ Successfully converted all SeedPotTriggers to CompoundXPot and assigned to Manager_Ch3!");
        Debug.Log("Remember to save your scene!");
    }

    [ContextMenu("Find SeedZone Triggers Automatically")]
    public void FindSeedZoneTriggers()
    {
        seedZoneTriggers = new GameObject[6];
        for (int i = 1; i <= 6; i++)
        {
            string name = $"Trigger_Dialog_SeedZone_{i:D2}";
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                seedZoneTriggers[i - 1] = found;
                Debug.Log($"Found {name}");
            }
            else
            {
                Debug.LogWarning($"Could not find {name}");
            }
        }

        // Auto-find manager if not assigned
        if (manager == null)
        {
#if UNITY_2021_3_OR_NEWER
            manager = FindFirstObjectByType<Manager_Ch3>();
#else
            manager = FindObjectOfType<Manager_Ch3>();
#endif
            if (manager != null)
            {
                Debug.Log($"Found Manager_Ch3: {manager.name}");
            }
        }
    }
}

