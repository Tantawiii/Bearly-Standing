using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Lets a Downed bear be picked up, carried, swung, and thrown by another bear — implements
    /// the same IHoldable contract as Pillow so PlayerCombat's code stays unified. Takes over
    /// physics from PlayerController's CharacterController while Downed/carried/thrown (see
    /// PlayerController.SetCharacterControllerEnabled), using the Rigidbody + CapsuleCollider
    /// that sit disabled on the bear the rest of the time.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(Rigidbody))]
    public class DownedPlayerHandle : MonoBehaviour, IHoldable
    {
        [Header("Impact when used as a weapon (heavier than a pillow)")]
        [SerializeField] private float meleeKnockback = 12f;
        [SerializeField] private float meleeVertical = 3f;
        [SerializeField] private float throwKnockback = 20f;
        [SerializeField] private float throwVertical = 4f;

        [Header("Placeholder pose (no rig yet — see TASKS.md Day 3)")]
        [SerializeField] private Vector3 downedLocalEuler = new Vector3(90f, 0f, 0f);       // "lying down"
        [SerializeField] private Vector3 carriedLocalPosition = new Vector3(0f, 0.2f, 0.6f); // relative to hold socket
        [SerializeField] private Vector3 carriedLocalEuler = new Vector3(0f, 0f, 90f);       // "slung over the shoulder"
        [SerializeField] private float groundedSettleDelay = 0.4f;

        private PlayerHealth health;
        private PlayerController controller;
        private Rigidbody rb;
        private Collider col;
        private PlayerCombat holder;
        private bool isHeld;
        private bool inFlight;
        private float groundedSince = -1f;

        public bool CanBePickedUp => health.State == HealthState.Downed && !isHeld;
        public Transform Transform => transform;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            controller = GetComponent<PlayerController>();
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            health.OnEnteredDowned += HandleEnteredDowned;
        }

        private void OnDestroy()
        {
            if (health != null) health.OnEnteredDowned -= HandleEnteredDowned;
        }

        private void HandleEnteredDowned()
        {
            controller.SetCharacterControllerEnabled(false);
            rb.isKinematic = false;
            rb.useGravity = true;
            col.enabled = true;
            transform.localEulerAngles = downedLocalEuler;
        }

        public void OnPickedUp(PlayerCombat newHolder, Transform holdSocket)
        {
            if (!CanBePickedUp) return;

            isHeld = true;
            holder = newHolder;
            rb.isKinematic = true;
            col.enabled = false;

            transform.SetParent(holdSocket, false);
            transform.localPosition = carriedLocalPosition;
            transform.localEulerAngles = carriedLocalEuler;
        }

        public void OnDropped() => Release(settle: true);

        public HitImpact GetSwingImpact()
        {
            health.RegisterDownedUse();
            return new HitImpact(meleeKnockback, meleeVertical);
        }

        public HitImpact GetThrowImpact() => new HitImpact(throwKnockback, throwVertical);

        public void OnThrown(Vector3 velocity)
        {
            health.RegisterDownedUse();
            Release(settle: false);
            rb.isKinematic = false;
            rb.linearVelocity = velocity;
            inFlight = true;
            groundedSince = -1f;
        }

        private void Release(bool settle)
        {
            isHeld = false;
            if (holder != null) holder.ClearHeld(this);
            holder = null;
            transform.SetParent(null, true);
            col.enabled = true;
            if (settle) transform.localEulerAngles = downedLocalEuler;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!inFlight) return;

            var otherHealth = collision.collider.GetComponentInParent<PlayerHealth>();
            if (otherHealth != null && otherHealth != health)
            {
                Vector3 dir = otherHealth.transform.position - transform.position;
                HitImpact impact = GetThrowImpact();
                otherHealth.ApplyHit(dir, impact.KnockbackForce, impact.VerticalForce);
                inFlight = false;
                SettleOnGround();
                return;
            }

            groundedSince = Time.time;
        }

        private void Update()
        {
            if (!inFlight) return;
            if (groundedSince > 0f && Time.time - groundedSince > groundedSettleDelay)
            {
                inFlight = false;
                SettleOnGround();
            }
        }

        private void SettleOnGround()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.localEulerAngles = downedLocalEuler;
        }
    }
}
