using System.Collections.Generic;
using UnityEngine;
using System;

[DisallowMultipleComponent]
public class CompoundXPot : MonoBehaviour
{
    [Header("Counting Rule")]
    [Tooltip("How many unique CompoundX objects must be inside to satisfy this pot.")]
    public int requiredCount = 0; // Pot1: 0, Pot2: 1, Pot3: 3, Pot4: 5, Pot5: 7, Pot6: 9

    [Tooltip("Tag used by Compound X objects.")]
    public string compoundXTag = "CompoundX";

    [Header("Status (read-only)")]
    public bool isX = false; // true when currentCount >= requiredCount

    private readonly HashSet<GameObject> _contents = new HashSet<GameObject>();

    // Event to notify Manager_Ch3 when state changes
    public event Action OnStatusChanged;

    void Start()
    {
        UpdateStatus();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(compoundXTag)) return;
        if (_contents.Add(other.gameObject))
            UpdateStatus();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(compoundXTag)) return;
        if (_contents.Remove(other.gameObject))
            UpdateStatus();
    }

    private void UpdateStatus()
    {
        bool newState = _contents.Count >= requiredCount;
        if (newState != isX)
        {
            isX = newState;
            OnStatusChanged?.Invoke();
        }
    }

    public void ForceSatisfied()
    {
        if (!isX)
        {
            isX = true;
            OnStatusChanged?.Invoke();
        }
    }
}
