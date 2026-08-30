using UnityEngine;

namespace BearlyStanding
{
    /// <summary>Slowly turns the menu's display Ted.</summary>
    public class MenuSpin : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 20f;

        private void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
