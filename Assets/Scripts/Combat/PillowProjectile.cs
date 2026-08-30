using UnityEngine;

namespace BearlyStanding
{
    /// <summary>Drives a Pillow's physics-projectile behaviour while it's Thrown.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PillowProjectile : MonoBehaviour
    {
        [SerializeField] private float groundedSettleDelay = 0.4f;
        [Tooltip("Radius of the swept overlap that decides 'the pillow touched a bear' — a bit bigger than the pillow so fast throws still connect.")]
        [SerializeField] private float hitSweepRadius = 0.3f;
        [Tooltip("Ignore the thrower for this long after launch so the pillow doesn't 'hit' the bear it's leaving.")]
        [SerializeField] private float throwerGrace = 0.12f;

        private Rigidbody rb;
        private Pillow pillow;
        private GameObject thrower;
        private bool inFlight;
        private float groundedSince = -1f;
        private float launchedAt = -1f;
        private Vector3 lastPos;

        private void Awake() => rb = GetComponent<Rigidbody>();

        public void Launch(Pillow owningPillow, GameObject thrownBy)
        {
            pillow = owningPillow;
            thrower = thrownBy;
            inFlight = true;
            groundedSince = -1f;
            launchedAt = Time.time;
            lastPos = rb.position;
        }

        // Swept contact test — this is the real "the pillow hit the bear" check. Runs every physics
        // step along the segment the pillow just travelled, so a fast throw with a thin collider
        // still registers on the bear's body instead of tunnelling past it.
        private void FixedUpdate()
        {
            if (!inFlight) { lastPos = rb.position; return; }

            Vector3 now = rb.position;
            Vector3 delta = now - lastPos;
            float dist = delta.magnitude;

            if (dist > 0.0001f)
            {
                var sweep = Physics.SphereCastAll(lastPos, hitSweepRadius, delta.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
                foreach (var s in sweep)
                    if (TryHitBear(s.collider)) { lastPos = now; return; }
            }

            // Also catch a bear the pillow is already overlapping (point-blank throw / slow lob).
            var overlaps = Physics.OverlapSphere(now, hitSweepRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in overlaps)
                if (TryHitBear(c)) { lastPos = now; return; }

            lastPos = now;
        }

        private bool TryHitBear(Collider other)
        {
            if (!inFlight || other == null) return false;
            if (IsThrower(other.transform)) return false;

            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return false;

            Vector3 dir = health.transform.position - transform.position;
            HitImpact impact = pillow.GetThrowImpact();
            health.ApplyHit(dir, impact.KnockbackForce, impact.VerticalForce);
            inFlight = false;
            pillow.BecomeAvailable();
            return true;
        }

        private bool IsThrower(Transform t)
        {
            if (thrower == null) return false;
            if (Time.time - launchedAt > throwerGrace) return false;
            return t.IsChildOf(thrower.transform);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!inFlight) return;
            if (IsThrower(collision.transform)) return;

            if (TryHitBear(collision.collider)) return;

            // Hit a wall/prop/floor — give it a moment to settle before becoming pickup-able again.
            groundedSince = Time.time;
        }

        private void Update()
        {
            if (!inFlight) return;
            if (groundedSince > 0f && Time.time - groundedSince > groundedSettleDelay)
            {
                inFlight = false;
                pillow.BecomeAvailable();
            }
        }
    }
}
