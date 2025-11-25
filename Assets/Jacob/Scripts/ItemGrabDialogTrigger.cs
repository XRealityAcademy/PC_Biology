using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Triggers dialog when this item is grabbed in PC mode.
/// Attach this to any grabbable item (pot, seed, ruler, water can, etc.)
/// PCInteractor will automatically call OnPCGrabbed() when the item is picked up.
/// </summary>
public class ItemGrabDialogTrigger : MonoBehaviour
{
    [Header("Dialog Settings")]
    [Tooltip("Dialog index to play when this item is grabbed (e.g., 4 for pot, 5 for seed, etc.)")]
    public int dialogIndex = 8;

    [Tooltip("Reference to Manager_Ch1 (for Chapter 1 scenes). If null, will auto-find one in the scene.")]
    public Manager_Ch1 managerCh1;

    [Tooltip("Reference to Manager_Ch3 (for Chapter 3 scenes). If null, will auto-find one in the scene.")]
    public Manager_Ch3 managerCh3;

    [Header("Settings")]
    [Tooltip("Only trigger dialog once (even if item is grabbed multiple times)")]
    public bool oneShot = true;

    [Header("Shared Trigger Group")]
    [Tooltip("If set, items with the same group name share a single trigger. " +
             "Example: Set all seeds to group 'Seed' - first seed grabbed triggers dialog, " +
             "other seeds won't trigger. Leave empty for per-item triggering.")]
    public string sharedTriggerGroup = "";

    // Static tracking for shared trigger groups - scene-specific to prevent cross-scene conflicts
    private static HashSet<string> triggeredGroups = new HashSet<string>();
    private static string lastSceneName = "";

    // Private state
    private bool hasTriggered = false;

    void Awake()
    {
        // Clear trigger groups when a new scene loads to prevent cross-scene state pollution
        string currentSceneName = SceneManager.GetActiveScene().name;
        if (lastSceneName != currentSceneName)
        {
            triggeredGroups.Clear();
            lastSceneName = currentSceneName;
            Debug.Log($"[ItemGrabDialogTrigger] New scene detected: {currentSceneName}. Cleared trigger groups.");
        }

        // Auto-find manager if not assigned (try Ch1 first, then Ch3)
        if (!managerCh1 && !managerCh3)
        {
            managerCh1 = FindFirstObjectByType<Manager_Ch1>();
            if (!managerCh1)
                managerCh3 = FindFirstObjectByType<Manager_Ch3>();
        }
    }

    /// <summary>
    /// Called from PCInteractor when this item is grabbed on PC.
    /// PCInteractor automatically calls this method when it successfully grabs an object.
    /// </summary>
    public void OnPCGrabbed()
    {
        Debug.Log($"[ItemGrabDialogTrigger] OnPCGrabbed() called on {gameObject.name}. Dialog Index: {dialogIndex}, Shared Group: '{sharedTriggerGroup}'");
        TriggerDialog();
    }

    /// <summary>
    /// Main method to trigger the dialog
    /// </summary>
    private void TriggerDialog()
    {
        // Check shared trigger group first
        if (!string.IsNullOrEmpty(sharedTriggerGroup))
        {
            if (triggeredGroups.Contains(sharedTriggerGroup))
            {
                Debug.Log($"[ItemGrabDialogTrigger] {gameObject.name} - Shared group '{sharedTriggerGroup}' already triggered. Skipping.");
                return;
            }
        }
        else
        {
            // Check per-item one-shot
            if (hasTriggered && oneShot)
            {
                Debug.Log($"[ItemGrabDialogTrigger] {gameObject.name} already triggered dialog (oneShot=true). Skipping.");
                return;
            }
        }

        // Check if manager exists
        if (!managerCh1 && !managerCh3)
        {
            Debug.LogWarning($"[ItemGrabDialogTrigger] {gameObject.name}: No Manager_Ch1 or Manager_Ch3 found. Cannot trigger dialog {dialogIndex}.");
            return;
        }

        Debug.Log($"[ItemGrabDialogTrigger] {gameObject.name} was grabbed! Triggering dialog index {dialogIndex}.");
        
        // Mark as triggered
        hasTriggered = true;
        if (!string.IsNullOrEmpty(sharedTriggerGroup))
        {
            triggeredGroups.Add(sharedTriggerGroup);
            Debug.Log($"[ItemGrabDialogTrigger] Shared group '{sharedTriggerGroup}' marked as triggered.");
        }
        
        // Use appropriate method based on which manager is available
        if (managerCh1)
        {
            // Use ForcePlayDialogByIndex to bypass order checks for grab-triggered dialogs
            managerCh1.ForcePlayDialogByIndex(dialogIndex);
        }
        else if (managerCh3)
        {
            // Use ForcePlayDialogByIndex to stop autoplay and play the triggered dialog
            managerCh3.ForcePlayDialogByIndex(dialogIndex);
        }
    }

    /// <summary>
    /// Reset the trigger (useful if you want to allow re-triggering)
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
        if (!string.IsNullOrEmpty(sharedTriggerGroup))
        {
            triggeredGroups.Remove(sharedTriggerGroup);
        }
    }

    /// <summary>
    /// Reset a specific shared trigger group (useful for resetting all seeds, etc.)
    /// </summary>
    public static void ResetSharedGroup(string groupName)
    {
        triggeredGroups.Remove(groupName);
        Debug.Log($"[ItemGrabDialogTrigger] Reset shared group '{groupName}'.");
    }
}

