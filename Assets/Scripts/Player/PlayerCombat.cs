using System;
using System.Collections;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Holding/swinging/throwing logic, shared by pillows and downed bears via IHoldable.
    /// Driven either by PlayerInputHandler (human) or TeddyAI (bots) calling the Try* methods.
    ///
    /// In networked play NetworkBear sets <see cref="alwaysArmed"/> (the bear is permanently
    /// holding its pillow — no pickup/downed-carry online) and routes throws through
    /// <see cref="networkThrow"/>; melee still uses the local Hitbox, which relays the hit.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Sockets (wired by Day1SceneBuilder)")]
        public Transform holdSocket;
        public Hitbox meleeHitbox;

        /// <summary>If set (the human bear points this at the camera), throws launch along this transform's flattened forward instead of the bear's facing.</summary>
        public Transform aimSource;

        [Header("Networked mode (set by NetworkBear)")]
        [Tooltip("Online: the bear permanently holds its pillow — pickup/downed-carry are offline-only.")]
        public bool alwaysArmed;
        /// <summary>Online: TryThrow delegates here with the flattened aim direction instead of spawning a local pillow.</summary>
        [NonSerialized] public Action<Vector3> networkThrow;

        [Header("Armed-melee impact (used when alwaysArmed, i.e. online)")]
        [SerializeField] private float armedMeleeKnockback = 8f;
        [SerializeField] private float armedMeleeVertical = 2f;

        /// <summary>Raised the frame a melee swing actually starts — PlayerAnimator turns it into a swing animation.</summary>
        public event Action OnSwing;

        /// <summary>Raised the frame a throw is committed — PlayerAnimator turns it into a throw animation.</summary>
        public event Action OnThrow;

        [Header("Interact / Melee")]
        [SerializeField] private float interactRange = 2f;
        [SerializeField] private LayerMask holdableMask = ~0;
        [SerializeField] private float meleeActiveWindow = 0.15f;
        [SerializeField] private float meleeCooldown = 0.4f;

        [Header("Throw")]
        [SerializeField] private float throwCooldown = 0.5f;
        [SerializeField] private float throwSpeed = 15f;

        [Header("Giant-pillow pickup")]
        [SerializeField] private float giantKnockbackMultiplier = 2.2f;
        [SerializeField] private float giantSwingSlow = 1.8f; // swing cooldown + active-window multiplier while giant

        private IHoldable held;
        private float nextMeleeTime;
        private float nextThrowTime;
        private bool swingInProgress;

        /// <summary>Temporary attack-rate scale (>1 = faster). Speed-boost pickup drives this.</summary>
        public float AttackSpeedMultiplier { get; set; } = 1f;
        /// <summary>Swings left with the giant/heavy pillow (0 = normal pillow). Set by the Giant Pillow pickup.</summary>
        public int GiantSwingsRemaining { get; set; }

        public bool IsHolding => held != null || alwaysArmed;

        /// <summary>Set by PlayerInputHandler while the human holds the aim button — ThrowTrajectory draws the arc while true.</summary>
        public bool Aiming { get; set; }

        /// <summary>True when a throw would actually launch something (a held pillow/bear, or online's permanent pillow).</summary>
        public bool HasThrowReady => held != null || alwaysArmed;

        /// <summary>Where a thrown object leaves from — the hold socket (falls back to chest height).</summary>
        public Vector3 ThrowOrigin => holdSocket != null ? holdSocket.position : transform.position + Vector3.up * 1.2f;

        /// <summary>Launch velocity a throw uses right now — shared by TryThrow and the trajectory preview so they never drift.</summary>
        public Vector3 ThrowLaunchVelocity => AimDirection() * throwSpeed + Vector3.up * (throwSpeed * 0.15f);

        /// <summary>Finds the nearest pickup-able IHoldable (pillow or downed bear) in range and picks it up.</summary>
        public void TryInteract()
        {
            if (alwaysArmed || held != null) return; // online you never pick up; offline swing/throw to free your hands first

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
            if (!IsHolding || swingInProgress || Time.time < nextMeleeTime) return;

            float rate = Mathf.Max(0.2f, AttackSpeedMultiplier);
            bool giant = GiantSwingsRemaining > 0;
            float cooldown = meleeCooldown / rate * (giant ? giantSwingSlow : 1f);
            nextMeleeTime = Time.time + cooldown;
            if (giant) GiantSwingsRemaining--;
            StartCoroutine(SwingRoutine(giant));
        }

        private IEnumerator SwingRoutine(bool giant)
        {
            swingInProgress = true;
            OnSwing?.Invoke();
            SfxManager.Play(SfxId.PillowSwing, transform.position);

            HitImpact impact = held != null ? held.GetSwingImpact() : new HitImpact(armedMeleeKnockback, armedMeleeVertical);
            if (giant) impact = new HitImpact(impact.KnockbackForce * giantKnockbackMultiplier, impact.VerticalForce * giantKnockbackMultiplier);
            if (meleeHitbox != null) meleeHitbox.Activate(impact, gameObject);

            float window = meleeActiveWindow * (giant ? giantSwingSlow : 1f);
            yield return new WaitForSeconds(window);
            if (meleeHitbox != null) meleeHitbox.Deactivate();
            swingInProgress = false;
        }

        public void TryThrow()
        {
            if (Time.time < nextThrowTime) return;
            float rate = Mathf.Max(0.2f, AttackSpeedMultiplier);

            if (alwaysArmed)
            {
                nextThrowTime = Time.time + throwCooldown / rate;
                Aiming = false;
                OnThrow?.Invoke();
                SfxManager.Play(SfxId.PillowThrow, transform.position);
                networkThrow?.Invoke(AimDirection());
                return;
            }

            if (held == null) return;
            nextThrowTime = Time.time + throwCooldown / rate;
            Aiming = false;

            Vector3 velocity = ThrowLaunchVelocity;
            var thrown = held;
            held = null;
            OnThrow?.Invoke();
            SfxManager.Play(SfxId.PillowThrow, transform.position);
            thrown.OnThrown(velocity);
        }

        private Vector3 AimDirection()
        {
            Vector3 forward = transform.forward;
            if (aimSource != null)
            {
                Vector3 flat = Vector3.ProjectOnPlane(aimSource.forward, Vector3.up);
                if (flat.sqrMagnitude > 0.001f) forward = flat.normalized;
            }
            return forward;
        }

        /// <summary>Called by the held object itself when it stops being held for any reason.</summary>
        public void ClearHeld(IHoldable holdable)
        {
            if (held == holdable) held = null;
        }
    }
}
