using System.Collections;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Translates gameplay state into visual feedback. No rig exists yet (see TASKS.md Day 1),
    /// so this drives placeholder feedback directly on the capsule visual — a hit flash and
    /// hiding on elimination. Swap in a real Animator + clips here once the rig lands (Day 3).
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        /// <summary>Assigned by Day1SceneBuilder to the placeholder capsule's Renderer.</summary>
        public Renderer visualRenderer;

        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField] private float hitFlashDuration = 0.15f;

        private PlayerHealth health;
        private MaterialPropertyBlock propertyBlock;
        private Color originalColor = Color.white;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            propertyBlock = new MaterialPropertyBlock();
            if (visualRenderer != null && visualRenderer.sharedMaterial != null)
                originalColor = visualRenderer.sharedMaterial.color;
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.OnHeartsChanged += HandleHeartsChanged;
            health.OnEliminated += HandleEliminated;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.OnHeartsChanged -= HandleHeartsChanged;
            health.OnEliminated -= HandleEliminated;
        }

        private void HandleHeartsChanged(int hearts) => StartCoroutine(FlashHit());

        private IEnumerator FlashHit()
        {
            if (visualRenderer == null) yield break;
            SetColor(hitFlashColor);
            yield return new WaitForSeconds(hitFlashDuration);
            SetColor(originalColor);
        }

        private void HandleEliminated()
        {
            if (visualRenderer != null) visualRenderer.enabled = false; // simplest placeholder "knocked out"
        }

        private void SetColor(Color c)
        {
            visualRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", c); // URP Lit shader color property
            visualRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
