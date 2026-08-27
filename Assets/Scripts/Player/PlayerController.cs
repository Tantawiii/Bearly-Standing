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
        [SerializeField] private float acceleration = 25f;
        [SerializeField] private float rotationSpeed = 720f; // degrees/sec
        [SerializeField] private float knockbackDecay = 6f;

        [Header("Gravity")]
        [Tooltip("Referenced/shared, not hardcoded — lets a global effect (Gravity Well pickup) scale it for everyone at once.")]
        [SerializeField] private float baseGravity = -20f;

        /// <summary>Multiplier applied on top of baseGravity for every bear at once — the Gravity Well pickup drives this.</summary>
        public static float GlobalGravityMultiplier = 1f;

        private CharacterController controller;
        private Vector3 currentVelocity;
        private Vector3 externalForce; // knockback, decays over time
        private float verticalVelocity;

        /// <summary>Movement intent in world space (already camera-relative for the human player), magnitude 0-1.</summary>
        public Vector2 MoveInput { get; set; }

        /// <summary>If set, the bear turns to face this world-space direction (flattened to Y=0) instead of its move direction.</summary>
        public Vector3? FacingOverride { get; set; }

        /// <summary>Soft input gate — false while Downed/Eliminated. See SetCharacterControllerEnabled for the hard gate used when carried/thrown.</summary>
        public bool MovementEnabled { get; set; } = true;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // small stick-to-ground value
            verticalVelocity += baseGravity * GlobalGravityMultiplier * dt;

            Vector3 desiredMove = MovementEnabled ? new Vector3(MoveInput.x, 0f, MoveInput.y) : Vector3.zero;
            if (desiredMove.sqrMagnitude > 1f) desiredMove.Normalize();

            Vector3 targetVelocity = desiredMove * walkSpeed;
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
