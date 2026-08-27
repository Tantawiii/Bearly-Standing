using Unity.Cinemachine;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>Feeds PlayerInputHandler's mouse-look delta into a CinemachinePanTilt's Pan/Tilt axes each frame.</summary>
    public class CinemachineLookInput : MonoBehaviour
    {
        /// <summary>Assigned at runtime by GameManager once the human bear exists.</summary>
        public PlayerInputHandler input;
        public CinemachinePanTilt panTilt;

        [SerializeField] private float sensitivity = 2.5f;

        private void Update()
        {
            if (input == null || panTilt == null) return;

            Vector2 delta = input.LookDelta * sensitivity;
            panTilt.PanAxis.Value += delta.x;
            panTilt.TiltAxis.Value -= delta.y;
        }
    }
}
