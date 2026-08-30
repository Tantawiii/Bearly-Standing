using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BearlyStanding
{
    /// <summary>
    /// Wires the New Input System ("Player" action map of InputSystem_Actions) to a
    /// PlayerController + PlayerCombat. Only added to the human-controlled bear — AI bears
    /// drive the same PlayerController/PlayerCombat directly instead (see TeddyAI).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCombat))]
    public class PlayerInputHandler : MonoBehaviour
    {
        public InputActionAsset inputActions;
        /// <summary>Assigned at runtime by GameManager once the camera exists — used for camera-relative movement.</summary>
        public Transform cameraTransform;

        public event Action OnPauseRequested;

        private PlayerController playerController;
        private PlayerCombat playerCombat;

        private InputAction moveAction, lookAction, attackAction, throwAction, interactAction, dodgeAction, sprintAction, pauseAction;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            playerCombat = GetComponent<PlayerCombat>();

            var map = inputActions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = map.FindAction("Move", true);
            lookAction = map.FindAction("Look", true);
            attackAction = map.FindAction("Attack", true);
            throwAction = map.FindAction("Throw", true);
            interactAction = map.FindAction("Interact", true);
            dodgeAction = map.FindAction("Dodge", true);      // Space — repurposed as Jump
            sprintAction = map.FindAction("Sprint", true);    // Left Shift — hold to run
            pauseAction = map.FindAction("Pause", true);
        }

        private void OnEnable()
        {
            moveAction.Enable(); lookAction.Enable(); attackAction.Enable();
            throwAction.Enable(); interactAction.Enable(); dodgeAction.Enable();
            sprintAction.Enable(); pauseAction.Enable();

            // Controls: LMB (Attack) throws straight away; RMB (Throw) is now hold-to-aim —
            // the trajectory arc shows while held and the pillow leaves on release.
            // Player melee is retired (bots still swing via TeddyAI → PlayerCombat.TryMeleeAttack).
            attackAction.performed += OnThrow;
            throwAction.performed += OnAimStart;
            throwAction.canceled += OnAimRelease;
            interactAction.performed += OnInteract;
            dodgeAction.performed += OnJump;
            pauseAction.performed += OnPause;
        }

        private void OnDisable()
        {
            attackAction.performed -= OnThrow;
            throwAction.performed -= OnAimStart;
            throwAction.canceled -= OnAimRelease;
            interactAction.performed -= OnInteract;
            dodgeAction.performed -= OnJump;
            pauseAction.performed -= OnPause;

            if (playerCombat != null) playerCombat.Aiming = false;

            moveAction.Disable(); lookAction.Disable(); attackAction.Disable();
            throwAction.Disable(); interactAction.Disable(); dodgeAction.Disable();
            sprintAction.Disable(); pauseAction.Disable();
        }

        private void Update()
        {
            Vector2 rawMove = moveAction.ReadValue<Vector2>();

            Vector3 camForward = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRight = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : Vector3.right;
            Vector3 worldMove = camForward * rawMove.y + camRight * rawMove.x;

            playerController.MoveInput = new Vector2(worldMove.x, worldMove.z);
            playerController.FacingOverride = worldMove.sqrMagnitude > 0.01f ? worldMove : (Vector3?)null;
            playerController.Sprinting = CanAct && sprintAction.IsPressed();

            // Drop the aim if the bear can no longer act (downed / eliminated) while holding RMB.
            if (!CanAct && playerCombat.Aiming) playerCombat.Aiming = false;
        }

        public Vector2 LookDelta => lookAction.ReadValue<Vector2>();

        /// <summary>False once the bear is Downed or Eliminated — swallow combat input from then on.</summary>
        private bool CanAct => playerController.MovementEnabled;

        private void OnThrow(InputAction.CallbackContext ctx) { if (CanAct) playerCombat.TryThrow(); }

        private void OnAimStart(InputAction.CallbackContext ctx) { if (CanAct) playerCombat.Aiming = true; }

        private void OnAimRelease(InputAction.CallbackContext ctx)
        {
            if (!playerCombat.Aiming) return;
            playerCombat.Aiming = false;
            if (CanAct) playerCombat.TryThrow();
        }

        private void OnInteract(InputAction.CallbackContext ctx) { if (CanAct) playerCombat.TryInteract(); }
        private void OnJump(InputAction.CallbackContext ctx) { if (CanAct) playerController.TryJump(); }
        private void OnPause(InputAction.CallbackContext ctx) => OnPauseRequested?.Invoke();
    }
}
