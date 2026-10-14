using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoldTheCircle
{
    public enum RiteMotif
    {
        Lantern,
        Aegis,
        Storm,
        Lance,
        Ember,
        Impact,
        Slash,
        Band
    }

    /// <summary>Authored transparent art, shared assets and allocation-free per-renderer tinting.</summary>
    public sealed class RiteGlyph : MonoBehaviour
    {
        private static readonly int s_tintId = Shader.PropertyToID("_Tint");
        private static readonly int s_rectId = Shader.PropertyToID("_UvRect");
        private static readonly int s_fillId = Shader.PropertyToID("_Fill");
        private static readonly Texture2D[] s_icons = new Texture2D[8];
        private static readonly Dictionary<string, Texture2D> s_relicIcons = new(24);
        private static Mesh s_quad;
        private static Material s_material;
        private static Material s_ribbon;

        private MeshRenderer m_renderer;
        private MaterialPropertyBlock m_properties;

        public static Material RibbonMaterial
        {
            get
            {
                if (s_ribbon == null)
                {
                    s_ribbon = Resources.Load<Material>("Art/RiteRibbon");
                }
                return s_ribbon;
            }
        }

        public static RiteGlyph Create(Transform parent, string name, RiteMotif motif,
            Vector2 size, Color tint)
        {
            if (s_quad == null)
            {
                s_quad = Resources.Load<Mesh>("Art/RiteQuad");
                s_material = Resources.Load<Material>("Art/RiteAtlas");
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = s_quad;
            var glyph = go.AddComponent<RiteGlyph>();
            glyph.m_renderer = go.AddComponent<MeshRenderer>();
            glyph.m_renderer.sharedMaterial = s_material;
            glyph.m_renderer.shadowCastingMode = ShadowCastingMode.Off;
            glyph.m_renderer.receiveShadows = false;
            glyph.m_renderer.lightProbeUsage = LightProbeUsage.Off;
            glyph.m_renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            glyph.m_properties = new MaterialPropertyBlock();
            glyph.SetMotif(motif);
            glyph.SetTint(tint);
            glyph.SetFill(1f);
            return glyph;
        }

        public static Texture2D Icon(RiteMotif motif)
        {
            int index = (int)motif;
            if (s_icons[index] == null)
            {
                s_icons[index] = Resources.Load<Texture2D>("Art/Icons/" + motif);
            }
            return s_icons[index];
        }

        public static Texture2D RelicIcon(RelicSO relic)
        {
            if (relic == null) return Icon(RiteMotif.Lantern);
            if (!s_relicIcons.TryGetValue(relic.name, out Texture2D icon))
            {
                icon = Resources.Load<Texture2D>("Art/Relics/" + relic.name);
                s_relicIcons.Add(relic.name, icon);
            }
            return icon != null ? icon : Icon(RiteMotif.Lantern);
        }

        public void SetTint(Color tint)
        {
            m_properties.SetColor(s_tintId, tint);
            m_renderer.SetPropertyBlock(m_properties);
        }

        public void SetMotif(RiteMotif motif)
        {
            int index = (int)motif;
            m_properties.SetVector(s_rectId,
                new Vector4((index % 4) * 0.25f, (1 - index / 4) * 0.5f, 0.25f, 0.5f));
            m_renderer.SetPropertyBlock(m_properties);
        }

        // Clip rather than scale, so engraved ticks remain the same width as health drains.
        public void SetFill(float fill)
        {
            m_properties.SetFloat(s_fillId, Mathf.Clamp01(fill));
            m_renderer.SetPropertyBlock(m_properties);
        }
    }
}
