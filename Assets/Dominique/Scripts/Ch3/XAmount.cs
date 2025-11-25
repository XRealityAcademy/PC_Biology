using UnityEngine;

/// <summary>
/// Component that stores the amount of Compound X this object represents.
/// Attach this to X/Compound X GameObjects to specify how much they contain.
/// </summary>
public class XAmount : MonoBehaviour
{
    [Tooltip("The amount of Compound X this object represents (e.g., 2 grams, 4 grams, etc.)")]
    public float amount = 1f;
}

