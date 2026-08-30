using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Global arena effects — currently just the Gravity Well. Holds a local countdown and drives
    /// <see cref="PlayerController.GlobalGravityMultiplier"/> every frame, so every peer reads the
    /// same floaty gravity. Networked triggering goes through <see cref="Pickup"/>'s server RPC;
    /// offline the pickup calls <see cref="TriggerLocal"/> directly.
    /// </summary>
    public class ArenaEffectManager : MonoBehaviour
    {
        public static ArenaEffectManager Instance { get; private set; }

        [SerializeField] private float gravityWellScale = 0.4f;

        private float wellEndTime = -1f;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PlayerController.GlobalGravityMultiplier = 1f;
        }

        private void Update()
        {
            bool active = Time.time < wellEndTime;
            PlayerController.GlobalGravityMultiplier = active ? gravityWellScale : 1f;
        }

        /// <summary>Start (or extend) the Gravity Well locally. Called on every peer.</summary>
        public void TriggerLocal(float duration)
        {
            wellEndTime = Mathf.Max(wellEndTime, Time.time + duration);
        }

        public void ResetForRematch()
        {
            wellEndTime = -1f;
            PlayerController.GlobalGravityMultiplier = 1f;
        }
    }
}
