using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Translates gameplay state into visual feedback.
    ///
    /// Two modes, picked automatically:
    ///  • Rigged mode — when an <see cref="Animator"/> (+ controller) is present on the bear's
    ///    visual (wired by Day1SceneBuilder once the ted_low FBX + rig is available). Drives the
    ///    Speed / Throw / Swing parameters and freezes the Animator when Downed/Eliminated so the
    ///    placeholder "lie down" transform pose from DownedPlayerHandle is visible.
    ///  • Placeholder mode — no Animator: flashes the capsule on hit and hides it on elimination.
    ///
    /// Either way, a red hit-flash via MaterialPropertyBlock works on both the capsule MeshRenderer
    /// and the FBX SkinnedMeshRenderer. Swap in real animation clips on Day 3 (see TASKS.md).
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        [Header("Placeholder capsule (used when no rigged Animator is present)")]
        public Renderer visualRenderer;

        [Header("Rigged model (optional — wired by Day1SceneBuilder when the FBX is available)")]
        public Animator animator;

        [Header("Networked (optional — wired by NetworkBuilder)")]
        [Tooltip("When present, animation triggers go through NetworkAnimator so they replicate.")]
        public NetworkAnimator networkAnimator;

        /// <summary>
        /// True on the machine that owns this bear's movement (owner client / server-for-bots / offline).
        /// False on remote copies — there NetworkAnimator feeds Speed/Grounded/triggers and this script
        /// only does hit-flash / elimination visuals.
        /// </summary>
        public bool DriveParametersLocally { get; set; } = true;

        [Header("Animator parameter names")]
        [SerializeField] private string speedParam = "Speed";     // 0..1, planar speed / run speed
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private string jumpTrigger = "Jump";
        [SerializeField] private string throwTrigger = "Throw";
        [SerializeField] private string swingTrigger = "Swing";
        [SerializeField] private string hitReactTrigger = "HitReact";

        [Header("Hit flash")]
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField] private float hitFlashDuration = 0.15f;
        [SerializeField] private float speedDamp = 0.12f; // SetFloat damp time for the Speed param

        private PlayerController controller;
        private PlayerHealth health;
        private PlayerCombat combat;

        private readonly List<Renderer> renderers = new List<Renderer>();
        private MaterialPropertyBlock propertyBlock;

        private int speedHash, groundedHash, jumpHash, throwHash, swingHash, hitReactHash;
        private bool hasAnimator;
        private bool flashing;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
            combat = GetComponent<PlayerCombat>();
            propertyBlock = new MaterialPropertyBlock();

            if (animator == null) animator = GetComponentInChildren<Animator>();
            hasAnimator = animator != null && animator.runtimeAnimatorController != null;

            GetComponentsInChildren(true, renderers);
            if (visualRenderer == null && renderers.Count > 0) visualRenderer = renderers[0];

            speedHash = Animator.StringToHash(speedParam);
            groundedHash = Animator.StringToHash(groundedParam);
            jumpHash = Animator.StringToHash(jumpTrigger);
            throwHash = Animator.StringToHash(throwTrigger);
            swingHash = Animator.StringToHash(swingTrigger);
            hitReactHash = Animator.StringToHash(hitReactTrigger);
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnHeartsChanged += HandleHeartsChanged;
                health.OnEnteredDowned += HandleDowned;
                health.OnEliminated += HandleEliminated;
                health.OnHit += HandleHit;
            }
            if (combat != null)
            {
                combat.OnThrow += HandleThrow;
                combat.OnSwing += HandleSwing;
            }
            if (controller != null) controller.OnJumped += HandleJump;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnHeartsChanged -= HandleHeartsChanged;
                health.OnEnteredDowned -= HandleDowned;
                health.OnEliminated -= HandleEliminated;
                health.OnHit -= HandleHit;
            }
            if (combat != null)
            {
                combat.OnThrow -= HandleThrow;
                combat.OnSwing -= HandleSwing;
            }
            if (controller != null) controller.OnJumped -= HandleJump;
        }

        private void HandleHit(int hitNumber, bool downing)
        {
            if (!downing) SetTrigger(hitReactHash);
        }

        private void Update()
        {
            if (!hasAnimator || !animator.enabled || controller == null) return;
            if (!DriveParametersLocally) return; // NetworkAnimator feeds params on remote copies
            float normalized = controller.RunSpeed > 0.01f ? controller.PlanarSpeed / controller.RunSpeed : 0f;
            animator.SetFloat(speedHash, Mathf.Clamp01(normalized), speedDamp, Time.deltaTime);
            animator.SetBool(groundedHash, controller.IsGrounded);
        }

        private void HandleHeartsChanged(int hearts)
        {
            if (health != null && health.State == HealthState.Alive && !flashing)
                StartCoroutine(FlashHit());
        }

        private void SetTrigger(int hash)
        {
            if (!hasAnimator || !animator.enabled || !DriveParametersLocally) return;
            if (networkAnimator != null) networkAnimator.SetTrigger(hash);
            else animator.SetTrigger(hash);
        }

        private void HandleThrow() => SetTrigger(throwHash);
        private void HandleSwing() => SetTrigger(swingHash);
        private void HandleJump() => SetTrigger(jumpHash);

        private void HandleDowned()
        {
            // Let DownedPlayerHandle's transform "lie down" pose read through instead of the standing rig.
            if (animator != null) animator.enabled = false;
        }

        private void HandleEliminated()
        {
            if (animator != null) animator.enabled = false;
            foreach (var r in renderers) if (r != null) r.enabled = false; // simplest placeholder "knocked out"
        }

        private IEnumerator FlashHit()
        {
            flashing = true;
            SetFlash(true);
            yield return new WaitForSeconds(hitFlashDuration);
            SetFlash(false);
            flashing = false;
        }

        private void SetFlash(bool on)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (on)
                {
                    r.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_BaseColor", hitFlashColor);
                    propertyBlock.SetColor("_Color", hitFlashColor);
                    r.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    // Clear our override so the renderer's own (possibly tinted) material shows again.
                    r.SetPropertyBlock(null);
                }
            }
        }
    }
}
