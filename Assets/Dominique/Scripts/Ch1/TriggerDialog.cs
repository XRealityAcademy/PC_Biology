using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[AddComponentMenu("XR/Dialog/Trigger Dialog")]
public class TriggerDialog : MonoBehaviour
{
    [Tooltip("Dialog index to play on activation.")]
    public int dialogIndex = 4;

    [Tooltip("Reference to Manager_Ch1 (for Chapter 1 scenes). If null, will auto-find one in the scene.")]
    public Manager_Ch1 managerCh1;

    [Tooltip("Reference to Manager_Ch3 (for Chapter 3 scenes). If null, will auto-find one in the scene.")]
    public Manager_Ch3 managerCh3;

    [Header("Collision Activation")]
    [Tooltip("Only activate when this tag enters the trigger.")]
    public string playerTag = "Player";

    [Tooltip("Allow activation only once.")]
    public bool oneShot = true;

    [Header("Ray & Grab Activation (optional)")]
    [Tooltip("Controller or hand transform used as the ray origin.")]
    public Transform rightRayOrigin;            // controller/hand

    [Tooltip("Custom input wrapper exposing IsGrabPressed.")]
    public CustomInputAction rightHandInput;    // must expose bool IsGrabPressed

    [SerializeField, Tooltip("Max distance for the dialog raycast.")]
    private float maxRayDistance = 10f;

    [SerializeField, Tooltip("Layers the dialog raycast will consider. If left empty, defaults to a layer named 'DialogTrigger' if it exists.")]
    private LayerMask raycastMask;

    // ───────── Internals ─────────
    Collider _col;
    bool _fired;

    void Awake()
    {
        // Ensure trigger collider
        _col = GetComponent<Collider>();
        if (_col == null)
        {
            Debug.LogError($"[TriggerDialog] No Collider found on {gameObject.name}. Adding BoxCollider.");
            _col = gameObject.AddComponent<BoxCollider>();
        }
        _col.isTrigger = true;

        // Fallback: if mask not set in Inspector, try 'DialogTrigger' to mirror your old setup
        if (raycastMask.value == 0)
            raycastMask = LayerMask.GetMask("DialogTrigger");

        // Auto-find manager if not assigned (try Ch1 first, then Ch3)
        if (!managerCh1 && !managerCh3)
        {
            managerCh1 = FindFirstObjectByType<Manager_Ch1>();
            if (!managerCh1)
                managerCh3 = FindFirstObjectByType<Manager_Ch3>();
        }
        
        // Debug validation
        if (!managerCh1 && !managerCh3)
        {
            Debug.LogWarning($"[TriggerDialog] No Manager_Ch1 or Manager_Ch3 found in scene. Make sure one exists or assign it in the Inspector.");
        }
        else
        {
            Debug.Log($"[TriggerDialog] Manager found: {(managerCh1 ? managerCh1.name : managerCh3.name)}");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired && oneShot) return;
        
        // Debug: Log when something enters the trigger
        Debug.Log($"[TriggerDialog] OnTriggerEnter: {other.name} (Tag: {other.tag})");
        
        // If playerTag is empty/null, trigger on any object
        // Otherwise, only trigger if tag matches
        bool shouldTrigger = string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag);
        
        if (shouldTrigger)
        {
            Debug.Log($"[TriggerDialog] Trigger condition met for {other.name}. Activating dialog {dialogIndex}.");
            Activate();
        }
        else
        {
            Debug.Log($"[TriggerDialog] Tag mismatch. Expected '{playerTag}', got '{other.tag}'");
        }
    }

    void Update()
    {
        if (_fired && oneShot) return;
        if (!rightRayOrigin || !rightHandInput || !rightHandInput.IsGrabPressed) return;

        // Raycast from the controller/hand forward
        if (Physics.Raycast(rightRayOrigin.position,
                            rightRayOrigin.forward,
                            out var hit,
                            maxRayDistance,
                            raycastMask.value == 0 ? ~0 : raycastMask,
                            QueryTriggerInteraction.Collide))
        {
            if (hit.collider == _col)
                Activate();
        }
    }

    void Activate()
    {
        if (_fired && oneShot)
        {
            Debug.Log($"[TriggerDialog] Already fired (oneShot=true). Skipping activation.");
            return;
        }
        
        if (!managerCh1 && !managerCh3)
        {
            Debug.LogError($"[TriggerDialog] No Manager_Ch1 or Manager_Ch3 found; cannot play dialog index {dialogIndex}.");
            return;
        }

        // Special check for Manager_Ch3: Dialog 10 (index 9) is gated by X placement completion
        if (managerCh3 && dialogIndex == 9)
        {
            // Check if all X amounts are correct before allowing dialog 10 to trigger
            bool allXComplete = true;
            if (managerCh3.pots != null && managerCh3.pots.Length > 0)
            {
                for (int i = 0; i < managerCh3.pots.Length; i++)
                {
                    if (managerCh3.pots[i] != null && !managerCh3.pots[i].isX)
                    {
                        allXComplete = false;
                        break;
                    }
                }
            }
            
            if (!allXComplete)
            {
                Debug.Log($"[TriggerDialog] Dialog 10 (index 9) is gated. Not all X amounts are correct yet. Skipping activation.");
                return;
            }
        }

        Debug.Log($"[TriggerDialog] Activating dialog index {dialogIndex} via {(managerCh1 ? "Manager_Ch1" : "Manager_Ch3")}.");
        _fired = true;
        
        // Use appropriate method based on which manager is available
        if (managerCh1)
        {
            managerCh1.PlayDialogByIndex(dialogIndex);
        }
        else if (managerCh3)
        {
            // Use ForcePlayDialogByIndex to stop autoplay and play the triggered dialog
            managerCh3.ForcePlayDialogByIndex(dialogIndex);
        }
    }

    // Optional helper if you ever need to re-arm this trigger at runtime
    public void ResetTrigger()
    {
        _fired = false;
    }
}
