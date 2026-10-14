using UnityEngine;
using UnityEngine.Pool;

namespace HoldTheCircle
{
    /// <summary>Pooled ink bursts retain a thin, readable silhouette in passthrough.</summary>
    public class HitFlashEffect : MonoBehaviour
    {
        private static ObjectPool<HitFlashEffect> s_pool;
        private RiteGlyph m_impact;
        private RiteGlyph[] m_embers;
        private float m_elapsed;
        private bool m_kill;
        private Color m_tint;

        public static void Spawn(Vector3 position, Color tint) => Play(position, tint, false);
        public static void SpawnKill(Vector3 position, Color tint) => Play(position, tint, true);

        private static void Play(Vector3 position, Color tint, bool kill)
        {
            s_pool ??= new ObjectPool<HitFlashEffect>(Create,
                e => e.gameObject.SetActive(true), e => e.gameObject.SetActive(false),
                e => { if (e != null) Destroy(e.gameObject); }, false, 32, 64);
            HitFlashEffect effect = s_pool.Get();
            effect.transform.position = position;
            effect.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
            effect.m_elapsed = 0f;
            effect.m_kill = kill;
            effect.m_tint = tint;
            effect.m_impact.transform.localScale = Vector3.one * 0.12f;
            effect.m_impact.SetTint(tint);
            for (int i = 0; i < effect.m_embers.Length; i++)
            {
                effect.m_embers[i].gameObject.SetActive(kill);
                effect.m_embers[i].transform.localPosition = Vector3.zero;
            }
        }

        private static HitFlashEffect Create()
        {
            var effect = new GameObject("Lantern Impact").AddComponent<HitFlashEffect>();
            effect.m_impact = RiteGlyph.Create(effect.transform, "Impact Cut",
                RiteMotif.Impact, Vector2.one, Color.white);
            effect.m_embers = new RiteGlyph[3];
            for (int i = 0; i < effect.m_embers.Length; i++)
            {
                effect.m_embers[i] = RiteGlyph.Create(effect.transform, "Escaping Ember",
                    RiteMotif.Ember, new Vector2(0.07f, 0.1f), Color.white);
                effect.m_embers[i].transform.localRotation = Quaternion.Euler(0, 0, i * 120 - 25);
            }
            return effect;
        }

        private void Update()
        {
            m_elapsed += Time.deltaTime;
            float duration = m_kill ? 0.32f : 0.16f;
            float t = Mathf.Clamp01(m_elapsed / duration);
            Color ink = m_tint;
            ink.a = (1f - t) * (1f - t);
            m_impact.SetTint(ink);
            float scale = Mathf.Lerp(0.12f, m_kill ? 0.68f : 0.34f, Mathf.Sqrt(t));
            m_impact.transform.localScale = Vector3.one * scale;
            for (int i = 0; i < m_embers.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f + 0.3f;
                Vector3 axis = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                m_embers[i].transform.localPosition = axis * (t * 0.3f) + Vector3.up * (t * t * 0.04f);
                m_embers[i].SetTint(ink);
            }
            if (m_elapsed >= duration) s_pool.Release(this);
        }
    }
}
