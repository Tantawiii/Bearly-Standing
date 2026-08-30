using System;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Core CharacterController movement, shared by the human bear (driven by PlayerInputHandler)
    /// and AI bears (driven by TeddyAI) — both just set MoveInput/FacingOverride each frame.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 8f;          // hold Sprint
        [SerializeField] private float acceleration = 25f;
        [SerializeField] private float rotationSpeed = 720f;   // degrees/sec
        [SerializeField] private float knockbackDecay = 6f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 1.6f;
        [SerializeField] private float coyoteTime = 0.12f;     // grace window to still jump just after leaving the ground

        [Header("Gravity")]
        [Tooltip("Referenced/shared, not hardcoded — lets a global effect (Gravity Well pickup) scale it for everyone at once.")]
        [SerializeField] private float baseGravity = -20f;

        /// <summary>Multiplier applied on top of baseGravity for every bear at once — the Gravity Well pickup drives this.</summary>
        public static float GlobalGravityMultiplier = 1f;

        private CharacterController controller;
        private Vector3 currentVelocity;
        private Vector3 externalForce; // knockback, decays over time
        private float verticalVelocity;
        private float lastGroundedTime = -999f;
        private bool grounded;
        private float footstepTimer;

        /// <summary>Movement intent in world space (already camera-relative for the human player), magnitude 0-1.</summary>
        public Vector2 MoveInput { get; set; }

        /// <summary>If set, the bear turns to face this world-space direction (flattened to Y=0) instead of its move direction.</summary>
        public Vector3? FacingOverride { get; set; }

        /// <summary>Soft input gate — false while Downed/Eliminated. See SetCharacterControllerEnabled for the hard gate used when carried/thrown.</summary>
        public bool MovementEnabled { get; set; } = true;

        /// <summary>Set by the input/AI driver each frame — swaps the top speed between walk and run.</summary>
        public bool Sprinting { get; set; }

        /// <summary>Temporary move-speed scale (1 = normal). Speed-boost pickup drives this above 1.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Fired the frame a jump actually launches — PlayerAnimator turns it into the Jump animation.</summary>
        public event Action OnJumped;

        /// <summary>Current horizontal movement velocity (no knockback, no gravity) — read by PlayerAnimator to drive locomotion.</summary>
        public Vector3 PlanarVelocity => new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        /// <summary>Current planar speed in m/s.</summary>
        public float PlanarSpeed => PlanarVelocity.magnitude;

        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public bool IsGrounded => grounded;
        public float VerticalVelocity => verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            grounded = controller.isGrounded;
            if (grounded) lastGroundedTime = Time.time;

            if (grounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // small stick-to-ground value
            verticalVelocity += baseGravity * GlobalGravityMultiplier * dt;

            Vector3 desiredMove = MovementEnabled ? new Vector3(MoveInput.x, 0f, MoveInput.y) : Vector3.zero;
            if (desiredMove.sqrMagnitude > 1f) desiredMove.Normalize();

            float topSpeed = (Sprinting ? runSpeed : walkSpeed) * Mathf.Max(0.1f, SpeedMultiplier);
            Vector3 targetVelocity = desiredMove * topSpeed;
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * dt);
            externalForce = Vector3.Lerp(externalForce, Vector3.zero, knockbackDecay * dt);

            Vector3 motion = currentVelocity + externalForce;
            motion.y = verticalVelocity;
            controller.Move(motion * dt);

            Vector3 faceDir = FacingOverride ?? new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            if (faceDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * dt);
            }

            // Footsteps
            float planarSpeed = PlanarSpeed;
            if (grounded && planarSpeed > 0.5f)
            {
                footstepTimer -= dt * (planarSpeed / Mathf.Max(0.1f, walkSpeed));
                if (footstepTimer <= 0f)
                {
                    footstepTimer = 0.42f;
                    SfxManager.Play(SfxId.Footstep, transform.position);
                }
            }
            else footstepTimer = 0f;
        }

        /// <summary>Launches a jump if grounded (within coyote-time) and movement isn't gated. Returns true if it fired.</summary>
        public bool TryJump()
        {
            if (!MovementEnabled || !enabled) return false;
            if (Time.time - lastGroundedTime > coyoteTime && !grounded) return false;

            float g = baseGravity * GlobalGravityMultiplier;
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * Mathf.Max(0.01f, -g));
            lastGroundedTime = -999f; // consume the coyote window
            OnJumped?.Invoke();
            SfxManager.Play(SfxId.JumpHop, transform.position);
            return true;
        }

        /// <summary>Reset transient movement state for a rematch.</summary>
        public void ResetMovement()
        {
            currentVelocity = Vector3.zero;
            externalForce = Vector3.zero;
            verticalVelocity = 0f;
            SpeedMultiplier = 1f;
            MovementEnabled = true;
            if (!controller.enabled) controller.enabled = true;
            enabled = true;
        }

        /// <summary>Applies an instantaneous knockback impulse (horizontal direction + separate vertical force).</summary>
        public void ApplyKnockback(Vector3 horizontalDirection, float horizontalForce, float verticalForce)
        {
            horizontalDirection.y = 0f;
            if (horizontalDirection.sqrMagnitude > 0.01f) horizontalDirection.Normalize();
            externalForce += horizontalDirection * horizontalForce;
            verticalVelocity = Mathf.Max(verticalVelocity, verticalForce);
        }

        /// <summary>
        /// Hard-disables the CharacterController (and this script's own Update) so a Rigidbody
        /// can take over — used while a bear is carried/thrown per the Downed-state mechanic.
        /// </summary>
        public void SetCharacterControllerEnabled(bool value)
        {
            controller.enabled = value;
            enabled = value;
        }
    }
}
