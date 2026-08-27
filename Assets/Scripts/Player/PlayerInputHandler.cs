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

        private InputAction moveAction, lookAction, attackAction, throwAction, interactAction, dodgeAction, pauseAction;

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
            dodgeAction = map.FindAction("Dodge", true);
            pauseAction = map.FindAction("Pause", true);
        }

        private void OnEnable()
        {
            moveAction.Enable(); lookAction.Enable(); attackAction.Enable();
            throwAction.Enable(); interactAction.Enable(); dodgeAction.Enable(); pauseAction.Enable();

            attackAction.performed += OnAttack;
            throwAction.performed += OnThrow;
            interactAction.performed += OnInteract;
            pauseAction.performed += OnPause;
        }

        private void OnDisable()
        {
            attackAction.performed -= OnAttack;
            throwAction.performed -= OnThrow;
            interactAction.performed -= OnInteract;
            pauseAction.performed -= OnPause;

            moveAction.Disable(); lookAction.Disable(); attackAction.Disable();
            throwAction.Disable(); interactAction.Disable(); dodgeAction.Disable(); pauseAction.Disable();
        }

        private void Update()
        {
            Vector2 rawMove = moveAction.ReadValue<Vector2>();

            Vector3 camForward = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRight = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : Vector3.right;
            Vector3 worldMove = camForward * rawMove.y + camRight * rawMove.x;

            playerController.MoveInput = new Vector2(worldMove.x, worldMove.z);
            playerController.FacingOverride = worldMove.sqrMagnitude > 0.01f ? worldMove : (Vector3?)null;
        }

        public Vector2 LookDelta => lookAction.ReadValue<Vector2>();

        private void OnAttack(InputAction.CallbackContext ctx) => playerCombat.TryMeleeAttack();
        private void OnThrow(InputAction.CallbackContext ctx) => playerCombat.TryThrow();
        private void OnInteract(InputAction.CallbackContext ctx) => playerCombat.TryInteract();
        private void OnPause(InputAction.CallbackContext ctx) => OnPauseRequested?.Invoke();
    }
}
