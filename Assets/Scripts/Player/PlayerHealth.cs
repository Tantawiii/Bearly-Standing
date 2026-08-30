using System;
using UnityEngine;

namespace BearlyStanding
{
    public enum HealthState { Alive, Downed, Eliminated }

    /// <summary>
    /// 3-heart damage model extended with a Downed phase: the 3rd hit knocks a bear down
    /// instead of eliminating them outright — another bear can then carry/swing/throw them
    /// (DownedPlayerHandle) until FinalizeElimination() actually removes them from the match.
    /// A Downed bear still counts as "in contention" for MatchManager's win check.
    ///
    /// Knockback scales up by hit number and the downing hit adds hit-stop + a heavy shake.
    /// Pickups feed <see cref="DamageMultiplier"/> / <see cref="KnockbackMultiplier"/> (Tanky armor).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Hearts")]
        [SerializeField] private int maxHearts = 3;

        [Header("Knockback tiers (multiplier by hit number; last entry reused past the end)")]
        [SerializeField] private float[] knockbackByHit = { 1f, 1.7f, 3f };

        [Header("Downed state")]
        [Tooltip("Kill-switch: if false, the 3rd hit eliminates immediately (original GDD rule) instead of entering Downed.")]
        [SerializeField] private bool downedStateEnabled = true;
        [SerializeField] private int maxDownedUses = 2;      // tunable — times used as a weapon before final elimination
        [SerializeField] private float downedDuration = 6f;  // tunable — seconds before final elimination regardless

        public event Action<int> OnHeartsChanged;
        public event Action OnEnteredDowned;
        public event Action OnEliminated;
        /// <summary>(hitNumber 1..N, wasDowningHit) — for hit-reaction animation and feedback.</summary>
        public event Action<int, bool> OnHit;

        public HealthState State { get; private set; } = HealthState.Alive;
        public int CurrentHearts { get; private set; }
        public int DownedUseCount { get; private set; }
        public int MaxHearts => maxHearts;
        public bool IsDowned => State == HealthState.Downed;
        public bool IsEliminated => State == HealthState.Eliminated;

        /// <summary>Incoming-damage scale (1 = normal). Tanky armor pickup drives this below 1.</summary>
        public float DamageMultiplier { get; set; } = 1f;
        /// <summary>Incoming-knockback scale (1 = normal). Tanky armor pickup drives this below 1.</summary>
        public float KnockbackMultiplier { get; set; } = 1f;

        /// <summary>Networking kill-switch (TASKS.md Day 2): online we revert to instant elimination on the 3rd hit.</summary>
        public void SetDownedStateEnabled(bool value) => downedStateEnabled = value;

        /// <summary>Server-authoritative setter used by NetworkBear to mirror the downed lifecycle onto clients.</summary>
        public void ForceState(HealthState newState, int hearts, int downedUses)
        {
            CurrentHearts = Mathf.Clamp(hearts, 0, maxHearts);
            DownedUseCount = downedUses;
            if (newState == State) { OnHeartsChanged?.Invoke(CurrentHearts); return; }

            State = newState;
            switch (newState)
            {
                case HealthState.Downed:
                    controller.MovementEnabled = false;
                    if (combat != null) combat.enabled = false;
                    OnEnteredDowned?.Invoke();
                    break;
                case HealthState.Eliminated:
                    controller.MovementEnabled = false;
                    OnEliminated?.Invoke();
                    break;
            }
            OnHeartsChanged?.Invoke(CurrentHearts);
        }

        private PlayerController controller;
        private PlayerCombat combat;
        private float downedTimer;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            combat = GetComponent<PlayerCombat>();
            CurrentHearts = maxHearts;
        }

        private void Update()
        {
            if (State != HealthState.Downed) return;
            downedTimer += Time.deltaTime;
            if (downedTimer >= downedDuration) FinalizeElimination();
        }

        /// <param name="fromDirection">World-space direction the hit travelled from — the bear launches away from it.</param>
        public void ApplyHit(Vector3 fromDirection, float knockbackForce, float verticalForce)
        {
            if (State != HealthState.Alive) return;

            // Tanky armor: DamageMultiplier < 1 gives a chance to shrug the hit off (no heart lost).
            bool costsHeart = UnityEngine.Random.value <= DamageMultiplier;
            if (costsHeart) CurrentHearts = Mathf.Max(0, CurrentHearts - 1);
            OnHeartsChanged?.Invoke(CurrentHearts);

            int hitsTaken = Mathf.Max(1, maxHearts - CurrentHearts); // 1 for the first heart lost, 2 for the second …
            bool downingHit = costsHeart && CurrentHearts <= 0;
            int tier = Mathf.Clamp(hitsTaken, 1, 3);

            float tierMul = knockbackByHit != null && knockbackByHit.Length > 0
                ? knockbackByHit[Mathf.Min(hitsTaken - 1, knockbackByHit.Length - 1)]
                : 1f;
            float kbScale = tierMul * Mathf.Max(0f, KnockbackMultiplier);
            controller.ApplyKnockback(fromDirection, knockbackForce * kbScale, verticalForce * kbScale);

            Vector3 fxPos = transform.position + Vector3.up;
            ScreenShake.Bump(fxPos, downingHit ? 1.0f : 0.15f + 0.18f * tier);
            HitFeedback.Play(fxPos, downingHit ? 3 : tier);
            SfxManager.Play(downingHit ? SfxId.HeavyImpact : SfxId.PillowHit, fxPos);
            if (downingHit)
            {
                HitStop.Freeze(0.08f);
                SfxManager.Play(SfxId.Knockout, fxPos);
            }
            OnHit?.Invoke(hitsTaken, downingHit);

            if (CurrentHearts <= 0)
            {
                if (downedStateEnabled) EnterDowned();
                else FinalizeElimination();
            }
        }

        private void EnterDowned()
        {
            State = HealthState.Downed;
            downedTimer = 0f;
            DownedUseCount = 0;
            controller.MovementEnabled = false;
            if (combat != null) combat.enabled = false;
            OnEnteredDowned?.Invoke();
        }

        /// <summary>Called by DownedPlayerHandle each time this downed bear is swung or thrown as a weapon.</summary>
        public void RegisterDownedUse()
        {
            if (State != HealthState.Downed) return;
            DownedUseCount++;
            if (DownedUseCount >= maxDownedUses) FinalizeElimination();
        }

        private void FinalizeElimination()
        {
            if (State == HealthState.Eliminated) return;
            State = HealthState.Eliminated;
            controller.MovementEnabled = false;
            OnEliminated?.Invoke();
            MatchManager.Instance?.NotifyEliminated(this);
        }

        /// <summary>Full reset for a rematch without respawning the GameObject.</summary>
        public void ResetForRematch()
        {
            State = HealthState.Alive;
            CurrentHearts = maxHearts;
            DownedUseCount = 0;
            downedTimer = 0f;
            DamageMultiplier = 1f;
            KnockbackMultiplier = 1f;
            controller.MovementEnabled = true;
            if (combat != null) combat.enabled = true;
            OnHeartsChanged?.Invoke(CurrentHearts);
        }
    }
}
