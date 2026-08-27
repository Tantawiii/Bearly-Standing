using System.Collections;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Holding/swinging/throwing logic, shared by pillows and downed bears via IHoldable.
    /// Driven either by PlayerInputHandler (human) or TeddyAI (bots) calling the Try* methods.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Sockets (wired by Day1SceneBuilder)")]
        public Transform holdSocket;
        public Hitbox meleeHitbox;

        [Header("Interact / Melee")]
        [SerializeField] private float interactRange = 2f;
        [SerializeField] private LayerMask holdableMask = ~0;
        [SerializeField] private float meleeActiveWindow = 0.15f;
        [SerializeField] private float meleeCooldown = 0.4f;

        [Header("Throw")]
        [SerializeField] private float throwCooldown = 0.5f;
        [SerializeField] private float throwSpeed = 15f;

        private IHoldable held;
        private float nextMeleeTime;
        private float nextThrowTime;
        private bool swingInProgress;

        public bool IsHolding => held != null;

        /// <summary>Finds the nearest pickup-able IHoldable (pillow or downed bear) in range and picks it up.</summary>
        public void TryInteract()
        {
            if (held != null) return; // swing/throw to free your hands first

            Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, holdableMask, QueryTriggerInteraction.Collide);
            IHoldable nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var col in hits)
            {
                var holdable = col.GetComponentInParent<IHoldable>();
                if (holdable == null || !holdable.CanBePickedUp) continue;
                float d = (holdable.Transform.position - transform.position).sqrMagnitude;
                if (d < nearestDist) { nearestDist = d; nearest = holdable; }
            }

            if (nearest != null)
            {
                held = nearest;
                held.OnPickedUp(this, holdSocket);
            }
        }

        public void TryMeleeAttack()
        {
            if (held == null || swingInProgress || Time.time < nextMeleeTime) return;
            nextMeleeTime = Time.time + meleeCooldown;
            StartCoroutine(SwingRoutine());
        }

        private IEnumerator SwingRoutine()
        {
            swingInProgress = true;
            HitImpact impact = held.GetSwingImpact();
            if (meleeHitbox != null) meleeHitbox.Activate(impact, gameObject);
            yield return new WaitForSeconds(meleeActiveWindow);
            if (meleeHitbox != null) meleeHitbox.Deactivate();
            swingInProgress = false;
        }

        public void TryThrow()
        {
            if (held == null || Time.time < nextThrowTime) return;
            nextThrowTime = Time.time + throwCooldown;

            Vector3 velocity = transform.forward * throwSpeed + Vector3.up * (throwSpeed * 0.15f);
            var thrown = held;
            held = null;
            thrown.OnThrown(velocity);
        }

        /// <summary>Called by the held object itself when it stops being held for any reason.</summary>
        public void ClearHeld(IHoldable holdable)
        {
            if (held == holdable) held = null;
        }
    }
}
