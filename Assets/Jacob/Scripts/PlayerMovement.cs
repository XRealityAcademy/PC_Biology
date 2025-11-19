using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    public float groundDrag;

    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask whatIsGround;
    bool isGrounded;

    [Header("Input")]
    [Tooltip("The Input Action Asset containing the PC Player action map")]
    public InputActionAsset inputActionAsset;
    
    [Tooltip("Name of the Move action in the PC Player action map")]
    public string moveActionName = "Move";
    
    private InputAction moveAction;

    public Transform orientation;

    float horizontalInput;
    float verticalInput;

    Vector3 moveDirection;

    Rigidbody rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        
        // Find and enable the move action
        if (inputActionAsset != null)
        {
            InputActionMap pcPlayerMap = inputActionAsset.FindActionMap("PC Player");
            if (pcPlayerMap != null)
            {
                moveAction = pcPlayerMap.FindAction(moveActionName);
                if (moveAction != null)
                {
                    moveAction.Enable();
                }
                else
                {
                    Debug.LogError($"PlayerMovement: Action '{moveActionName}' not found in PC Player action map!");
                }
            }
            else
            {
                Debug.LogError("PlayerMovement: 'PC Player' action map not found in Input Action Asset!");
            }
        }
        else
        {
            Debug.LogWarning("PlayerMovement: Input Action Asset is not assigned!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        PlayerInput();
        SpeedControl();

        if (isGrounded)
            rb.linearDamping = groundDrag;
        else
            rb.linearDamping = 0;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void PlayerInput()
    {
        if (moveAction != null && moveAction.enabled)
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            horizontalInput = moveInput.x;
            verticalInput = moveInput.y;
        }
        else
        {
            horizontalInput = 0f;
            verticalInput = 0f;
        }
    }

    private void MovePlayer()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
        rb.AddForce(moveDirection.normalized * moveSpeed * 15f, ForceMode.Force);
    }

    private void SpeedControl()
    {
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVel.magnitude > moveSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * moveSpeed;
            rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
        }
    }
}
