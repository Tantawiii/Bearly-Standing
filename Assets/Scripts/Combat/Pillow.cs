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

        private void Start()
        {
            // Loose pillows are placed on spawn markers that sit at floor level, so with the
            // re-centred collider they start half-buried. Drop them onto whatever's below so
            // they rest cleanly and are pickup-able without physics ejecting them.
            if (State != PillowState.Available || col == null) return;
            col.enabled = false;
            bool grounded = Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down,
                                            out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore);
            col.enabled = true;
            if (grounded)
            {
                float rest = hit.point.y + 0.12f;
                if (transform.position.y < rest)
                    transform.position = new Vector3(transform.position.x, rest, transform.position.z);
            }
        }

        public void OnPickedUp(PlayerCombat newHolder, Transform holdSocket)
        {
            State = PillowState.Held;
            holder = newHolder;
            rb.isKinematic = true;
            // Interpolation on a kinematic body dragged by its parent makes it visibly trail the
            // hand while the bear runs — pin it rigidly to the socket instead.
            rb.interpolation = RigidbodyInterpolation.None;
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
            rb.interpolation = RigidbodyInterpolation.Interpolate;
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
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            col.enabled = true;
        }
    }
}
