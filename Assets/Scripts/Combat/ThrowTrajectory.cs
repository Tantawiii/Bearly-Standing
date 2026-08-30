using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BearlyStanding
{
    /// <summary>
    /// Predicted throw-arc preview for the human bear. Added at spawn by <see cref="GameManager"/>
    /// (never on AI or remote bears). Draws while the player holds the aim button — see
    /// <see cref="PlayerCombat.Aiming"/>, driven by <see cref="PlayerInputHandler"/>. Builds its own
    /// LineRenderer + landing marker procedurally so there are no art/prefab dependencies.
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    public class ThrowTrajectory : MonoBehaviour
    {
        [SerializeField] private int steps = 48;
        [SerializeField] private float stepTime = 0.045f;
        [SerializeField] private float maxDistance = 34f;
        [SerializeField] private float lineWidth = 0.09f;
        [SerializeField] private Color arcColor = new Color(1f, 0.86f, 0.25f, 1f);
        [SerializeField] private LayerMask blockers = ~0;

        private PlayerCombat combat;
        private LineRenderer line;
        private Transform marker;
        private Renderer markerRenderer;
        private readonly List<Vector3> points = new List<Vector3>(64);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();

            // Sprites/Default is always present, respects LineRenderer vertex colours, and alpha-blends.
            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = "ThrowArc" };
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, arcColor);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, arcColor);

            var lineGO = new GameObject("ThrowArc");
            lineGO.transform.SetParent(transform, false);
            line = lineGO.AddComponent<LineRenderer>();
            line.sharedMaterial = mat;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth * 0.4f;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.startColor = arcColor;
            line.endColor = new Color(arcColor.r, arcColor.g, arcColor.b, 0.25f);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.enabled = false;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ThrowArcMarker";
            var sphereCol = sphere.GetComponent<Collider>();
            if (sphereCol != null) Destroy(sphereCol);
            markerRenderer = sphere.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = mat;
            markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            marker = sphere.transform;
            marker.SetParent(transform, false);
            marker.localScale = Vector3.one * 0.28f;
            marker.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            bool show = combat != null && combat.Aiming && combat.HasThrowReady;
            if (line.enabled != show) line.enabled = show;
            if (!show)
            {
                if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                return;
            }

            Vector3 origin = combat.ThrowOrigin;
            Vector3 velocity = combat.ThrowLaunchVelocity;
            Vector3 gravity = Physics.gravity;

            points.Clear();
            points.Add(origin);
            Vector3 prev = origin;
            bool landed = false;
            Vector3 landPos = origin;

            for (int i = 1; i <= steps; i++)
            {
                float t = i * stepTime;
                Vector3 next = origin + velocity * t + 0.5f * gravity * (t * t);

                if ((next - origin).magnitude > maxDistance) { points.Add(next); break; }

                if (Physics.Linecast(prev, next, out RaycastHit hit, blockers, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(transform))
                {
                    points.Add(hit.point);
                    landed = true;
                    landPos = hit.point;
                    break;
                }

                points.Add(next);
                prev = next;
            }

            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());

            if (marker.gameObject.activeSelf != landed) marker.gameObject.SetActive(landed);
            if (landed) marker.position = landPos;
        }
    }
}
