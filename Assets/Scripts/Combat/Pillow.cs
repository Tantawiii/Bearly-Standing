using UnityEngine;

namespace BearlyStanding
{
    public enum PillowState { Available, Held, Thrown }

    /// <summary>Available -&gt; Held -&gt; Thrown -&gt; (lands) -&gt; Available. See GDD section 10.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Pillow : MonoBehaviour, IHoldable
    {
        [Header("Impact")]
        [SerializeField] private float meleeKnockback = 8f;
        [SerializeField] private float meleeVertical = 2f;
        [SerializeField] private float throwKnockback = 14f;
        [SerializeField] private float throwVertical = 3f;

        private Rigidbody rb;
        private Collider col;
        private PlayerCombat holder;

        public PillowState State { get; private set; } = PillowState.Available;
        public bool CanBePickedUp => State == PillowState.Available;
        public Transform Transform => transform;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        public void OnPickedUp(PlayerCombat newHolder, Transform holdSocket)
        {
            State = PillowState.Held;
            holder = newHolder;
            rb.isKinematic = true;
            col.enabled = false;
            transform.SetParent(holdSocket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void OnDropped() => BecomeAvailable();

        public HitImpact GetSwingImpact() => new HitImpact(meleeKnockback, meleeVertical);
        public HitImpact GetThrowImpact() => new HitImpact(throwKnockback, throwVertical);

        public void OnThrown(Vector3 velocity)
        {
            State = PillowState.Thrown;
            transform.SetParent(null, true);
            rb.isKinematic = false;
            col.enabled = true;
            rb.linearVelocity = velocity;

            var projectile = GetComponent<PillowProjectile>();
            if (projectile != null) projectile.Launch(this, holder != null ? holder.gameObject : null);
            holder = null;
        }

        /// <summary>Called by PillowProjectile once it lands/hits — pillow becomes pickup-able again.</summary>
        public void BecomeAvailable()
        {
            if (holder != null) holder.ClearHeld(this);
            holder = null;
            State = PillowState.Available;
            transform.SetParent(null, true);
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            col.enabled = true;
        }
    }
}
