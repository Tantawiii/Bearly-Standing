namespace BearlyStanding
{
    /// <summary>
    /// Per the GDD's simple state machine. Attack/Throw are performed as actions inside
    /// ChaseTarget rather than kept as separate dispatched states — TeddyAI still stamps
    /// them into `state` transiently for Inspector visibility while deciding what to do.
    /// </summary>
    public enum AIState
    {
        Idle,
        SearchForPillow,
        MoveToPillow,
        SearchForTarget,
        ChaseTarget,
        Attack,
        ThrowAtTarget,
        Recover
    }
}
