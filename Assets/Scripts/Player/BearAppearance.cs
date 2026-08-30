using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// One knob for a bear's look. GameManager calls this once per spawn so six bears read as six
    /// colours from a single prefab. Works on the rigged FBX (SkinnedMeshRenderer) and on the
    /// capsule placeholder alike.
    ///
    ///  • <see cref="SetMaterial"/> — swap every renderer to a shared colour-variant material asset
    ///    (what Day1SceneBuilder generates under Assets/Graphics/ted/Variants). Preferred.
    ///  • <see cref="SetTint"/> — fallback when no variant material exists: instance the current
    ///    material and tint its base colour.
    /// </summary>
    public class BearAppearance : MonoBehaviour
    {
        [Tooltip("Renderers to recolour. Auto-filled from children if left empty.")]
        [SerializeField] private Renderer[] renderers;

        private Renderer[] Renderers
        {
            get
            {
                if (renderers == null || renderers.Length == 0)
                    renderers = GetComponentsInChildren<Renderer>(true);
                return renderers;
            }
        }

        public void SetMaterial(Material material)
        {
            if (material == null) return;
            foreach (var r in Renderers)
                if (r != null) r.sharedMaterial = material;
        }

        public void SetTint(Color color)
        {
            foreach (var r in Renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                var instance = new Material(r.sharedMaterial);
                if (instance.HasProperty("_BaseColor")) instance.SetColor("_BaseColor", color);
                if (instance.HasProperty("_Color")) instance.SetColor("_Color", color);
                r.sharedMaterial = instance;
            }
        }
    }
}
