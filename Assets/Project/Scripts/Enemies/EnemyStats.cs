using UnityEngine;

// =============================================================================
// EnemyStats  (Scripts/Enemies)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   Gives an enemy its WEIGHT (Small / Medium / Heavy). The enemy keeps its own
//   base stats (SteamTank max steam, attack damage, push distances on its
//   SteamKnockback); the weight multiplies them using WeightTierSettings:
//     - max steam (health)           - how far hits push it (full / empty tank)
//     - damage of its attacks        - how long hits stun it
//   Other scripts can read Weight and Tier (the skills will use pullSlots).
//
// PUT IT ON   Every enemy (GearWheel adds it by itself when you add GearWheel).
//             Pick the weight in the dropdown. For a new kind of enemy, set its
//             BASE stats as if it were Small, then choose its weight.
// SLOT        Tier Settings (optional): a shared WeightTierSettings asset.
//             Empty = the built-in values.
// =============================================================================
[RequireComponent(typeof(SteamTank))]
[RequireComponent(typeof(SteamKnockback))]
public class EnemyStats : MonoBehaviour
{
    [SerializeField] private EnemyWeight weight = EnemyWeight.Small;

    [Tooltip("Optional shared table that says what each weight means. " +
             "Empty = built-in values.")]
    [SerializeField] private WeightTierSettings tierSettings;

    private SteamTank tank;
    private SteamKnockback knockback;
    private float baseMaxSteam;

    public EnemyWeight Weight => weight;

    /// <summary>The multipliers of this enemy's weight.</summary>
    public WeightTier Tier { get; private set; }

    /// <summary>Attacks multiply their damage by this.</summary>
    public float DamageMultiplier => Tier != null ? Tier.damageMultiplier : 1f;

    private void Awake()
    {
        tank = GetComponent<SteamTank>();
        knockback = GetComponent<SteamKnockback>();

        // The tank size set on the enemy itself is its BASE (as a Small).
        baseMaxSteam = tank.Max;

        ApplyWeight();
    }

    /// <summary>Applies the weight's multipliers to the tank and the push.</summary>
    public void ApplyWeight()
    {
        WeightTierSettings table =
            tierSettings != null ? tierSettings : WeightTierSettings.Default;

        Tier = table.Get(weight);

        tank.SetMaxSteam(baseMaxSteam * Tier.steamMultiplier, true);

        knockback.SetWeightMultipliers(
            Tier.pushWhenFull,
            Tier.pushWhenEmpty,
            Tier.stunMultiplier
        );
    }

    /// <summary>Changes the weight while the game runs.</summary>
    public void SetWeight(EnemyWeight newWeight)
    {
        weight = newWeight;
        ApplyWeight();
    }
}
