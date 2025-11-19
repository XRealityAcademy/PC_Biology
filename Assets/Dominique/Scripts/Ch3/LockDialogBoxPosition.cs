using UnityEngine;

/// <summary>
/// Locks the Dialog Box to a fixed world position, preventing it from following the player.
/// Attach this to the Dialog Box GameObject.
/// </summary>
public class LockDialogBoxPosition : MonoBehaviour
{
    [Header("Position Lock")]
    [Tooltip("Lock position to the current world position on Start")]
    public bool lockPositionOnStart = true;
    
    [Tooltip("Fixed world position (if lockPositionOnStart is false, use this)")]
    public Vector3 fixedWorldPosition;
    
    [Tooltip("Lock rotation as well")]
    public bool lockRotation = false;
    
    private Vector3 lockedPosition;
    private Quaternion lockedRotation;
    private bool isLocked = false;

    void Start()
    {
        InitializeLock();
    }

    void OnEnable()
    {
        // Re-initialize lock when object becomes active (important for objects that start inactive)
        InitializeLock();
    }

    void InitializeLock()
    {
        if (lockPositionOnStart)
        {
            // If fixedWorldPosition is set (not zero), use it; otherwise use current position
            if (fixedWorldPosition != Vector3.zero)
            {
                lockedPosition = fixedWorldPosition;
                transform.position = fixedWorldPosition; // Set immediately
            }
            else
            {
                // Lock to current position in world space
                lockedPosition = transform.position;
            }
            lockedRotation = transform.rotation;
        }
        else
        {
            // Use specified fixed position
            lockedPosition = fixedWorldPosition;
            transform.position = fixedWorldPosition; // Set immediately
            lockedRotation = transform.rotation;
        }
        
        isLocked = true;
        
        // Make sure we're not a child of something that moves
        if (transform.parent != null)
        {
            Debug.LogWarning($"[LockDialogBoxPosition] '{gameObject.name}' is a child of '{transform.parent.name}'. " +
                           $"If the parent moves, this object will move with it. Consider making it a root object.");
        }
    }

    void LateUpdate()
    {
        if (isLocked)
        {
            // Force position to stay locked
            if (transform.position != lockedPosition)
            {
                transform.position = lockedPosition;
            }
            
            // Force rotation to stay locked if enabled
            if (lockRotation && transform.rotation != lockedRotation)
            {
                transform.rotation = lockedRotation;
            }
        }
    }

    /// <summary>
    /// Manually set a new locked position
    /// </summary>
    public void SetLockedPosition(Vector3 newPosition)
    {
        lockedPosition = newPosition;
        transform.position = newPosition;
    }

    /// <summary>
    /// Unlock the position (allow it to move)
    /// </summary>
    public void UnlockPosition()
    {
        isLocked = false;
    }

    /// <summary>
    /// Lock the position again
    /// </summary>
    public void LockPosition()
    {
        lockedPosition = transform.position;
        isLocked = true;
    }
}

