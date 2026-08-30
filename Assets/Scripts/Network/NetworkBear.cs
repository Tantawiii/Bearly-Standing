using Mirror;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// The networking layer over a bear. One prefab covers both roles:
    ///  • Player bear — owned by a client connection; that client simulates movement/combat locally
    ///    (client-authoritative NetworkTransform), everyone else just watches.
    ///  • Bot bear — spawned by the server and owned by the host connection, so the host simulates it
    ///    exactly like a local player (TeddyAI drives it). Host mode only.
    ///
    /// Hits are relayed through Commands so they're applied on the server and replayed on every client
    /// (PlayerHealth runs everywhere and converges). Online uses the instant-elimination kill-switch —
    /// the Downed carry/throw mechanic stays an offline "Practice" feature.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCombat))]
    public class NetworkBear : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnColorIndexChanged))] public int colorIndex = -1;
        [SyncVar] public bool isBot;

        [Header("Networked throw (hitscan)")]
        [SerializeField] private float throwRange = 14f;
        [SerializeField] private float throwKnockback = 14f;
        [SerializeField] private float throwVertical = 3f;
        [SerializeField] private LayerMask throwMask = ~0;

        private PlayerController controller;
        private CharacterController characterController;
        private CapsuleCollider bodyCollider;
        private PlayerInputHandler input;
        private TeddyAI ai;
        private PlayerCombat combat;
        private PlayerHealth health;
        private PlayerAnimator anim;
        private BearAppearance appearance;

        private bool isDriver;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            characterController = GetComponent<CharacterController>();
            bodyCollider = GetComponent<CapsuleCollider>();
            input = GetComponent<PlayerInputHandler>();
            ai = GetComponent<TeddyAI>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<PlayerHealth>();
            anim = GetComponent<PlayerAnimator>();
            appearance = GetComponent<BearAppearance>();

            // Nothing drives the bear until OnStartClient assigns a role.
            if (input) input.enabled = false;
            if (ai) ai.enabled = false;
            if (controller) controller.enabled = false;
            if (combat) combat.enabled = false;

            health.SetDownedStateEnabled(false);      // online kill-switch: instant elimination on the 3rd hit
            combat.alwaysArmed = true;                // permanently holding the pillow — no pickup online
            combat.networkThrow = RequestThrow;
        }

        public override void OnStartServer()
        {
            RegisterWithMatch();                       // server needs every bear for the win check
        }

        public override void OnStartClient()
        {
            isDriver = isOwned;
            ConfigureRole();
            RegisterWithMatch();
            ApplyColor();
            if (isOwned && !isBot) SetupLocalPlayer(); // the actual local player only (not host-owned bots)
        }

        private void ConfigureRole()
        {
            if (controller) controller.enabled = isDriver;
            // Driver: CharacterController does collision. Remote: it's just NetworkTransform writing the
            // transform, so swap in the plain CapsuleCollider — otherwise nothing here for other players'
            // melee/throw to detect.
            if (characterController) characterController.enabled = isDriver;
            if (bodyCollider)
            {
                bodyCollider.enabled = !isDriver;
                bodyCollider.isTrigger = false;
            }
            if (combat) combat.enabled = isDriver;
            if (input) input.enabled = isDriver && !isBot;
            if (ai) ai.enabled = isDriver && isBot;
            if (anim) anim.DriveParametersLocally = isDriver;
        }

        private void SetupLocalPlayer()
        {
            var mainCam = Camera.main;
            var camTarget = transform.Find("CameraTarget");
            Transform followTarget = camTarget != null ? camTarget : transform;

            var vcam = FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (vcam != null)
            {
                vcam.Follow = followTarget;
                vcam.LookAt = followTarget;
                var look = vcam.GetComponent<CinemachineLookInput>();
                if (look != null) look.input = input;
            }
            if (input != null && mainCam != null) input.cameraTransform = mainCam.transform;
            if (combat != null && mainCam != null) combat.aimSource = mainCam.transform;

            var hud = FindAnyObjectByType<GameHUD>();
            if (hud != null) hud.SetTrackedPlayer(health);
        }

        private void RegisterWithMatch()
        {
            if (MatchManager.Instance != null) MatchManager.Instance.RegisterPlayer(health);
        }

        // ----- colour -----

        private void OnColorIndexChanged(int oldIndex, int newIndex) => ApplyColor();

        private void ApplyColor()
        {
            if (appearance == null || colorIndex < 0) return;
            var mgr = BearlyNetworkManager.Bearly;
            var mat = mgr != null ? mgr.BearMaterial(colorIndex) : null;
            if (mat != null) appearance.SetMaterial(mat);
            else appearance.SetTint(Color.HSVToRGB((colorIndex * 0.16f) % 1f, 0.55f, 0.92f));
        }

        // ----- melee hit relay (Hitbox calls this on the driver machine) -----

        /// <summary>Returns true if the hit was routed over the network (offline callers get false and apply directly).</summary>
        public bool TryRelayHit(PlayerHealth victim, Vector3 dir, float knockback, float vertical)
        {
            var victimId = victim != null ? victim.GetComponent<NetworkIdentity>() : null;
            if (victimId == null) return false;

            if (isServer)
            {
                ServerApplyHit(victimId, dir, knockback, vertical);
                RpcApplyHit(victimId, dir, knockback, vertical);
            }
            else if (isOwned)
            {
                CmdApplyHit(victimId, dir, knockback, vertical);
            }
            return true;
        }

        [Command]
        private void CmdApplyHit(NetworkIdentity victim, Vector3 dir, float knockback, float vertical)
        {
            ServerApplyHit(victim, dir, knockback, vertical);
            RpcApplyHit(victim, dir, knockback, vertical);
        }

        [Server]
        private void ServerApplyHit(NetworkIdentity victim, Vector3 dir, float knockback, float vertical)
        {
            if (victim != null && victim.TryGetComponent(out PlayerHealth h)) h.ApplyHit(dir, knockback, vertical);
        }

        [ClientRpc]
        private void RpcApplyHit(NetworkIdentity victim, Vector3 dir, float knockback, float vertical)
        {
            if (isServer) return; // host already applied server-side
            if (victim != null && victim.TryGetComponent(out PlayerHealth h)) h.ApplyHit(dir, knockback, vertical);
        }

        // ----- networked throw (server hitscan) -----

        public void RequestThrow(Vector3 direction)
        {
            if (isServer) ServerThrow(transform.position, direction);
            else if (isOwned) CmdThrow(transform.position, direction);
        }

        [Command]
        private void CmdThrow(Vector3 origin, Vector3 direction) => ServerThrow(origin, direction);

        [Server]
        private void ServerThrow(Vector3 origin, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();

            Vector3 from = origin + Vector3.up * 1.1f;
            if (Physics.SphereCast(from, 0.35f, direction, out var hit, throwRange, throwMask, QueryTriggerInteraction.Ignore))
            {
                var h = hit.collider.GetComponentInParent<PlayerHealth>();
                var victimId = h != null ? h.GetComponent<NetworkIdentity>() : null;
                if (victimId != null && victimId != netIdentity)
                {
                    ServerApplyHit(victimId, direction, throwKnockback, throwVertical);
                    RpcApplyHit(victimId, direction, throwKnockback, throwVertical);
                }
            }
        }
    }
}
