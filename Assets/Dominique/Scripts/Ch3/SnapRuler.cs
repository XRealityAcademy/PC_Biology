using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

[DisallowMultipleComponent]
public class SnapRuler : MonoBehaviour
{
    [Header("Scene References")]
    public Manager_Ch3 managerCh3;
    public Transform rulerObj;  // leave null to use self

    [Header("Snapping Areas (order 0..5)")]
    public Collider[] snappingAreas = new Collider[6];
    public Transform[] snapTargets = new Transform[6];

    [Header("UI checks (1:1)")]
    public GameObject[] peaHeightUI = new GameObject[6];
    
    [Tooltip("Parent GameObject that contains all peaHeightUI elements. Will be activated on first snap.")]
    public GameObject peaHeightUIParent;

    [Header("Snapping Behavior")]
    [Range(0f, 1f)] public float snapStrength = 1f;
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffsetEuler = Vector3.zero;

    public enum LockMode { None, FreezeRotation, FreezeAll }
    public LockMode lockMode = LockMode.FreezeRotation;

    [Header("Unsnap (no XR Manager)")]
    public bool enableMotionUnsnap = true;
    public float unsnapDistance = 0.01f;
    public float unsnapLinearSpeed = 0.08f;
    public float unsnapAngularSpeed = 0.6f;
    public bool unsnapOnGrabPress = true;
    public bool allowEditorKeyUnsnap = true;

    [Header("Anti-Jitter Settings")]
    [Tooltip("Detach from controller/parent while snapped so only this script drives pose.")]
    public bool detachFromParentOnSnap = true;

    [Tooltip("Drive pose AFTER controllers in LateUpdate to avoid tug-of-war.")]
    public bool drivePoseInLateUpdate = true;

    [Tooltip("Smooth time (seconds) for position while snapped. Lower = snappier.")]
    public float snapPosSmoothTime = 0.035f;

    [Tooltip("Per-frame rotation blend while snapped (0..1).")]
    [Range(0.01f, 1f)] public float snapRotLerp = 0.2f;

    [Tooltip("Directly pin pose when within this distance (meters).")]
    public float hardLockPosEpsilon = 0.0015f;

    [Tooltip("Directly pin pose when within this angle (degrees).")]
    public float hardLockRotEpsilonDeg = 0.8f;

    [Header("Audio (optional)")]
    public AudioSource sfxSource;
    public AudioClip snapClip;

    // ---- internals ----
    private bool[] _snappedFlags = new bool[6];
    private bool _dialog17Triggered = false;

    private Rigidbody _rb;
    private Quaternion _rotOffset;
    private bool _isSnapped = false;
    private int _currentSnappedIndex = -1;

    private Vector3 _targetPos;
    private Quaternion _targetRot;

    // smoothing
    private Vector3 _velSmooth; // for SmoothDamp
    private Vector3 _snapWorldPos;
    private Quaternion _snapWorldRot;

    // parenting restore
    private Transform _originalParent;

    // RB settings restore
    private RigidbodyInterpolation _rbInterpBefore;
    private CollisionDetectionMode _rbCollisionBefore;
    
    // PC Interactor reference (to notify when snapping)
    private PCInteractor _pcInteractor;

    void Awake()
    {
        if (!rulerObj) rulerObj = transform;
        _rb = rulerObj.GetComponent<Rigidbody>();
        _rotOffset = Quaternion.Euler(rotationOffsetEuler);
        
        // Find PCInteractor if in PC mode
        _pcInteractor = FindFirstObjectByType<PCInteractor>();

        // Deactivate parent if assigned
        if (peaHeightUIParent != null)
        {
            peaHeightUIParent.SetActive(false);
            Debug.Log($"<color=cyan>SnapRuler: Deactivated peaHeightUIParent '{peaHeightUIParent.name}'</color>");
        }

        // Deactivate individual UI elements
        if (peaHeightUI != null)
            foreach (var ui in peaHeightUI)
                if (ui) ui.SetActive(false);
    }

