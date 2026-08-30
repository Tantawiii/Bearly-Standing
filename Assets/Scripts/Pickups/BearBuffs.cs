using System.Collections.Generic;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Applies timed power-up effects to a bear via a single dispatcher switch (no per-effect
    /// strategy classes — overkill for four). Drives the multipliers on PlayerController /
    /// PlayerCombat / PlayerHealth and shows a coloured glow "tell" while any buff is live.
    /// Gravity Well is not handled here — it's global (see <see cref="ArenaEffectManager"/>).
    /// </summary>
    public class BearBuffs : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float defaultDuration = 8f;   // seconds each buff lasts by default
        [SerializeField] private float tankyDamageChance = 0.5f;   // chance a hit still costs a heart
        [SerializeField] private float tankyKnockback = 0.45f;
        [SerializeField] private float speedMove = 1.5f;
        [SerializeField] private float speedAttack = 1.6f;
        [SerializeField] private int giantSwings = 4;

        private PlayerController controller;
        private PlayerCombat combat;
        private PlayerHealth health;

        private readonly Dictionary<PickupEffectType, float> until = new Dictionary<PickupEffectType, float>();
        private static readonly List<PickupEffectType> _scratch = new List<PickupEffectType>();
        private GameObject tell;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<PlayerHealth>();
            BuildTell();
        }

        public void Grant(PickupEffectType type, float duration = 0f)
        {
            float d = duration > 0f ? duration : defaultDuration;
            until[type] = Time.time + d;

            switch (type)
            {
                case PickupEffectType.TankyArmor:
                    if (health != null) { health.DamageMultiplier = tankyDamageChance; health.KnockbackMultiplier = tankyKnockback; }
                    break;
                case PickupEffectType.SpeedBoost:
                    if (controller != null) controller.SpeedMultiplier = speedMove;
                    if (combat != null) combat.AttackSpeedMultiplier = speedAttack;
                    break;
                case PickupEffectType.GiantPillow:
                    if (combat != null) combat.GiantSwingsRemaining = giantSwings;
                    break;
            }
            SfxManager.Play(SfxId.PickupGrab, transform.position);
        }

        private void Update()
        {
            _scratch.Clear();
            foreach (var kv in until) if (Time.time >= kv.Value) _scratch.Add(kv.Key);
            foreach (var t in _scratch) Expire(t);

            if (tell != null) tell.SetActive(until.Count > 0);
        }

        private void Expire(PickupEffectType type)
        {
            until.Remove(type);
            switch (type)
            {
                case PickupEffectType.TankyArmor:
                    if (health != null) { health.DamageMultiplier = 1f; health.KnockbackMultiplier = 1f; }
                    break;
                case PickupEffectType.SpeedBoost:
                    if (controller != null) controller.SpeedMultiplier = 1f;
                    if (combat != null) combat.AttackSpeedMultiplier = 1f;
                    break;
                case PickupEffectType.GiantPillow:
                    if (combat != null) combat.GiantSwingsRemaining = 0;
                    break;
            }
        }

        public void ClearAll()
        {
            _scratch.Clear();
            foreach (var kv in until) _scratch.Add(kv.Key);
            foreach (var t in _scratch) Expire(t);
            until.Clear();
            if (tell != null) tell.SetActive(false);
        }

        private void BuildTell()
        {
            tell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tell.name = "BuffTell";
            var col = tell.GetComponent<Collider>();
            if (col != null) Destroy(col);
            tell.transform.SetParent(transform, false);
            tell.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            tell.transform.localScale = Vector3.one * 0.35f;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "BuffTellMat" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.3f, 0.85f));
            tell.GetComponent<Renderer>().sharedMaterial = mat;
            tell.SetActive(false);
        }
    }
}
