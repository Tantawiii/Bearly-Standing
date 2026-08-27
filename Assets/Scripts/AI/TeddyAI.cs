using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Simple state-machine AI per GDD section 22-25: Idle -&gt; Search for Pillow -&gt; Pick Up
    /// -&gt; Search for Target -&gt; Chase -&gt; Attack/Throw -&gt; Recover -&gt; repeat, with a little
    /// randomness (retarget chance, missed throws) so bots don't all feel identical.
    /// Drives the same PlayerController/PlayerCombat/PlayerHealth every human bear uses.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerHealth))]
    public class TeddyAI : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float meleeRange = 1.8f;
        [SerializeField] private float throwRange = 12f;
        [SerializeField] private float pickupRange = 1.2f;
        [SerializeField] private float recoverTime = 0.6f;
        [SerializeField] private float reactionDelayMin = 0.05f;
        [SerializeField] private float reactionDelayMax = 0.35f;
        [Range(0f, 1f)] [SerializeField] private float retargetRandomness = 0.1f; // "10% off-target choice"
        [Range(0f, 1f)] [SerializeField] private float missedThrowChance = 0.15f;

        private PlayerController controller;
        private PlayerCombat combat;
        private PlayerHealth health;

        [SerializeField] private AIState state = AIState.Idle; // visible in Inspector for debugging
        private Pillow targetPillow;
        private PlayerHealth targetPlayer;
        private float stateTimer;
        private float nextDecisionTime;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            if (health.State != HealthState.Alive)
            {
                controller.MoveInput = Vector2.zero;
                return;
            }

            if (Time.time < nextDecisionTime) return;

            switch (state)
            {
                case AIState.Idle:
                case AIState.SearchForPillow:
                    TickSearchForPillow();
                    break;
                case AIState.MoveToPillow:
                    TickMoveToPillow();
                    break;
                case AIState.SearchForTarget:
                    TickSearchForTarget();
                    break;
                case AIState.ChaseTarget:
                    TickChaseTarget();
                    break;
                case AIState.Recover:
                    TickRecover();
                    break;
            }
        }

        private void TickSearchForPillow()
        {
            if (combat.IsHolding) { state = AIState.SearchForTarget; return; }

            var nearbyTarget = AITargeting.FindNearestAliveTarget(transform.position, health);

            // GDD: "if a target is very close without a pillow, attack anyway"
            if (nearbyTarget != null && Vector3.Distance(nearbyTarget.transform.position, transform.position) < meleeRange * 1.2f)
            {
                targetPlayer = nearbyTarget;
                state = AIState.ChaseTarget;
                return;
            }

            targetPillow = AITargeting.FindNearestAvailablePillow(transform.position);
            if (targetPillow == null) { Wander(); return; }
            state = AIState.MoveToPillow;
        }

        private void TickMoveToPillow()
        {
            if (targetPillow == null || !targetPillow.CanBePickedUp)
            {
                state = AIState.SearchForPillow;
                return;
            }

            MoveToward(targetPillow.transform.position);

            if (Vector3.Distance(transform.position, targetPillow.transform.position) <= pickupRange)
            {
                combat.TryInteract();
                Decide(AIState.SearchForTarget, reactionDelayMin, reactionDelayMax);
            }
        }

        private void TickSearchForTarget()
        {
            if (!combat.IsHolding) { state = AIState.SearchForPillow; return; }

            targetPlayer = AITargeting.FindNearestAliveTarget(transform.position, health);
            if (targetPlayer == null) { Wander(); return; }

            state = AIState.ChaseTarget;
        }

        private void TickChaseTarget()
        {
            if (targetPlayer == null || targetPlayer.State == HealthState.Eliminated)
            {
                // small personality randomness: sometimes drift to whichever target is nearest right now anyway
                if (Random.value < retargetRandomness) targetPlayer = AITargeting.FindNearestAliveTarget(transform.position, health);
                state = AIState.SearchForTarget;
                return;
            }

            float dist = Vector3.Distance(transform.position, targetPlayer.transform.position);
            controller.FacingOverride = targetPlayer.transform.position - transform.position;

            if (!combat.IsHolding)
            {
                if (dist < meleeRange) combat.TryInteract(); // no pillow, no time to be picky
                MoveToward(targetPlayer.transform.position);
                return;
            }

            if (dist <= meleeRange)
            {
                controller.MoveInput = Vector2.zero;
                state = AIState.Attack;
                combat.TryMeleeAttack();
                Decide(AIState.Recover, 0f, 0f);
            }
            else if (dist <= throwRange)
            {
                controller.MoveInput = Vector2.zero;
                state = AIState.ThrowAtTarget;
                if (Random.value >= missedThrowChance) combat.TryThrow(); // "occasional missed throws" — simply skip the throw
                Decide(AIState.Recover, 0f, 0f);
            }
            else
            {
                MoveToward(targetPlayer.transform.position);
            }
        }

        private void TickRecover()
        {
            controller.MoveInput = Vector2.zero;
            stateTimer += Time.deltaTime;
            if (stateTimer >= recoverTime)
            {
                stateTimer = 0f;
                state = combat.IsHolding ? AIState.SearchForTarget : AIState.SearchForPillow;
            }
        }

        private void MoveToward(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position;
            dir.y = 0f;
            controller.MoveInput = dir.sqrMagnitude > 0.01f ? new Vector2(dir.normalized.x, dir.normalized.z) : Vector2.zero;
        }

        private void Wander()
        {
            controller.MoveInput = Vector2.zero; // minimal idle — just wait for the next decision tick
            Decide(AIState.SearchForPillow, 0.5f, 1.2f);
        }

        private void Decide(AIState next, float minDelay, float maxDelay)
        {
            state = next;
            nextDecisionTime = Time.time + Random.Range(minDelay, maxDelay);
        }
    }
}