    void Update()
    {
        if (allowEditorKeyUnsnap && Input.GetKeyDown(KeyCode.U))
            ForceUnsnap();

        if (_isSnapped)
        {
            // Check for unsnap input (VR or PC)
            bool shouldUnsnap = false;
            if (unsnapOnGrabPress)
            {
                if (IsVRMode())
                {
                    // VR mode: check VR controllers
                    shouldUnsnap = IsAnyGripPressed() || IsAnyTriggerPressed();
                }
                else
                {
                    // PC mode: check mouse/keyboard
                    shouldUnsnap = Input.GetMouseButton(0) || Input.GetKey(KeyCode.E);
                }
            }
            
            if (shouldUnsnap)
            {
                ForceUnsnap();
                return;
            }

            if (enableMotionUnsnap)
            {
                if (Vector3.Distance(rulerObj.position, _snapWorldPos) > unsnapDistance)
                {
                    ForceUnsnap(); return;
                }
                if (_rb)
                {
                    // FIXED: linearVelocity -> velocity
                    if (_rb.linearVelocity.sqrMagnitude > unsnapLinearSpeed * unsnapLinearSpeed) { ForceUnsnap(); return; }
                    if (_rb.angularVelocity.magnitude > unsnapAngularSpeed) { ForceUnsnap(); return; }
                }
            }
        }
    }

