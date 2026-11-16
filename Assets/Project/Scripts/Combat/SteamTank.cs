using System;
using UnityEngine;

// =============================================================================
// SteamTank  (Scripts/Combat)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Steam is health, flight fuel and weight, all in one number. This script
//   only HOLDS the number and the rules for changing it:
//     TrySpend(x)   all-or-nothing payment (dash)
//     Drain(x)      take up to x (flying, hits)
//     Gain(x)       add steam (steam balls, backpack, kills)
//     IncreaseMax(x) bigger tank on level up (new space arrives filled)
//   Events: Changed(current, max) and Emptied.
//
// PUT IT ON   Tusk_Player and every enemy.
// TIP         Right-click the component title -> 'Refill Steam' (Play mode).
// =============================================================================
public class SteamTank : MonoBehaviour
{
    [Header("Steam")]
    [SerializeField] private float maxSteam = 100f;
    [SerializeField] private bool startFull = true;
    [Tooltip("Only used when Start Full is off.")]
    [SerializeField] private float startingSteam = 100f;

    public float Current { get; private set; }
    public float Max => maxSteam;

    /// <summary>0 = empty, 1 = full.</summary>
    public float Normalized => maxSteam > 0f ? Current / maxSteam : 0f;

    public bool IsEmpty => Current <= 0f;

    /// <summary>Raised whenever steam changes: (current, max).</summary>
    public event Action<float, float> Changed;

    /// <summary>Raised once when steam drops from above 0 to 0.</summary>
    public event Action Emptied;

    private void Awake()
    {
        Current = startFull
            ? maxSteam
            : Mathf.Clamp(startingSteam, 0f, maxSteam);
    }

    /// <summary>
    /// All-or-nothing payment (for things like a dash).
    /// Returns false and spends nothing if there is not enough steam.
    /// </summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f)
            return true;

        if (Current < amount)
            return false;

        SetCurrent(Current - amount);
        return true;
    }

    /// <summary>
    /// Takes up to this much steam (for things like flying, hits).
    /// Returns how much was actually taken.
    /// </summary>
    public float Drain(float amount)
    {
        if (amount <= 0f)
            return 0f;

        float before = Current;
        SetCurrent(Current - amount);

        return before - Current;
    }

    public void Gain(float amount)
    {
        if (amount <= 0f)
            return;

        SetCurrent(Current + amount);
    }

    /// <summary>
    /// Raises the maximum (for example +5 on level up).
    /// The new capacity also arrives filled.
    /// </summary>
    public void IncreaseMax(float amount)
    {
        if (amount <= 0f)
            return;

        maxSteam += amount;
        SetCurrent(Current + amount);
    }

    /// <summary>
    /// Sets the size of the tank (and the starting steam). Used to give new
    /// enemies a default tank size. With fillToMax the tank also becomes full.
    /// </summary>
    public void SetMaxSteam(float value, bool fillToMax = false)
    {
        maxSteam = Mathf.Max(1f, value);
        startingSteam = maxSteam;

        if (Application.isPlaying)
            SetCurrent(fillToMax ? maxSteam : Current);
    }

    // Right-click the component title in the Inspector to use this.
    [ContextMenu("Refill Steam")]
    public void SetFull()
    {
        SetCurrent(maxSteam);
    }

    private void SetCurrent(float value)
    {
        float before = Current;
        Current = Mathf.Clamp(value, 0f, maxSteam);

        if (Mathf.Approximately(before, Current))
            return;

        Changed?.Invoke(Current, maxSteam);

        if (before > 0f && Current <= 0f)
            Emptied?.Invoke();
    }
}
