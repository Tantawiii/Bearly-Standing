using Mirror;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// A glowing orb power-up. Consumed on touch: the dispatcher in <see cref="BearBuffs"/> (or the
    /// global <see cref="ArenaEffectManager"/> for Gravity Well) applies the effect for a duration.
    /// A dimming point light shows the despawn timer.
    ///
    /// Network-aware but offline-safe: with no server/client running it just consumes locally; online
    /// it's server-authoritative (`CmdConsume` → `consumed` SyncVar → hook + server destroy).
    /// </summary>
    public class Pickup : NetworkBehaviour
    {
        [SerializeField] private float lifetime = 22f;
        [SerializeField] private float buffDuration = 8f;
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private float bob = 0.15f;

        [SyncVar] private PickupEffectType effect;
        [SyncVar(hook = nameof(OnConsumedChanged))] private bool consumed;
        [SyncVar] private double spawnNetTime;

        private Transform orb;
        private Light orbLight;
        private float baseIntensity;
        private float localSpawnTime;
        private float baseY;

        private static readonly Color[] EffectColors =
        {
            new Color(0.55f, 0.75f, 1f),   // TankyArmor  — pale blue
            new Color(1f, 0.85f, 0.35f),   // SpeedBoost  — gold
            new Color(1f, 0.45f, 0.85f),   // GiantPillow — pink
            new Color(0.6f, 0.4f, 1f),     // GravityWell — violet
        };

        private void Awake()
        {
            baseY = transform.position.y;
            localSpawnTime = Time.time;
            BuildVisual();
            Recolour();
        }

        public override void OnStartServer()
        {
            spawnNetTime = NetworkTime.time;
            Invoke(nameof(ServerExpire), lifetime);
        }

        public override void OnStartClient() => Recolour();

        /// <summary>Set the effect before the object is spawned (server) or activated (offline).</summary>
        public void Configure(PickupEffectType type)
        {
            effect = type;
            Recolour();
        }

        private void Update()
        {
            if (orb != null)
            {
                orb.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
                var p = transform.position;
                p.y = baseY + Mathf.Sin(Time.time * 2f) * bob;
                transform.position = p;
            }

            float remaining01 = Mathf.Clamp01(1f - Elapsed() / lifetime);
            if (orbLight != null) orbLight.intensity = baseIntensity * (0.25f + 0.75f * remaining01);
        }

        private float Elapsed()
        {
            if (NetworkClient.active || NetworkServer.active)
                return (float)(NetworkTime.time - spawnNetTime);
            return Time.time - localSpawnTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (consumed) return;
            var buffs = other.GetComponentInParent<BearBuffs>();
            if (buffs == null) return;

            var health = buffs.GetComponent<PlayerHealth>();
            if (health != null && health.State != HealthState.Alive) return;

            if (!NetworkClient.active && !NetworkServer.active)
            {
                ConsumeLocal(buffs.gameObject);
                return;
            }

            var nb = other.GetComponentInParent<NetworkBear>();
            if (nb != null && nb.isOwned) CmdConsume(nb.netIdentity);
        }

        [Command(requiresAuthority = false)]
        private void CmdConsume(NetworkIdentity consumer)
        {
            if (consumed || consumer == null) return;
            consumed = true;
            ApplyEffect(consumer.gameObject);
            Invoke(nameof(ServerDestroy), 0.2f);
        }

        private void ConsumeLocal(GameObject consumer)
        {
            consumed = true;
            ApplyEffect(consumer);
            OnConsumedChanged(false, true);
            Destroy(gameObject, 0.2f);
        }

        private void ApplyEffect(GameObject consumer)
        {
            if (effect == PickupEffectType.GravityWell)
            {
                if (ArenaEffectManager.Instance != null) ArenaEffectManager.Instance.TriggerLocal(buffDuration);
                if (isServer) RpcGravityWell(buffDuration);
            }
            else
            {
                var buffs = consumer.GetComponent<BearBuffs>();
                if (buffs != null) buffs.Grant(effect, buffDuration);
                if (isServer && consumer.TryGetComponent(out NetworkIdentity id)) RpcGrant(id, effect, buffDuration);
            }
        }

        [ClientRpc]
        private void RpcGrant(NetworkIdentity consumer, PickupEffectType type, float duration)
        {
            if (isServer || consumer == null) return;
            var buffs = consumer.GetComponent<BearBuffs>();
            if (buffs != null) buffs.Grant(type, duration);
        }

        [ClientRpc]
        private void RpcGravityWell(float duration)
        {
            if (isServer) return;
            if (ArenaEffectManager.Instance != null) ArenaEffectManager.Instance.TriggerLocal(duration);
        }

        private void OnConsumedChanged(bool wasConsumed, bool now)
        {
            if (!now) return;
            SfxManager.Play(SfxId.PickupGrab, transform.position);
            if (orb != null) orb.gameObject.SetActive(false);
            if (orbLight != null) orbLight.enabled = false;
        }

        [Server] private void ServerExpire() { if (!consumed) NetworkServer.Destroy(gameObject); }
        [Server] private void ServerDestroy() { NetworkServer.Destroy(gameObject); }

        private void BuildVisual()
        {
            orb = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            orb.name = "Orb";
            var col = orb.GetComponent<Collider>();
            if (col != null) Destroy(col);
            orb.SetParent(transform, false);
            orb.localScale = Vector3.one * 0.5f;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            orb.GetComponent<Renderer>().sharedMaterial = new Material(shader) { name = "OrbMat" };

            var trigger = gameObject.GetComponent<SphereCollider>();
            if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.9f;

            var lightGO = new GameObject("OrbLight");
            lightGO.transform.SetParent(transform, false);
            orbLight = lightGO.AddComponent<Light>();
            orbLight.type = LightType.Point;
            orbLight.range = 6f;
            baseIntensity = 3.5f;
            orbLight.intensity = baseIntensity;

            Recolour();
        }

        private void Recolour()
        {
            if (orb == null) return;
            Color c = EffectColors[(int)effect % EffectColors.Length];
            var r = orb.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                if (r.sharedMaterial.HasProperty("_BaseColor")) r.sharedMaterial.SetColor("_BaseColor", c);
                if (r.sharedMaterial.HasProperty("_Color")) r.sharedMaterial.SetColor("_Color", c);
            }
            if (orbLight != null) orbLight.color = c;
        }
    }
}
