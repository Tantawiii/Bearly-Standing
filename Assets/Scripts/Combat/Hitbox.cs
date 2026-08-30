using System.Collections.Generic;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Short-lived melee hitbox. PlayerCombat activates this for the attack's active window
    /// and deactivates it afterward — hit detection uses this trigger, not the pillow's visual mesh.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        private Collider hitCollider;
        private GameObject wielder;
        private HitImpact impact;
        private readonly HashSet<PlayerHealth> hitThisSwing = new HashSet<PlayerHealth>();

        private void Awake()
        {
            hitCollider = GetComponent<Collider>();
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;
        }

        public void Activate(HitImpact hitImpact, GameObject owner)
        {
            impact = hitImpact;
            wielder = owner;
            hitThisSwing.Clear();
            hitCollider.enabled = true;
        }

        public void Deactivate()
        {
            hitCollider.enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (wielder != null && other.transform.IsChildOf(wielder.transform)) return;

            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return;
            if (!hitThisSwing.Add(health)) return;

            Vector3 dir = health.transform.position - (wielder != null ? wielder.transform.position : transform.position);

            // Networked: route the hit through the attacker's NetworkBear so it's applied
            // server-authoritatively and replayed on every client. Offline: apply directly.
            var attacker = wielder != null ? wielder.GetComponent<NetworkBear>() : null;
            if (attacker != null && attacker.TryRelayHit(health, dir, impact.KnockbackForce, impact.VerticalForce))
                return;

            health.ApplyHit(dir, impact.KnockbackForce, impact.VerticalForce);
        }
    }
}