    void LateUpdate()
    {
        if (!_isSnapped || !drivePoseInLateUpdate) return;

        // Soft-drive toward target pose AFTER controller updates to avoid jitter.
        // Recompute target each frame in case snap target moves slightly.
        var t = GetCurrentSnapTargetTransform(_currentSnappedIndex);
        if (t != null)
        {
            _targetPos = t.TransformPoint(positionOffset);
            _targetRot = t.rotation * _rotOffset;
        }

        // Position smoothing
        rulerObj.position = Vector3.SmoothDamp(rulerObj.position, _targetPos, ref _velSmooth, snapPosSmoothTime);

        // Rotation smoothing
        rulerObj.rotation = Quaternion.Slerp(rulerObj.rotation, _targetRot, snapRotLerp);

        // Hard lock if very close to kill micro-wobble
        if ((rulerObj.position - _targetPos).sqrMagnitude <= hardLockPosEpsilon * hardLockPosEpsilon &&
            Quaternion.Angle(rulerObj.rotation, _targetRot) <= hardLockRotEpsilonDeg)
        {
            rulerObj.SetPositionAndRotation(_targetPos, _targetRot);
        }

        // zero RB drift while snapped - do this AFTER setting position to prevent physics fighting
        if (_rb)
        {
            // FIXED: linearVelocity -> velocity
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        int idx = IndexOfArea(other);
        if (idx < 0)
        {
            Debug.Log($"<color=gray>SnapRuler: Collider '{other.name}' entered but is not in snappingAreas array</color>");
            return;
        }

        Debug.Log($"<color=cyan>SnapRuler: OnTriggerEnter - Collider '{other.name}' matched snappingAreas[{idx}]</color>");

        if (!_isSnapped || _currentSnappedIndex == idx)
        {
            SnapToIndex(idx);
            MarkSnapped(idx);
        }
        else
        {
            Debug.Log($"<color=yellow>SnapRuler: Already snapped to index {_currentSnappedIndex}, ignoring trigger for index {idx}</color>");
        }
    }

    void OnTriggerStay(Collider other)
    {
        int idx = IndexOfArea(other);
        if (idx < 0) return;

        if (!_snappedFlags[idx] && (!_isSnapped || _currentSnappedIndex == idx))
        {
            SnapToIndex(idx);
            MarkSnapped(idx);
        }
    }

    void OnTriggerExit(Collider other)
    {
        int idx = IndexOfArea(other);
        if (idx < 0) return;

        if (_isSnapped && _currentSnappedIndex == idx)
        {
            Debug.Log($"<color=yellow>SnapRuler: OnTriggerExit - Ruler left snapping area {idx}, unsnapping...</color>");
            
            // Small delay before unsnapping to prevent immediate re-entry
            // This helps prevent the ruler from flying away due to physics interactions
            StartCoroutine(DelayedUnsnap());
        }
    }
    
    IEnumerator DelayedUnsnap()
    {
        // Wait a frame to let physics settle
        yield return null;
        
        // Check if ruler is still in a snapping area before unsnapping
        // This prevents unsnapping if the ruler immediately re-enters the trigger
        bool stillInSnappingArea = false;
        Collider[] overlappingColliders = Physics.OverlapSphere(rulerObj.position, 0.1f);
        foreach (var col in overlappingColliders)
        {
            if (IndexOfArea(col) >= 0)
            {
                stillInSnappingArea = true;
                Debug.Log($"<color=yellow>SnapRuler: Ruler still overlapping with snapping area '{col.name}', canceling unsnap</color>");
                break;
            }
        }
        
        if (!stillInSnappingArea)
        {
            ForceUnsnap();
        }
    }

    int IndexOfArea(Collider col)
    {
        if (snappingAreas == null)
        {
            Debug.LogWarning("<color=red>SnapRuler: snappingAreas array is NULL!</color>");
            return -1;
        }
        
        for (int i = 0; i < snappingAreas.Length; i++)
        {
            if (snappingAreas[i] == col)
            {
                Debug.Log($"<color=green>SnapRuler: Found matching collider at index {i}: '{col.name}'</color>");
                return i;
            }
        }
        
        Debug.Log($"<color=gray>SnapRuler: Collider '{col.name}' not found in snappingAreas array</color>");
        return -1;
    }

    Transform GetCurrentSnapTargetTransform(int idx)
    {
        if (idx < 0) return null;
        var explicitT = (snapTargets != null && idx < snapTargets.Length) ? snapTargets[idx] : null;
        if (explicitT) return explicitT;
        if (snappingAreas != null && idx < snappingAreas.Length && snappingAreas[idx])
            return snappingAreas[idx].transform;
        return null;
    }

    void SnapToIndex(int idx)
    {
        if (idx < 0 || idx >= snappingAreas.Length) return;

        var t = GetCurrentSnapTargetTransform(idx);
        if (t == null) return;

        _targetPos = t.TransformPoint(positionOffset);
        _targetRot = t.rotation * _rotOffset;

        // IMPORTANT: If PCInteractor is holding the ruler, release it first to prevent conflicts
        if (_pcInteractor != null && _pcInteractor.IsGrabbingObject(rulerObj.gameObject))
        {
            Debug.Log("<color=yellow>SnapRuler: PCInteractor is holding ruler, releasing it before snap</color>");
            _pcInteractor.ForceReleaseGrab();
        }

        if (_rb)
        {
            // remember old RB settings, then clamp
            _rbInterpBefore = _rb.interpolation;
            _rbCollisionBefore = _rb.collisionDetectionMode;

            _rb.interpolation = RigidbodyInterpolation.None; // we're manually driving in LateUpdate
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // CRITICAL: Zero velocity BEFORE any other changes to prevent flying away
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            ApplyLockMode(lockMode);
            
            // Double-check velocity is zero after applying lock mode
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // Detach from controller to avoid parent pose fighting
        if (detachFromParentOnSnap)
        {
            _originalParent = rulerObj.parent;
            if (_originalParent != null)
            {
                Debug.Log($"<color=cyan>SnapRuler: Detaching ruler from parent '{_originalParent.name}'</color>");
            }
            rulerObj.SetParent(null, true);
        }

        // Initial placement (snappy or blended)
        if (snapStrength >= 1f)
            rulerObj.SetPositionAndRotation(_targetPos, _targetRot);
        else
        {
            rulerObj.position = Vector3.Lerp(rulerObj.position, _targetPos, snapStrength);
            rulerObj.rotation = Quaternion.Slerp(rulerObj.rotation, _targetRot, snapStrength);
        }

        _isSnapped = true;
        _currentSnappedIndex = idx;

        _snapWorldPos = _targetPos;
        _snapWorldRot = _targetRot;

        if (sfxSource && snapClip) sfxSource.PlayOneShot(snapClip);
    }

    void ApplyLockMode(LockMode mode)
    {
        if (!_rb) return;

        switch (mode)
        {
            case LockMode.None:
                _rb.isKinematic = false;
                _rb.constraints = RigidbodyConstraints.None;
                break;

            case LockMode.FreezeRotation:
                _rb.isKinematic = false; // allow hand to pull position
                _rb.constraints = RigidbodyConstraints.FreezeRotation;
                break;

            case LockMode.FreezeAll:
                _rb.isKinematic = true;  // total pin (most stable, but feels sticky)
                _rb.constraints = RigidbodyConstraints.FreezeAll;
                break;
        }
    }

    public void ForceUnsnap()
    {
        if (_rb)
        {
            // IMPORTANT: Zero out all velocity BEFORE changing kinematic state to prevent flying away
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            
            // Restore interpolation and collision detection BEFORE making non-kinematic
            _rb.interpolation = _rbInterpBefore;
            _rb.collisionDetectionMode = _rbCollisionBefore;
            
            // Make non-kinematic AFTER zeroing velocity and restoring settings
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.None;
            
            // Double-check velocity is still zero after state change
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            
            // Add a small damping to prevent any residual physics forces from causing movement
            _rb.linearDamping = 5f;  // Add drag temporarily to slow down any unexpected movement
            _rb.angularDamping = 5f;
            
            // Remove drag after a short delay
            StartCoroutine(RemoveDragAfterDelay());
        }

        // Re-parent back to whoever was holding it (if any)
        // NOTE: Only re-parent if the original parent still exists and is valid
        if (detachFromParentOnSnap && _originalParent != null)
        {
            // Check if parent is still valid (not destroyed)
            if (_originalParent.gameObject != null)
            {
                Debug.Log($"<color=cyan>SnapRuler: Re-parenting ruler back to '{_originalParent.name}'</color>");
                rulerObj.SetParent(_originalParent, true);
            }
            else
            {
                Debug.Log($"<color=yellow>SnapRuler: Original parent was destroyed, keeping ruler unparented</color>");
            }
            _originalParent = null;
        }

        _isSnapped = false;
        _currentSnappedIndex = -1;
        
        Debug.Log($"<color=yellow>SnapRuler: ForceUnsnap called - velocity zeroed, constraints removed, isKinematic: {(_rb ? _rb.isKinematic.ToString() : "N/A")}</color>");
    }
    
    IEnumerator RemoveDragAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        if (_rb != null)
        {
            _rb.linearDamping = 0f;  // Restore original drag (0 from scene file)
            _rb.angularDamping = 0.05f;  // Restore original angular drag
        }
    }

    void MarkSnapped(int idx)
    {
        if (idx < 0 || idx >= _snappedFlags.Length)
        {
            Debug.LogWarning($"SnapRuler: Invalid index {idx} (must be 0-5)");
            return;
        }

        // Only mark as snapped if it hasn't been snapped before
        if (!_snappedFlags[idx])
        {
            _snappedFlags[idx] = true;
            Debug.Log($"<color=green>SnapRuler: Snapping area {idx} has been snapped for the first time</color>");
        }

        // Activate parent UI container on first snap
        // Auto-detect parent if not assigned
        if (peaHeightUIParent == null && peaHeightUI != null && peaHeightUI.Length > 0 && peaHeightUI[0] != null)
        {
            // Try to find the parent by going up the hierarchy
            Transform parent = peaHeightUI[0].transform.parent;
            if (parent != null)
            {
                peaHeightUIParent = parent.gameObject;
                Debug.Log($"<color=yellow>SnapRuler: Auto-detected peaHeightUIParent as '{peaHeightUIParent.name}'</color>");
            }
        }
        
        if (peaHeightUIParent != null && !peaHeightUIParent.activeInHierarchy)
        {
            peaHeightUIParent.SetActive(true);
            Debug.Log($"<color=green>SnapRuler: Activated peaHeightUIParent '{peaHeightUIParent.name}' on first snap</color>");
        }
        else if (peaHeightUIParent == null)
        {
            Debug.LogWarning($"<color=orange>SnapRuler: peaHeightUIParent is not assigned and could not be auto-detected. UI elements may not be visible if their parent is inactive.</color>");
        }
        
        // Light up matching UI (once activated, stays true)
        Debug.Log($"<color=cyan>SnapRuler: Attempting to activate peaHeightUI[{idx}]</color>");
        
        if (peaHeightUI == null)
        {
            Debug.LogError($"<color=red>SnapRuler: peaHeightUI array is NULL!</color>");
            return;
        }
        
        if (idx >= peaHeightUI.Length)
        {
            Debug.LogError($"<color=red>SnapRuler: Index {idx} is out of bounds! peaHeightUI array length is {peaHeightUI.Length}</color>");
            return;
        }
        
        if (peaHeightUI[idx] == null)
        {
            Debug.LogError($"<color=red>SnapRuler: peaHeightUI[{idx}] is NULL! Make sure it's assigned in the Inspector.</color>");
            return;
        }
        
        Debug.Log($"<color=cyan>SnapRuler: peaHeightUI[{idx}] found: '{peaHeightUI[idx].name}', currently active: {peaHeightUI[idx].activeSelf}, activeInHierarchy: {peaHeightUI[idx].activeInHierarchy}</color>");
        
        peaHeightUI[idx].SetActive(true);
        
        // Verify it was activated
        if (peaHeightUI[idx].activeInHierarchy)
        {
            Debug.Log($"<color=green>SnapRuler: Successfully activated peaHeightUI[{idx}] '{peaHeightUI[idx].name}'</color>");
        }
        else
        {
            Debug.LogWarning($"<color=orange>SnapRuler: peaHeightUI[{idx}] '{peaHeightUI[idx].name}' was set active but is not activeInHierarchy. Check if parent is inactive!</color>");
        }

        // If all 6 peaHeightUI are active, trigger dialog 17 (only once)
        if (!_dialog17Triggered && AllPeaHeightUIActive())
        {
            _dialog17Triggered = true;
            Debug.Log("<color=green>SnapRuler: All 6 peaHeightUI are active! Triggering dialog 17.</color>");
            if (managerCh3) 
            {
                managerCh3.PlayDialogByIndex(17);
            }
            else 
            {
                Debug.LogWarning("SnapRuler: managerCh3 not assigned; cannot trigger dialog 17.");
            }
        }
    }

    bool AllPeaHeightUIActive()
    {
        // Check if all 6 peaHeightUI objects (indices 0-5) are active
        if (peaHeightUI == null) return false;
        
        int activeCount = 0;
        for (int i = 0; i < peaHeightUI.Length; i++)
        {
            if (peaHeightUI[i] != null && peaHeightUI[i].activeInHierarchy)
                activeCount++;
        }
        
        // Only log progress if dialog 17 hasn't been triggered yet
        if (!_dialog17Triggered)
        {
            Debug.Log($"SnapRuler: {activeCount}/6 peaHeightUI are active");
        }
        
        // Must have exactly 6 peaHeightUI active to trigger dialog 17
        return activeCount == 6;
    }

    // Check if we're in VR mode
    bool IsVRMode()
    {
        return XRGeneralSettings.Instance != null && 
               XRGeneralSettings.Instance.Manager != null &&
               XRGeneralSettings.Instance.Manager.activeLoader != null;
    }
    
    // XR polling (no XRI manager required)
    bool IsAnyGripPressed()
    {
        return IsButton(CommonUsages.gripButton, XRNode.LeftHand) ||
               IsButton(CommonUsages.gripButton, XRNode.RightHand);
    }
    bool IsAnyTriggerPressed()
    {
        return IsButton(CommonUsages.triggerButton, XRNode.LeftHand) ||
               IsButton(CommonUsages.triggerButton, XRNode.RightHand);
    }
    bool IsButton(InputFeatureUsage<bool> usage, XRNode node)
    {
        var d = InputDevices.GetDeviceAtXRNode(node);
        return d.isValid && d.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }
    
    /// <summary>
    /// Public method to hide all peaHeightUI elements. Called from Manager_Ch3 when showing graphs.
    /// </summary>
    public void HidePeaHeightUI()
    {
        // Hide the parent if it exists
        if (peaHeightUIParent != null)
        {
            peaHeightUIParent.SetActive(false);
            Debug.Log($"<color=cyan>SnapRuler: Hid peaHeightUIParent '{peaHeightUIParent.name}'</color>");
        }
        
        // Hide individual UI elements
        if (peaHeightUI != null)
        {
            int hiddenCount = 0;
            foreach (var ui in peaHeightUI)
            {
                if (ui != null && ui.activeSelf)
                {
                    ui.SetActive(false);
                    hiddenCount++;
                }
            }
            Debug.Log($"<color=cyan>SnapRuler: Hid {hiddenCount} peaHeightUI elements</color>");
        }
    }
    
    /// <summary>
    /// Public method to show all peaHeightUI elements that were previously activated. Called from Manager_Ch3 when showing bar graph.
    /// </summary>
    public void ShowPeaHeightUI()
    {
        // Show the parent if it exists
        if (peaHeightUIParent != null)
        {
            peaHeightUIParent.SetActive(true);
            Debug.Log($"<color=cyan>SnapRuler: Showed peaHeightUIParent '{peaHeightUIParent.name}'</color>");
        }
        
        // Show individual UI elements that were previously snapped (based on _snappedFlags)
        if (peaHeightUI != null)
        {
            int shownCount = 0;
            for (int i = 0; i < peaHeightUI.Length && i < _snappedFlags.Length; i++)
            {
                if (peaHeightUI[i] != null && _snappedFlags[i])
                {
                    // Only show UI elements that were previously snapped
                    peaHeightUI[i].SetActive(true);
                    shownCount++;
                }
            }
            Debug.Log($"<color=cyan>SnapRuler: Showed {shownCount} peaHeightUI elements (previously snapped ones)</color>");
        }
    }
}
