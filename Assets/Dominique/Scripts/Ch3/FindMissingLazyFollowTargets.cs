using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Helper script to find all LazyFollow components with unassigned targets.
/// Attach to any GameObject and use the context menu to find problematic LazyFollow components.
/// </summary>
public class FindMissingLazyFollowTargets : MonoBehaviour
{
    [ContextMenu("Find LazyFollow Components with Missing Targets")]
    public void FindMissingTargets()
    {
        LazyFollow[] allLazyFollows = FindObjectsByType<LazyFollow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        int missingCount = 0;
        int assignedCount = 0;
        
        Debug.Log($"=== Searching for LazyFollow components ===");
        Debug.Log($"Total LazyFollow components found: {allLazyFollows.Length}");
        
        foreach (LazyFollow lazyFollow in allLazyFollows)
        {
            // Try to access m_Target using reflection
            var targetField = typeof(LazyFollow).GetField("m_Target", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Transform target = null;
            
            if (targetField != null)
            {
                target = targetField.GetValue(lazyFollow) as Transform;
            }
            else
            {
                // Try public property
                var targetProperty = typeof(LazyFollow).GetProperty("target");
                if (targetProperty != null)
                {
                    target = targetProperty.GetValue(lazyFollow) as Transform;
                }
            }
            
            if (target == null)
            {
                missingCount++;
                string fullPath = GetFullPath(lazyFollow.transform);
                Debug.LogError($"❌ MISSING TARGET: {lazyFollow.gameObject.name}\n   Path: {fullPath}\n   Click this message to select in Hierarchy.", lazyFollow.gameObject);
            }
            else
            {
                assignedCount++;
                Debug.Log($"✓ {lazyFollow.gameObject.name} → {target.name}", lazyFollow.gameObject);
            }
        }
        
        Debug.Log($"=== Summary ===");
        Debug.Log($"✓ Assigned: {assignedCount}");
        Debug.LogError($"❌ Missing: {missingCount}");
        
        if (missingCount > 0)
        {
            Debug.LogWarning($"Found {missingCount} LazyFollow component(s) with missing targets. Click the red error messages above to select them in the Hierarchy, then assign the Target field in the Inspector.");
        }
        else
        {
            Debug.Log("✓ All LazyFollow components have assigned targets!");
        }
    }
    
    private string GetFullPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}

