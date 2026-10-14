using UnityEngine;
using UnityEngine.UIElements;

namespace HoldTheCircle
{
    /// <summary>Shared world-space typography and meters for combat readouts.</summary>
    internal sealed class RitePanel
    {
        private readonly PanelSettings m_settings;
        private readonly GameObject m_object;
        private UIRenderer m_renderer;
        private readonly System.IDisposable m_localization;
        public VisualElement Root { get; }
        public Transform Transform => m_object.transform;

        public RitePanel(string name, Vector2 size)
        {
            m_object = new GameObject(name);
            m_settings = ScriptableObject.CreateInstance<PanelSettings>();
            m_settings.renderMode = PanelRenderMode.WorldSpace;
            m_settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/LanternTheme");
            UIDocument document = m_object.AddComponent<UIDocument>();
            document.panelSettings = m_settings;
            document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            document.worldSpaceSize = size;
            Root = document.rootVisualElement;
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/RiteHUD"));
            Root.AddToClassList("rite-hud");
            Root.pickingMode = PickingMode.Ignore;
            Root.style.width = size.x;
            Root.style.height = size.y;
            Transform.localScale = Vector3.one * 0.1f;
            m_localization = L10n.RegisterRoot(Root);
        }

        public void SetVisible(bool visible)
        {
            if (m_object == null) return;
            // Preserve the document tree while hidden, as on the choice menu.
            if (m_renderer == null) m_renderer = m_object.GetComponent<UIRenderer>();
            if (m_renderer != null) m_renderer.enabled = visible;
            Root.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        public void Place(Transform viewpoint, float distance, float height)
        {
            Vector3 forward = viewpoint.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Transform.SetPositionAndRotation(viewpoint.position + forward * distance + Vector3.up * height,
                Quaternion.LookRotation(forward));
        }

        public void Dispose()
        {
            m_localization.Dispose();
            if (m_object != null) Object.Destroy(m_object);
            if (m_settings != null) Object.Destroy(m_settings);
        }

        public static Label Label(VisualElement parent, string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            L10n.Set(label, text);
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        public static VisualElement Row(VisualElement parent, string className)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList(className);
            parent.Add(row);
            return row;
        }
    }

    internal sealed class RiteMeter
    {
        private readonly VisualElement m_fill;
        private readonly Label m_value;
        private int m_lastPercent = -1;
        public VisualElement Root { get; }

        public RiteMeter(VisualElement parent, string title, RiteMotif motif, Color tint)
        {
            Root = RitePanel.Row(parent, "rite-hud__meter");
            VisualElement heading = RitePanel.Row(Root, "rite-hud__meter-heading");
            var icon = new Image { image = RiteGlyph.Icon(motif), pickingMode = PickingMode.Ignore, tintColor = tint };
            icon.AddToClassList("rite-hud__icon");
            heading.Add(icon);
            RitePanel.Label(heading, title, "rite-hud__meter-title");
            m_value = RitePanel.Label(heading, "100%", "rite-hud__meter-value");
            VisualElement track = RitePanel.Row(Root, "rite-hud__track");
            m_fill = RitePanel.Row(track, "rite-hud__fill");
            m_fill.style.backgroundColor = tint;
        }

        public void Set(float ratio, Color tint, string value = null)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100);
            if (percent != m_lastPercent)
            {
                m_fill.style.width = Length.Percent(percent);
                L10n.Set(m_value, value ?? percent + "%");
                m_lastPercent = percent;
            }
            else if (value != null && m_value.text != value)
            {
                L10n.Set(m_value, value);
            }
            m_fill.style.backgroundColor = tint;
        }
    }
}
