namespace BearlyStanding
{
    /// <summary>The four power-ups. Gravity Well is global (all bears); the rest are per-bear.</summary>
    public enum PickupEffectType
    {
        TankyArmor,   // temporary damage + knockback reduction
        SpeedBoost,   // temporary move-speed + attack-speed multiplier
        GiantPillow,  // next N swings are heavy: bigger knockback, slower
        GravityWell   // global: reduced gravity for everyone for a duration
    }
}
