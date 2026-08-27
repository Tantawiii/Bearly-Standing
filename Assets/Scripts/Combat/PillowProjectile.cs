using UnityEngine;

namespace BearlyStanding
{
    /// <summary>Drives a Pillow's physics-projectile behaviour while it's Thrown.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PillowProjectile : MonoBehaviour
    {
        [SerializeField] private float groundedSettleDelay = 0.4f;

        private Pillow pillow;
        private GameObject thrower;
        private bool inFlight;
        private float groundedSince = -1f;

        public void Launch(Pillow owningPillow, GameObject thrownBy)
        {
            pillow = owningPillow;
            thrower = thrownBy;
            inFlight = true;
            groundedSince = -1f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!inFlight) return;
            if (thrower != null && collision.transform.IsChildOf(thrower.transform)) return;

            var health = collision.collider.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                Vector3 dir = health.transform.position - transform.position;
                HitImpact impact = pillow.GetThrowImpact();
                health.ApplyHit(dir, impact.KnockbackForce, impact.VerticalForce);
                inFlight = false;
                pillow.BecomeAvailable();
                return;
            }

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
