using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Simple state-machine AI: Idle → Search for Pillow → Pick Up → Search for Target → Chase →
    /// Attack/Throw → Recover → repeat, with a little randomness (retarget chance, missed throws) so
    /// bots don't all feel identical, plus a boids-style separation nudge so they stop clumping into
    /// each other. Drives the same PlayerController/PlayerCombat/PlayerHealth the human bear uses.
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
        [Range(0f, 1f)] [SerializeField] private float retargetRandomness = 0.1f;   // "10% off-target choice"
        [Range(0f, 1f)] [SerializeField] private float missedThrowChance = 0.4f;    // 40% of throws are skipped/whiffed

        [Header("Separation (anti-clump)")]
        [SerializeField] private float separationRadius = 2.6f;
        [SerializeField] private float separationWeight = 1.3f;

        private PlayerController controller;
        private PlayerCombat combat;
        private PlayerHealth health;

        [SerializeField] private AIState state = AIState.Idle; // visible in Inspector for debugging
        private Pillow targetPillow;
        private PlayerHealth targetPlayer;
        private Vector3 desiredDir;   // world-space move intent this frame, before separation
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
                controller.Sprinting = false;
                return;
            }

            controller.Sprinting = state == AIState.ChaseTarget && targetPlayer != null &&
                                   Vector3.Distance(transform.position, targetPlayer.transform.position) > meleeRange * 2f;

            if (Time.time >= nextDecisionTime)
            {
                switch (state)
                {
                    case AIState.Idle:
                    case AIState.SearchForPillow: TickSearchForPillow(); break;
                    case AIState.MoveToPillow: TickMoveToPillow(); break;
                    case AIState.SearchForTarget: TickSearchForTarget(); break;
                    case AIState.ChaseTarget: TickChaseTarget(); break;
                    case AIState.Recover: TickRecover(); break;
                }
            }

            // Blend the state's move intent with a push away from other bears (never from the target).
            Vector3 move = desiredDir + Separation();
            move = Vector3.ClampMagnitude(move, 1f);
            controller.MoveInput = move.sqrMagnitude > 0.0004f ? new Vector2(move.x, move.z) : Vector2.zero;
        }

        private Vector3 Separation()
        {
            if (MatchManager.Instance == null) return Vector3.zero;
            Vector3 push = Vector3.zero;
            foreach (var p in MatchManager.Instance.ActivePlayers)
            {
                if (p == null || p == health || p == targetPlayer || p.State == HealthState.Eliminated) continue;
                Vector3 away = transform.position - p.transform.position;
                away.y = 0f;
                float d = away.magnitude;
                if (d > 0.01f && d < separationRadius)
                    push += away / d * ((separationRadius - d) / separationRadius);
            }
            return push * separationWeight;
        }

        private void TickSearchForPillow()
        {
            if (combat.IsHolding) { state = AIState.SearchForTarget; return; }

            var nearbyTarget = AITargeting.FindNearestAliveTarget(transform.position, health);
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
                if (Random.value < retargetRandomness) targetPlayer = AITargeting.FindNearestAliveTarget(transform.position, health);
                state = AIState.SearchForTarget;
                return;
            }

            float dist = Vector3.Distance(transform.position, targetPlayer.transform.position);
            controller.FacingOverride = targetPlayer.transform.position - transform.position;

            if (!combat.IsHolding)
            {
                if (dist < meleeRange) combat.TryInteract();
                MoveToward(targetPlayer.transform.position);
                return;
            }

            if (dist <= meleeRange)
            {
                desiredDir = Vector3.zero;
                state = AIState.Attack;
                combat.TryMeleeAttack();
                Decide(AIState.Recover, 0f, 0f);
            }
            else if (dist <= throwRange)
            {
                desiredDir = Vector3.zero;
                state = AIState.ThrowAtTarget;
                if (Random.value >= missedThrowChance) combat.TryThrow();
                Decide(AIState.Recover, 0f, 0f);
            }
            else
            {
                MoveToward(targetPlayer.transform.position);
            }
        }

        private void TickRecover()
        {
            desiredDir = Vector3.zero;
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
            desiredDir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.zero;
        }

        private void Wander()
        {
            desiredDir = Vector3.zero;
            Decide(AIState.SearchForPillow, 0.5f, 1.2f);
        }

        private void Decide(AIState next, float minDelay, float maxDelay)
        {
            state = next;
            nextDecisionTime = Time.time + Random.Range(minDelay, maxDelay);
        }
    }
}
