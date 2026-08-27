using UnityEngine;

namespace BearlyStanding
{
    /// <summary>Impact a hit deals: how hard and how high it launches whoever it lands on.</summary>
    public readonly struct HitImpact
    {
        public readonly float KnockbackForce;
        public readonly float VerticalForce;

        public HitImpact(float knockbackForce, float verticalForce)
        {
            KnockbackForce = knockbackForce;
            VerticalForce = verticalForce;
        }
    }

    /// <summary>
    /// Shared contract for anything a bear can pick up, swing, and throw: a pillow (Pillow.cs)
    /// or, once knocked down, another bear (DownedPlayerHandle.cs). Keeps PlayerCombat's
    /// pickup/swing/throw code from branching on "pillow vs. person".
    /// </summary>
    public interface IHoldable
    {
        /// <summary>False while already held, or (for a downed bear) once no longer Downed.</summary>
        bool CanBePickedUp { get; }

        Transform Transform { get; }

        /// <summary>Called by PlayerCombat when a player picks this up.</summary>
        void OnPickedUp(PlayerCombat holder, Transform holdSocket);

        /// <summary>Called if the holder loses this without throwing it (e.g. forced drop).</summary>
        void OnDropped();

        /// <summary>Impact dealt to whoever a melee swing connects with while this is held.</summary>
        HitImpact GetSwingImpact();

        /// <summary>Impact dealt on landing a throw.</summary>
        HitImpact GetThrowImpact();

        /// <summary>Called when the holder throws this with the given launch velocity.</summary>
        void OnThrown(Vector3 velocity);
    }
}
