using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCam : MonoBehaviour
{
    public float sensX;
    public float sensY;
    
    [Header("Input")]
    [Tooltip("The Input Action Asset containing the PC Player action map")]
    public InputActionAsset inputActionAsset;
    
    [Tooltip("Name of the Look action in the PC Player action map")]
    public string lookActionName = "Look";
    
    private InputAction lookAction;
    
    public Transform orientation;
    float xRotation;
    float yRotation;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Find and enable the look action
        if (inputActionAsset != null)
        {
            InputActionMap pcPlayerMap = inputActionAsset.FindActionMap("PC Player");
            if (pcPlayerMap != null)
            {
                lookAction = pcPlayerMap.FindAction(lookActionName);
                if (lookAction != null)
                {
                    lookAction.Enable();
                }
                else
                {
                    Debug.LogError($"PlayerCam: Action '{lookActionName}' not found in PC Player action map!");
                }
            }
            else
            {
                Debug.LogError("PlayerCam: 'PC Player' action map not found in Input Action Asset!");
            }
        }
        else
        {
            Debug.LogWarning("PlayerCam: Input Action Asset is not assigned!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // get mouse input from Input System
        Vector2 lookInput = Vector2.zero;
        if (lookAction != null && lookAction.enabled)
        {
            lookInput = lookAction.ReadValue<Vector2>();
        }
        
        float mouseX = lookInput.x * Time.deltaTime * sensX * 0.15f;
        float mouseY = lookInput.y * Time.deltaTime * sensY * 0.15f;

        yRotation += mouseX;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        //rotate cam and orientation
        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }
}
