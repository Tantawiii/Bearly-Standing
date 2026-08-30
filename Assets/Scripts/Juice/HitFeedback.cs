using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Pooled impact juice: an expanding dust puff on every hit, plus a punchy cartoon
    /// "BONK!" / "POW!" world-space word on strong / final hits. All built procedurally so
    /// there are no art dependencies.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        private static HitFeedback instance;

        private readonly Queue<Transform> puffPool = new Queue<Transform>();
        private readonly Queue<TextMesh> wordPool = new Queue<TextMesh>();
        private Material puffMaterial;
        private Font wordFont;

        private static readonly string[] LightWords = { "bap", "pff", "tap", "fwip" };
        private static readonly string[] HeavyWords = { "BONK!", "POW!", "WHAM!", "OOF!", "SPLAT!" };

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            puffMaterial = new Material(shader) { name = "DustPuff" };
            if (puffMaterial.HasProperty("_BaseColor")) puffMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.7f));
            if (puffMaterial.HasProperty("_Surface")) puffMaterial.SetFloat("_Surface", 1f); // transparent

            wordFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <param name="tier">1 = first hit … 3 = downing/final hit.</param>
        public static void Play(Vector3 worldPosition, int tier)
        {
            if (instance == null) return;
            instance.SpawnPuff(worldPosition, 0.6f + 0.35f * tier);
            if (tier >= 2)
            {
                string[] pool = tier >= 3 ? HeavyWords : (Random.value < 0.5f ? HeavyWords : LightWords);
                instance.SpawnWord(worldPosition + Vector3.up * 0.6f, pool[Random.Range(0, pool.Length)], tier);
            }
        }

        private void SpawnPuff(Vector3 pos, float size)
        {
            Transform puff = puffPool.Count > 0 ? puffPool.Dequeue() : CreatePuff();
            puff.position = pos;
            puff.rotation = Random.rotationUniform;
            puff.gameObject.SetActive(true);
            StartCoroutine(PuffRoutine(puff, size));
        }

        private Transform CreatePuff()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Puff";
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = puffMaterial;
            go.transform.SetParent(transform);
            return go.transform;
        }

        private IEnumerator PuffRoutine(Transform puff, float size)
        {
            var mpb = new MaterialPropertyBlock();
            var renderer = puff.GetComponent<Renderer>();
            float t = 0f;
            const float life = 0.35f;
            while (t < life)
            {
                t += Time.unscaledDeltaTime;
                float k = t / life;
                puff.localScale = Vector3.one * Mathf.Lerp(0.15f, size, Mathf.Sqrt(k));
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.7f * (1f - k)));
                mpb.SetColor("_Color", new Color(1f, 1f, 1f, 0.7f * (1f - k)));
                renderer.SetPropertyBlock(mpb);
                yield return null;
            }
            puff.gameObject.SetActive(false);
            puffPool.Enqueue(puff);
        }

        private void SpawnWord(Vector3 pos, string text, int tier)
        {
            TextMesh word = wordPool.Count > 0 ? wordPool.Dequeue() : CreateWord();
            word.text = text;
            word.fontSize = tier >= 3 ? 96 : 64;
            word.color = tier >= 3 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            word.transform.position = pos;
            word.gameObject.SetActive(true);
            StartCoroutine(WordRoutine(word));
        }

        private TextMesh CreateWord()
        {
            var go = new GameObject("HitWord");
            go.transform.SetParent(transform);
            var tm = go.AddComponent<TextMesh>();
            tm.font = wordFont;
            tm.GetComponent<MeshRenderer>().sharedMaterial = wordFont.material;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.08f;
            tm.fontStyle = FontStyle.Bold;
            return tm;
        }

        private IEnumerator WordRoutine(TextMesh word)
        {
            Camera cam = Camera.main;
            Vector3 start = word.transform.position;
            Color baseColor = word.color;
            float t = 0f;
            const float life = 0.7f;
            while (t < life)
            {
                t += Time.unscaledDeltaTime;
                float k = t / life;
                float pop = k < 0.2f ? Mathf.Lerp(0.3f, 1.15f, k / 0.2f) : Mathf.Lerp(1.15f, 1f, (k - 0.2f) / 0.8f);
                word.transform.position = start + Vector3.up * (k * 0.8f);
                word.transform.localScale = Vector3.one * pop;
                if (cam != null) word.transform.rotation = Quaternion.LookRotation(word.transform.position - cam.transform.position);
                word.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - Mathf.Clamp01((k - 0.5f) / 0.5f));
                yield return null;
            }
            word.gameObject.SetActive(false);
            wordPool.Enqueue(word);
        }

        internal static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("HitFeedback");
            go.AddComponent<HitFeedback>();
            DontDestroyOnLoad(go);
        }
    }
}
