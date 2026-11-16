using UnityEngine;

/// <summary>How heavy an enemy is. Heavy is for bosses and elite enemies.</summary>
public enum EnemyWeight
{
    Small,
    Medium,
    Heavy
}

// One row of the weight table: what a tier changes compared to the enemy's
// own base stats. 1 = no change.
[System.Serializable]
public class WeightTier
{
    [Tooltip("Multiplies the enemy's base max steam (its health).")]
    public float steamMultiplier = 1f;

    [Tooltip("Multiplies the damage of the enemy's attacks.")]
    public float damageMultiplier = 1f;

    [Tooltip("Multiplies how far a hit pushes it when its tank is FULL.")]
    public float pushWhenFull = 1f;

    [Tooltip("Multiplies how far a hit pushes it when its tank is EMPTY. Keep this " +
             "low for heavy enemies so an empty tank does not mean an instant ring-out.")]
    public float pushWhenEmpty = 1f;

    [Tooltip("Multiplies how long it stays stunned after a hit.")]
    public float stunMultiplier = 1f;

    [Tooltip("For the pull skill (later): how many pull slots it takes up. " +
             "0 = cannot be pulled, the fangs bounce off.")]
    public int pullSlots = 1;

    public WeightTier() { }

    public WeightTier(
        float steam, float damage, float full, float empty, float stun, int slots)
    {
        steamMultiplier = steam;
        damageMultiplier = damage;
        pushWhenFull = full;
        pushWhenEmpty = empty;
        stunMultiplier = stun;
        pullSlots = slots;
    }
}

// =============================================================================
// WeightTierSettings  (Scripts/Enemies)
// -----------------------------------------------------------------------------
// WHAT IT DOES
//   ONE shared table that says what Small, Medium and Heavy mean. Each enemy
//   has its own BASE stats (its steam tank, its attack damage, its push
//   distances). Its weight only MULTIPLIES them. Change a tier here and every
//   enemy of that weight changes.
//
//   Small   = the baseline (x1). The GearWheel is Small.
//   Medium  = more steam, hits harder, harder to push.
//   Heavy   = much more steam, hits much harder, hard to push. Even with an
//             EMPTY tank it flies far less than a Small one, so it is never an
//             instant ring-out (unless it stands right at the edge).
//
// HOW TO USE
//   Optional. Without this asset every enemy uses the built-in values below.
//   To edit them: Project window -> right-click -> Create -> Tusk -> Weight
//   Tiers, then drag the asset into the 'Tier Settings' slot of each EnemyStats
//   (or of the enemy prefab).
// =============================================================================
[CreateAssetMenu(fileName = "WeightTiers", menuName = "Tusk/Weight Tiers")]
public class WeightTierSettings : ScriptableObject
{
    //                                       steam damage full  empty stun  slots
    [SerializeField] private WeightTier small  = new WeightTier(1f, 1.0f, 1.00f, 1.00f, 1.00f, 1);
    [SerializeField] private WeightTier medium = new WeightTier(3f, 1.5f, 0.60f, 0.50f, 0.75f, 2);
    [SerializeField] private WeightTier heavy  = new WeightTier(8f, 2.5f, 0.35f, 0.25f, 0.50f, 0);

    private static WeightTierSettings builtIn;

    /// <summary>The built-in values, used when no asset is assigned.</summary>
    public static WeightTierSettings Default
    {
        get
        {
            if (builtIn == null)
            {
                builtIn = CreateInstance<WeightTierSettings>();
                builtIn.hideFlags = HideFlags.HideAndDontSave;
            }

            return builtIn;
        }
    }

    public WeightTier Get(EnemyWeight weight)
    {
        switch (weight)
        {
            case EnemyWeight.Medium: return medium;
            case EnemyWeight.Heavy: return heavy;
            default: return small;
        }
    }
}
