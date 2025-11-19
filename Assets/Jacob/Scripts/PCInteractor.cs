using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class PCInteractor : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("The Input Action Asset containing the PC Player action map")]
    public InputActionAsset inputActionAsset;
    
    [Tooltip("Name of the Grab action in the PC Player action map")]
    public string grabActionName = "Grab";
    
    [Tooltip("Name of the Rotate action in the PC Player action map (optional)")]
    public string rotateActionName = "Rotate";
    
    [Tooltip("Name of the Reset Rotation action in the PC Player action map (optional)")]
    public string resetRotationActionName = "ResetRotation";
    
    [Header("Interaction")]
    [Tooltip("Maximum distance for interaction")]
    public float interactDistance = 5f;
    
    [Tooltip("The camera to raycast from (usually the main camera)")]
    public Camera playerCamera;
    
    [Header("Grab Settings")]
    [Tooltip("Distance to hold grabbed objects from camera (forward distance to center of screen)")]
    public float holdDistance = 1.5f;
    
    [Tooltip("Minimum distance the object can be held from camera")]
    public float minHoldDistance = 0.7f;
    
    [Tooltip("Maximum distance the object can be held from camera")]
    public float maxHoldDistance = 2f;
    
    [Tooltip("How fast the distance changes when scrolling (units per scroll step)")]
    public float scrollSensitivity = 0.2f;
    
    [Tooltip("Vertical offset for held objects (positive = up, negative = lower). Adjust to align object center with screen center.")]
    public float holdVerticalOffset = 0f;
    
    [Tooltip("Speed at which objects move when grabbed")]
    public float grabMoveSpeed = 10f;
    
    [Header("Rotation Settings")]
    [Tooltip("Speed at which objects rotate when rotation key is held")]
    public float rotationSpeed = 90f; // degrees per second
    
    [Tooltip("Which axis to rotate around (Y = horizontal rotation, X = vertical, Z = roll)")]
    public Vector3 rotationAxis = Vector3.right; // Default: rotate around X axis (vertical)
    
    [Tooltip("Layers that can be grabbed")]
    public LayerMask grabableLayers = -1; // Everything by default
    
    private InputAction grabAction;
    private InputAction rotateAction;
    private InputAction resetRotationAction;
    private GameObject grabbedObject;
    private Rigidbody grabbedRigidbody;
    private bool wasGrabbingLastFrame = false;
    private Vector3 grabOffset;
    private Quaternion originalRotation;
    private bool wasKinematic;
    private RigidbodyConstraints originalConstraints;
    private bool isReleasingForSnap = false; // Flag to prevent velocity on snap release

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Find and enable the grab action
        if (inputActionAsset != null)
        {
            InputActionMap pcPlayerMap = inputActionAsset.FindActionMap("PC Player");
            if (pcPlayerMap != null)
            {
                grabAction = pcPlayerMap.FindAction(grabActionName);
                if (grabAction != null)
                {
                    grabAction.Enable();
                    Debug.Log($"<color=cyan>PCInteractor: Grab action '{grabActionName}' found and enabled!</color>");
                }
                else
                {
                    Debug.LogError($"PCInteractor: Action '{grabActionName}' not found in PC Player action map!");
                }
                
                // Find and enable rotate action (optional)
                rotateAction = pcPlayerMap.FindAction(rotateActionName);
                if (rotateAction != null)
                {
                    rotateAction.Enable();
                    Debug.Log($"<color=cyan>PCInteractor: Rotate action '{rotateActionName}' found and enabled!</color>");
                }
                else
                {
                    Debug.LogWarning($"PCInteractor: Rotate action '{rotateActionName}' not found. Rotation will use default key (R key).");
                }
                
                // Find and enable reset rotation action (optional)
                resetRotationAction = pcPlayerMap.FindAction(resetRotationActionName);
                if (resetRotationAction != null)
                {
                    resetRotationAction.Enable();
                    Debug.Log($"<color=cyan>PCInteractor: Reset Rotation action '{resetRotationActionName}' found and enabled!</color>");
                }
                else
                {
                    Debug.LogWarning($"PCInteractor: Reset Rotation action '{resetRotationActionName}' not found. Reset will use default key (R key).");
                }
            }
            else
            {
                Debug.LogError("PCInteractor: 'PC Player' action map not found in Input Action Asset!");
            }
        }
        else
        {
            Debug.LogWarning("PCInteractor: Input Action Asset is not assigned!");
        }
        
        // Auto-find camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                playerCamera = FindFirstObjectByType<Camera>();
            }
        }
        
        if (playerCamera != null)
        {
            Debug.Log($"<color=cyan>PCInteractor: Camera found: {playerCamera.name}</color>");
        }
        else
        {
            Debug.LogWarning("PCInteractor: No camera found! Raycast will not work.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Debug: Check action state
        if (grabAction == null)
        {
            // Only log once to avoid spam
            if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
            {
                Debug.LogWarning("PCInteractor: grabAction is null!");
            }
            return;
        }
        
        if (!grabAction.enabled)
        {
            if (Time.frameCount % 300 == 0)
            {
                Debug.LogWarning("PCInteractor: grabAction is not enabled!");
            }
            return;
        }
        
        // Check if grab action was triggered (left click equivalent)
        bool wasPressed = grabAction.WasPressedThisFrame();
        bool isPressed = grabAction.IsPressed();
        bool wasReleased = wasGrabbingLastFrame && !isPressed;
        
        // Don't grab if clicking on UI elements (let EventSystem handle it)
        if (wasPressed)
        {
            // Check if we're clicking on UI
            UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject())
            {
                Debug.Log("<color=cyan>PCInteractor: Click detected on UI element, skipping grab</color>");
                return; // Let UI handle the click
            }
            
            TryGrab();
        }
        // Handle grab release
        else if (wasReleased)
        {
            ReleaseGrab();
        }
        
        // Handle rotation input
        if (grabbedObject != null)
        {
            bool shouldRotate = false;
            
            // Check if rotate action is pressed (from input system)
            if (rotateAction != null && rotateAction.IsPressed())
            {
                shouldRotate = true;
            }
            // Fallback: Check for right mouse button (R key is now used for reset)
            else if (Input.GetMouseButton(1))
            {
                shouldRotate = true;
            }
            
            if (shouldRotate)
            {
                // Rotate the object around the specified axis
                // Using Space.Self to rotate around the object's local X axis
                float rotationAmount = rotationSpeed * Time.deltaTime;
                grabbedObject.transform.Rotate(rotationAxis, rotationAmount, Space.Self);
            }
            
            // Check for reset rotation input
            bool shouldResetRotation = false;
            
            // Check if reset rotation action is pressed (from input system)
            if (resetRotationAction != null && resetRotationAction.WasPressedThisFrame())
            {
                shouldResetRotation = true;
            }
            // Fallback: Check for R key or middle mouse button
            else if (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(2))
            {
                shouldResetRotation = true;
            }
            
            if (shouldResetRotation)
            {
                // Reset rotation to original orientation
                grabbedObject.transform.rotation = originalRotation;
                Debug.Log($"<color=cyan>PCInteractor: Reset rotation to original orientation</color>");
            }
            
            // Handle scroll wheel to adjust hold distance
            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                // Scroll up (positive) = move farther away, Scroll down (negative) = move closer
                holdDistance += scrollDelta * scrollSensitivity;
                // Clamp to min/max range
                holdDistance = Mathf.Clamp(holdDistance, minHoldDistance, maxHoldDistance);
            }
        }
        
        // Update grab state
        wasGrabbingLastFrame = isPressed;
    }
    
    private void TryGrab()
    {
        if (playerCamera == null) return;
        
        // If already grabbing something, release it first
        if (grabbedObject != null)
        {
            ReleaseGrab();
            return;
        }
        
        // Raycast from camera to see what we're looking at
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        
        // Debug: Draw ray in scene view
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red, 1f);
        
        // Use RaycastAll to get all objects hit, then prioritize by name/type
        RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance, grabableLayers);
        
        if (hits.Length == 0)
        {
            // Debug: Show why raycast failed
            Debug.Log($"<color=red>GRAB TRIGGERED!</color> Raycast didn't hit anything within {interactDistance}m distance.");
            Debug.Log($"<color=gray>Camera position: {playerCamera.transform.position}, Forward: {playerCamera.transform.forward}</color>");
            return;
        }
        
        // Sort by distance (closest first)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        // Filter out trigger colliders that are only for dialog triggers (they shouldn't block grabbing)
        List<RaycastHit> validHits = new List<RaycastHit>();
        foreach (var hitResult in hits)
        {
            // Skip trigger colliders that have TriggerDialog component (these are zone triggers, not grabbable objects)
            if (hitResult.collider.isTrigger)
            {
                TriggerDialog triggerDialog = hitResult.collider.GetComponent<TriggerDialog>();
                if (triggerDialog != null)
                {
                    Debug.Log($"<color=gray>Skipping trigger collider '{hitResult.collider.gameObject.name}' (TriggerDialog zone, not grabbable)</color>");
                    continue;
                }
            }
            validHits.Add(hitResult);
        }
        
        if (validHits.Count == 0)
        {
            Debug.Log($"<color=yellow>No grabbable objects found (all hits were trigger zones).</color>");
            return;
        }
        
        // Prioritize items with shared trigger groups (seeds, compound x, etc.) over other objects
        RaycastHit? prioritizedHit = null;
        Debug.Log($"<color=magenta>RaycastAll found {validHits.Count} valid objects (filtered from {hits.Length} total). Checking for prioritization...</color>");
        
        foreach (var hitResult in validHits)
        {
            GameObject testObject = hitResult.collider.gameObject;
            string objName = testObject.name.ToLower();
            
            // Check if this object or its parents/children have ItemGrabDialogTrigger
            ItemGrabDialogTrigger trigger = testObject.GetComponent<ItemGrabDialogTrigger>();
            if (trigger == null)
            {
                trigger = testObject.GetComponentInParent<ItemGrabDialogTrigger>();
            }
            if (trigger == null)
            {
                trigger = testObject.GetComponentInChildren<ItemGrabDialogTrigger>();
            }
            
            if (trigger == null) continue;
            
            // Check for shared trigger groups that should be prioritized
            bool hasSeedGroup = !string.IsNullOrEmpty(trigger.sharedTriggerGroup) && 
                                trigger.sharedTriggerGroup.Equals("Seed", System.StringComparison.OrdinalIgnoreCase);
            bool hasCompoundXGroup = !string.IsNullOrEmpty(trigger.sharedTriggerGroup) && 
                                     (trigger.sharedTriggerGroup.Equals("CompoundX", System.StringComparison.OrdinalIgnoreCase) ||
                                      trigger.sharedTriggerGroup.Equals("X", System.StringComparison.OrdinalIgnoreCase) ||
                                      trigger.sharedTriggerGroup.Equals("Compound X", System.StringComparison.OrdinalIgnoreCase));
            bool isRuler = objName.Contains("ruler");
            
            Debug.Log($"<color=gray>Checking: {testObject.name} - group: '{trigger.sharedTriggerGroup}', is ruler: {isRuler}</color>");
            
            // Prioritize items with shared trigger groups (Seed, CompoundX, etc.), but exclude rulers
            if ((hasSeedGroup || hasCompoundXGroup) && !isRuler)
            {
                prioritizedHit = hitResult;
                Debug.Log($"<color=yellow>✓ Prioritizing item (has shared group '{trigger.sharedTriggerGroup}', not ruler): {testObject.name}</color>");
                break;
            }
            else if ((hasSeedGroup || hasCompoundXGroup) && isRuler)
            {
                Debug.Log($"<color=orange>⚠ Skipping {testObject.name} - has shared group but is a ruler!</color>");
            }
        }
        
        if (prioritizedHit == null)
        {
            Debug.Log($"<color=gray>No seed found in raycast, using closest object: {validHits[0].collider.gameObject.name}</color>");
        }
        
        // If no seed found, use the closest hit
        RaycastHit hit = prioritizedHit ?? validHits[0];
        GameObject hitObject = hit.collider.gameObject;
        Debug.Log($"<color=cyan>Raycast hit: {hitObject.name} at distance {hit.distance:F2}m</color>");
        
        // Check if object has a Rigidbody (required for physics interaction)
        // Also check parent objects in case the collider is on a child
        Rigidbody rb = hitObject.GetComponent<Rigidbody>();
        GameObject targetObject = hitObject;
        
        Debug.Log($"<color=gray>Checking for Rigidbody on: {hitObject.name}</color>");
        
        int depth = 0;
        if (rb == null)
        {
            Debug.Log($"<color=gray>No Rigidbody on {hitObject.name}, checking parents...</color>");
            // Try to find Rigidbody in parent (check all parents up the hierarchy)
            Transform parent = hitObject.transform.parent;
            while (parent != null && rb == null && depth < 10) // Limit to 10 levels to avoid infinite loops
            {
                Debug.Log($"<color=gray>  Checking parent level {depth}: {parent.name}</color>");
                rb = parent.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    targetObject = parent.gameObject;
                    Debug.Log($"<color=green>Found Rigidbody on parent '{parent.name}' (original hit: {hitObject.name})</color>");
                    break;
                }
                parent = parent.parent;
                depth++;
            }
        }
        else
        {
            Debug.Log($"<color=green>Found Rigidbody directly on: {hitObject.name}</color>");
        }
        
        if (rb == null)
        {
            Debug.Log($"<color=yellow>Cannot grab {hitObject.name}: No Rigidbody component found!</color>");
            Debug.Log($"<color=gray>Checked: {hitObject.name} and all parent objects up the hierarchy (checked {depth} levels).</color>");
            return;
        }
        
        // Use the object with the Rigidbody
        GameObject originalHitObject = hitObject; // Save original hit object
        hitObject = targetObject;

        // Block grabbing bowls and pots specifically, but allow their children
        string objectName = hitObject.name.ToLower();
        bool isBowl = objectName.Contains("bowl");
        bool isPot = objectName.Contains("pot") && (objectName.Contains("_01") || objectName.Contains("_02") || 
                                                      objectName.Contains("_03") || objectName.Contains("_04") || 
                                                      objectName.Contains("_05") || objectName.Contains("_06") ||
                                                      objectName.StartsWith("pot_") || objectName.StartsWith("pots_"));
        
        if (isBowl || isPot)
        {
            // Check if the original hit object (child) is different and doesn't contain "bowl" or "pot"
            if (originalHitObject != hitObject)
            {
                string originalName = originalHitObject.name.ToLower();
                bool originalIsBowl = originalName.Contains("bowl");
                bool originalIsPot = originalName.Contains("pot") && (originalName.Contains("_01") || originalName.Contains("_02") || 
                                                                      originalName.Contains("_03") || originalName.Contains("_04") || 
                                                                      originalName.Contains("_05") || originalName.Contains("_06") ||
                                                                      originalName.StartsWith("pot_") || originalName.StartsWith("pots_"));
                
                if (!originalIsBowl && !originalIsPot)
                {
                    // Try to grab the child instead
                    Debug.Log($"<color=cyan>Parent '{hitObject.name}' is a {(isBowl ? "bowl" : "pot")}, trying to grab child '{originalHitObject.name}' instead</color>");
                    
                    // Check if child has Rigidbody
                    Rigidbody childRb = originalHitObject.GetComponent<Rigidbody>();
                    if (childRb != null)
                    {
                        hitObject = originalHitObject;
                        rb = childRb;
                        targetObject = originalHitObject;
                        Debug.Log($"<color=green>Using child object '{originalHitObject.name}' with its own Rigidbody</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=yellow>Cannot grab {hitObject.name}: {(isBowl ? "Bowls" : "Pots")} cannot be moved, and child '{originalHitObject.name}' has no Rigidbody.</color>");
                        return;
                    }
                }
                else
                {
                    Debug.Log($"<color=yellow>Cannot grab {hitObject.name}: {(isBowl ? "Bowls" : "Pots")} cannot be moved.</color>");
                    return;
                }
            }
            else
            {
                Debug.Log($"<color=yellow>Cannot grab {hitObject.name}: {(isBowl ? "Bowls" : "Pots")} cannot be moved.</color>");
                return;
            }
        }
        
        // Note: We can grab kinematic objects too, we just handle them differently
        // (they're already kinematic, so we don't need to change that)
        
        // Grab the object!
        grabbedObject = hitObject;
        grabbedRigidbody = rb;
        
        // Store original kinematic state, constraints, and rotation
        wasKinematic = rb.isKinematic;
        originalConstraints = rb.constraints;
        originalRotation = hitObject.transform.rotation;
        
        // Only make it kinematic if it wasn't already (to allow smooth movement)
        if (!rb.isKinematic)
        {
            rb.isKinematic = true;
        }
        
        // Unlock rotation constraints to allow rotation
        rb.constraints = RigidbodyConstraints.None;
        
        // Calculate offset from camera
        grabOffset = hitObject.transform.position - playerCamera.transform.position;
        
        // Ensure holdDistance is within min/max bounds when first grabbed
        holdDistance = Mathf.Clamp(holdDistance, minHoldDistance, maxHoldDistance);
        
        Debug.Log($"<color=green>GRAB SUCCESS!</color> You grabbed: <color=yellow>{hitObject.name}</color>");
        
        // Check for water can grab (Chapter 1)
        string objectNameLower = hitObject.name.ToLower();
        if (objectNameLower.Contains("water") && objectNameLower.Contains("can"))
        {
            WaterCanGrabRelay waterCanRelay = hitObject.GetComponent<WaterCanGrabRelay>();
            if (waterCanRelay == null)
            {
                waterCanRelay = hitObject.GetComponentInParent<WaterCanGrabRelay>();
            }
            if (waterCanRelay == null)
            {
                waterCanRelay = hitObject.GetComponentInChildren<WaterCanGrabRelay>();
            }
            
            if (waterCanRelay != null)
            {
                waterCanRelay.OnGrabbed();
                Debug.Log($"<color=cyan>PCInteractor: Triggered WaterCanGrabRelay on {hitObject.name}</color>");
            }
        }
        
        // Check for dialog trigger component (for PC mode)
        // Check on the grabbed object first (the one with Rigidbody)
        ItemGrabDialogTrigger dialogTrigger = hitObject.GetComponent<ItemGrabDialogTrigger>();
        
        // If not found, check the original hit object (in case trigger is on child)
        if (dialogTrigger == null && originalHitObject != hitObject)
        {
            dialogTrigger = originalHitObject.GetComponent<ItemGrabDialogTrigger>();
            if (dialogTrigger != null)
            {
                Debug.Log($"<color=cyan>Found ItemGrabDialogTrigger on original hit object: {originalHitObject.name}</color>");
            }
        }
        
        // Also check parent objects
        if (dialogTrigger == null)
        {
            Transform parentTransform = hitObject.transform.parent;
            int parentDepth = 0;
            while (parentTransform != null && dialogTrigger == null && parentDepth < 10)
            {
                dialogTrigger = parentTransform.GetComponent<ItemGrabDialogTrigger>();
                if (dialogTrigger != null)
                {
                    Debug.Log($"<color=cyan>Found ItemGrabDialogTrigger on parent: {parentTransform.name}</color>");
                    break;
                }
                parentTransform = parentTransform.parent;
                parentDepth++;
            }
        }
        
        // Also check children of the grabbed object
        if (dialogTrigger == null)
        {
            dialogTrigger = hitObject.GetComponentInChildren<ItemGrabDialogTrigger>();
            if (dialogTrigger != null)
            {
                Debug.Log($"<color=cyan>Found ItemGrabDialogTrigger on child: {dialogTrigger.gameObject.name}</color>");
            }
        }
        
        if (dialogTrigger != null)
        {
            dialogTrigger.OnPCGrabbed();
            Debug.Log($"<color=green>Triggered dialog from ItemGrabDialogTrigger on {hitObject.name}</color>");
        }
        
        // Visual feedback
        StartCoroutine(FlashObject(hitObject));
    }
    
    private void ReleaseGrab()
    {
        if (grabbedObject == null) return;
        
        Debug.Log($"<color=cyan>Released: {grabbedObject.name}</color>");
        
        // Restore original kinematic state and constraints
        if (grabbedRigidbody != null)
        {
            grabbedRigidbody.isKinematic = wasKinematic;
            grabbedRigidbody.constraints = originalConstraints;
            
            // Only add forward velocity if not releasing for snap (prevents flying away)
            if (!wasKinematic && !isReleasingForSnap)
            {
                Vector3 releaseVelocity = playerCamera.transform.forward * 2f;
                grabbedRigidbody.linearVelocity = releaseVelocity;
            }
            else if (isReleasingForSnap)
            {
                // When releasing for snap, ensure velocity is zero
                grabbedRigidbody.linearVelocity = Vector3.zero;
                grabbedRigidbody.angularVelocity = Vector3.zero;
            }
        }
        
        isReleasingForSnap = false; // Reset flag
        grabbedObject = null;
        grabbedRigidbody = null;
    }
    
    /// <summary>
    /// Public method to check if PCInteractor is currently grabbing a specific object
    /// </summary>
    public bool IsGrabbingObject(GameObject obj)
    {
        return grabbedObject == obj;
    }
    
    /// <summary>
    /// Public method to force release the currently grabbed object (used by SnapRuler)
    /// </summary>
    public void ForceReleaseGrab()
    {
        if (grabbedObject != null)
        {
            isReleasingForSnap = true; // Set flag to prevent velocity
            ReleaseGrab();
        }
    }
    
    private void FixedUpdate()
    {
        // Move grabbed object to follow camera
        if (grabbedObject != null && playerCamera != null)
        {
            // Calculate target position (camera position + forward direction * hold distance)
            Vector3 targetPosition = playerCamera.transform.position + playerCamera.transform.forward * holdDistance;
            
            // Add vertical offset to position object slightly lower for better depth perception
            targetPosition += playerCamera.transform.up * holdVerticalOffset;
            
            // Smoothly move the object to target position
            Vector3 currentPosition = grabbedObject.transform.position;
            Vector3 newPosition = Vector3.Lerp(currentPosition, targetPosition, grabMoveSpeed * Time.fixedDeltaTime);
            
            grabbedObject.transform.position = newPosition;
        }
    }
    
    private System.Collections.IEnumerator FlashObject(GameObject obj)
    {
        // Simple visual feedback - change color briefly
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color originalColor = renderer.material.color;
            renderer.material.color = Color.green;
            yield return new WaitForSeconds(0.2f);
            renderer.material.color = originalColor;
        }
    }
}
