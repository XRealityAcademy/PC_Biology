using UnityEngine;

/// <summary>
/// Controls subtle up and down floating movement for fairy characters.
/// This script applies a smooth sine wave motion to the Y position,
/// allowing independent control from the animation system.
/// </summary>
public class FairyFloat : MonoBehaviour
{
    [Header("Floating Settings")]
    [Tooltip("How high the fairy moves up and down (amplitude)")]
    [SerializeField] private float floatAmplitude = 0.02f;
    
    [Tooltip("How fast the fairy floats up and down")]
    [SerializeField] private float floatSpeed = 0.5f;
    
    [Tooltip("Optional offset for the starting position")]
    [SerializeField] private float floatOffset = 0f;
    
    [Header("Options")]
    [Tooltip("If enabled, uses local position instead of world position")]
    [SerializeField] private bool useLocalPosition = true;

    private Vector3 startPosition;
    private float timer = 0f;

    void Start()
    {
        // Store the initial position
        if (useLocalPosition)
        {
            startPosition = transform.localPosition;
        }
        else
        {
            startPosition = transform.position;
        }
        
        // Add random offset to timer to prevent all fairies floating in sync
        timer = Random.Range(0f, Mathf.PI * 2f);
    }

    void LateUpdate()
    {
        // Increment timer based on speed
        timer += Time.deltaTime * floatSpeed;
        
        // Calculate the vertical offset using sine wave for smooth motion
        float yOffset = Mathf.Sin(timer) * floatAmplitude + floatOffset;
        
        // Apply the movement
        if (useLocalPosition)
        {
            Vector3 newPosition = startPosition;
            newPosition.y += yOffset;
            transform.localPosition = newPosition;
        }
        else
        {
            Vector3 newPosition = startPosition;
            newPosition.y += yOffset;
            transform.position = newPosition;
        }
    }

    /// <summary>
    /// Reset the starting position (useful if the fairy is moved elsewhere)
    /// </summary>
    public void ResetStartPosition()
    {
        if (useLocalPosition)
        {
            startPosition = transform.localPosition;
        }
        else
        {
            startPosition = transform.position;
        }
    }
}

