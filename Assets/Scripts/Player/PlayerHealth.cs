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
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Hearts")]
        [SerializeField] private int maxHearts = 3;

        [Header("Downed state")]
        [Tooltip("Kill-switch: if false, the 3rd hit eliminates immediately (original GDD rule) instead of entering Downed.")]
        [SerializeField] private bool downedStateEnabled = true;
        [SerializeField] private int maxDownedUses = 2;      // tunable — times used as a weapon before final elimination
        [SerializeField] private float downedDuration = 6f;  // tunable — seconds before final elimination regardless

        public event Action<int> OnHeartsChanged;
        public event Action OnEnteredDowned;
        public event Action OnEliminated;

        public HealthState State { get; private set; } = HealthState.Alive;
        public int CurrentHearts { get; private set; }
        public int DownedUseCount { get; private set; }

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

            CurrentHearts = Mathf.Max(0, CurrentHearts - 1);
            OnHeartsChanged?.Invoke(CurrentHearts);
            controller.ApplyKnockback(fromDirection, knockbackForce, verticalForce);

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
    }
}
