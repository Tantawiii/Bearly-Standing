using Unity.Cinemachine;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Feeds PlayerInputHandler's mouse-look delta into a CinemachineOrbitalFollow's orbit axes.
    /// The raw mouse delta is low-pass filtered so the camera glides instead of snapping, and the
    /// sensitivity is deliberately gentle. GameManager / NetworkBear wire <see cref="input"/>;
    /// Day1SceneBuilder wires <see cref="orbital"/>.
    /// </summary>
    public class CinemachineLookInput : MonoBehaviour
    {
        public PlayerInputHandler input;
        public CinemachineOrbitalFollow orbital;

        [Tooltip("Degrees of orbit per pixel of mouse movement (after smoothing). Kept deliberately low.")]
        [SerializeField] private float horizontalSensitivity = 0.035f;
        [SerializeField] private float verticalSensitivity = 0.024f;
        [Tooltip("Higher = snappier, lower = floatier. ~16 is a clean glide.")]
        [SerializeField] private float smoothing = 16f;
        [Tooltip("Discard mouse-delta spikes larger than this many pixels in one frame (alt-tab / pointer warps).")]
        [SerializeField] private float maxRawDelta = 120f;

        private Vector2 smoothed;

        private void Update()
        {
            if (input == null || orbital == null) return;

            Vector2 raw = Vector2.ClampMagnitude(input.LookDelta, maxRawDelta);
            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            smoothed = Vector2.Lerp(smoothed, raw, k);

            orbital.HorizontalAxis.Value += smoothed.x * horizontalSensitivity;

            float tilt = orbital.VerticalAxis.Value - smoothed.y * verticalSensitivity;
            orbital.VerticalAxis.Value = Mathf.Clamp(tilt, orbital.VerticalAxis.Range.x, orbital.VerticalAxis.Range.y);
        }
    }
}
