using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>Thin etched seal keeps the real floor visible in MR.</summary>
    public class ArenaBuilder : MonoBehaviour
    {
        private Transform m_player;
        private Transform m_seal;
        private readonly List<LineRenderer> m_segments = new(12);
        private readonly List<RiteGlyph> m_inscriptions = new(12);
        private int m_lastStability = -1;
        public Vector3 Origin => m_seal != null ? m_seal.position : Vector3.zero;
        public const float Radius = 2.15f;

        public void Initialize(Transform player) => m_player = player;
        public void SetUseRealWorldCover(bool useRealWorldCover) { }
        public void Rebuild() => Rebuild(ArenaLayoutKind.Combat);

        public void Rebuild(ArenaLayoutKind layout)
        {
            if (m_seal != null) Destroy(m_seal.gameObject);
            m_segments.Clear();
            m_inscriptions.Clear();
            m_lastStability = -1;
            m_seal = new GameObject("Lantern Seal").transform;
            m_seal.SetParent(transform, false);
            Vector3 origin = m_player != null ? m_player.position : Vector3.zero;
            origin.y = 0.025f;
            m_seal.position = origin;
            Color ink = new Color(0.55f, 0.78f, 1f, 0.82f);
            Color gold = new Color(1f, 0.68f, 0.3f, 0.68f);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;
                m_segments.Add(RuneVisual.Arc(m_seal, "Seal Segment", Radius, angle + 2, 26, ink, 0.028f));
                RuneVisual.Arc(m_seal, "Etched Inner Arc", Radius - 0.12f, angle + 7, 16, gold, 0.012f);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 axis = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                RiteGlyph glyph = RiteGlyph.Create(m_seal, "Seal Inscription " + i,
                    i % 3 == 0 ? RiteMotif.Lantern : RiteMotif.Aegis, new Vector2(0.4f, 0.42f), gold);
                glyph.transform.localPosition = axis * (Radius + 0.2f);
                glyph.transform.localRotation = Quaternion.AngleAxis(90f - angle, Vector3.up) *
                    Quaternion.Euler(90f, 0, 0);
                m_inscriptions.Add(glyph);
            }
            SetStability(1f);
        }

        public void SetStability(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            int percent = Mathf.RoundToInt(ratio * 100);
            if (percent == m_lastStability) return;
            m_lastStability = percent;
            int intact = Mathf.CeilToInt(ratio * m_segments.Count);
            for (int i = 0; i < m_segments.Count; i++)
            {
                Color color = i < intact
                    ? Color.Lerp(new Color(1f, 0.38f, 0.15f, 0.85f), new Color(0.55f, 0.78f, 1f, 0.85f), ratio)
                    : new Color(0.3f, 0.2f, 0.2f, 0.25f);
                m_segments[i].startColor = m_segments[i].endColor = color;
                Color inscription = i < intact
                    ? Color.Lerp(new Color(1f, 0.43f, 0.18f, 0.85f), new Color(1f, 0.76f, 0.42f, 0.8f), ratio)
                    : new Color(0.38f, 0.26f, 0.21f, 0.18f);
                m_inscriptions[i].SetTint(inscription);
            }
        }
    }
}
