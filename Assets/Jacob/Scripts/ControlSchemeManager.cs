using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.InputSystem;

public class ControlSchemeManager : MonoBehaviour
{
    [Header("Player Rigs")]
    [Tooltip("The root GameObject containing PlayerMovement, PlayerCam, etc.")]
    [SerializeField] private GameObject pcPlayerRig; 
    
    [Tooltip("Your XR Origin setup with XRI components.")]
    [SerializeField] private GameObject xrPlayerRig;
    
    [Header("Input System")]
    [Tooltip("The Input Action Asset containing the PC Player action map")]
    [SerializeField] private InputActionAsset inputActionAsset;
    
    [Tooltip("The name of the PC Player action map (default: 'PC Player')")]
    [SerializeField] private string pcPlayerActionMapName = "PC Player";
    
    private InputActionMap pcPlayerActionMap; 

    void Start()
    {
        // Validate that both rigs are assigned
        if (pcPlayerRig == null)
        {
            Debug.LogError("ControlSchemeManager: PC Player Rig is not assigned in the Inspector!");
            return;
        }
        
        if (xrPlayerRig == null)
        {
            Debug.LogError("ControlSchemeManager: XR Player Rig is not assigned in the Inspector!");
            return;
        }

        // Initialize the PC Player action map if Input Action Asset is assigned
        if (inputActionAsset != null)
        {
            pcPlayerActionMap = inputActionAsset.FindActionMap(pcPlayerActionMapName);
            if (pcPlayerActionMap == null)
            {
                Debug.LogWarning($"ControlSchemeManager: Action map '{pcPlayerActionMapName}' not found in Input Action Asset. Make sure the name matches exactly.");
            }
        }
        else
        {
            Debug.LogWarning("ControlSchemeManager: Input Action Asset is not assigned. Action map management will be skipped.");
        }

        // Check if the XR Subsystem is initialized and an active loader is present
        bool vrActive = XRGeneralSettings.Instance != null && 
                        XRGeneralSettings.Instance.Manager != null &&
                        XRGeneralSettings.Instance.Manager.activeLoader != null;

        if (vrActive)
        {
            SetVRMode();
        }
        else
        {
            SetPCMode();
        }
    }

    private void SetVRMode()
    {
        // Disable the PC Player action map
        if (pcPlayerActionMap != null)
        {
            pcPlayerActionMap.Disable();
            Debug.Log($"ControlSchemeManager: Disabled '{pcPlayerActionMapName}' action map.");
        }
        
        // Disable the PC Rig and enable the XR Rig
        if (pcPlayerRig != null)
        {
            pcPlayerRig.SetActive(false);
        }
        
        if (xrPlayerRig != null)
        {
            xrPlayerRig.SetActive(true);
        }

        Debug.Log("Game launched in VR Mode.");
    }

    private void SetPCMode()
    {
        // Disable the XR Rig first
        if (xrPlayerRig != null)
        {
            xrPlayerRig.SetActive(false);
        }
        
        // Enable the PC Player action map BEFORE activating the rig
        // This ensures actions are ready when scripts start
        if (pcPlayerActionMap != null)
        {
            pcPlayerActionMap.Enable();
            Debug.Log($"ControlSchemeManager: Enabled '{pcPlayerActionMapName}' action map.");
        }
        else
        {
            Debug.LogWarning("ControlSchemeManager: PC Player action map is null. Make sure Input Action Asset is assigned and action map name is correct.");
        }
        
        // Now enable the PC Rig (this will trigger Start() on all components)
        if (pcPlayerRig != null)
        {
            pcPlayerRig.SetActive(true);
        }
        
        Debug.Log("Game launched in PC Mode.");
    }
}