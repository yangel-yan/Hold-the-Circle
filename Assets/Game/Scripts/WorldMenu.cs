using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.XR.CoreUtils;

namespace HoldTheCircle
{
    /// <summary>Readable world-space choices with selection followed by explicit confirmation.</summary>
    public sealed class WorldMenu : MonoBehaviour
    {
        public readonly struct Choice
        {
            public readonly string Title;
            public readonly string Detail;
            public readonly Action Action;
            public readonly bool Enabled;
            public readonly Texture2D Icon;
            public Choice(string title, string detail, Action action, bool enabled = true, Texture2D icon = null)
            {
                Title = title;
                Detail = detail;
                Action = action;
                Enabled = enabled;
                Icon = icon;
            }
        }

        private static WorldMenu s_instance;
        private PanelSettings m_settings;
        private UIDocument m_document;
        private VisualElement m_root;
        private Label m_title;
        private Label m_description;
        private Label m_selection;
        private Label m_hint;
        private VisualElement m_choicesRoot;
        private Button m_confirm;
        private IDisposable m_localization;
        private readonly List<Button> m_buttons = new(4);
        private readonly List<Choice> m_choices = new(4);
        private EyeHandMenuInput m_eyeHand;
        private int m_gazeTarget = -1;
        private int m_candidateTarget = -1;
        private float m_candidateAt;
        private float m_lastTargetAt;
        private int m_selected = -1;
        private int m_leftHover = -1;
        private int m_rightHover = -1;
        private float m_armedAt;
        private bool m_hasFocus = true;
        private bool m_applicationPaused;
        private const float k_rayDistance = 4f;
        private const string k_hint = "Look at a choice. Pinch either hand, release, then look at CONFIRM and pinch.";
        public static WorldMenu Current => s_instance;
        public bool IsOpen { get; private set; }
        public bool AcceptingInput => IsOpen && m_hasFocus && !m_applicationPaused;
        public string Title => m_title != null ? m_title.text : "";
        public int ChoiceCount => m_choices.Count;

        public static void Show(Transform player, Transform[] hands, string title,
            string description, params Choice[] choices)
        {
            if (s_instance == null)
                s_instance = new GameObject("World Menu").AddComponent<WorldMenu>();
            s_instance.Present(player, hands, title, description, choices);
        }

        private void OnEnable()
        {
            if (m_document != null) return;
            m_settings = ScriptableObject.CreateInstance<PanelSettings>();
            m_settings.renderMode = PanelRenderMode.WorldSpace;
            m_settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/LanternTheme");
            m_document = gameObject.AddComponent<UIDocument>();
            m_document.panelSettings = m_settings;
            m_document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            m_document.worldSpaceSize = new Vector2(960, 720);
            m_root = m_document.rootVisualElement;
            m_root.styleSheets.Add(Resources.Load<StyleSheet>("UI/WorldMenu"));
            m_root.AddToClassList("world-menu");
            var crest = new Image
            {
                image = RiteGlyph.Icon(RiteMotif.Lantern),
                tintColor = new Color(0.95f, 0.79f, 0.52f),
                pickingMode = PickingMode.Ignore
            };
            crest.AddToClassList("world-menu__crest");
            m_root.Add(crest);
            m_root.Add(new Label("HOLD THE CIRCLE  /  LANTERN RITES") { name = "eyebrow" });
            m_title = new Label();
            m_title.AddToClassList("world-menu__title");
            m_root.Add(m_title);
            m_description = new Label();
            m_description.AddToClassList("world-menu__description");
            m_root.Add(m_description);
            m_choicesRoot = new VisualElement();
            m_choicesRoot.AddToClassList("world-menu__choices");
            m_root.Add(m_choicesRoot);
            m_selection = new Label("Select an inscription");
            m_selection.AddToClassList("world-menu__selection");
            m_root.Add(m_selection);
            m_confirm = new Button(Confirm) { text = "CONFIRM" };
            m_confirm.AddToClassList("world-menu__confirm");
            m_root.Add(m_confirm);
            m_hint = new Label(k_hint);
            m_hint.AddToClassList("world-menu__hint");
            m_root.Add(m_hint);
            m_eyeHand = gameObject.AddComponent<EyeHandMenuInput>();
            m_localization = L10n.RegisterRoot(m_root);
        }

        private void Present(Transform player, Transform[] hands, string title,
            string description, Choice[] choices)
        {
            WaveCinematics.ClearBanner();
            var renderer = GetComponent<UIRenderer>();
            if (renderer != null) renderer.enabled = true;
            Transform viewpoint = Camera.main != null ? Camera.main.transform : player;
            XROrigin rig = viewpoint != null ? viewpoint.GetComponentInParent<XROrigin>() : null;
            if (rig == null) rig = FindFirstObjectByType<XROrigin>();
            Transform trackingSpace = rig != null && rig.CameraFloorOffsetObject != null
                ? rig.CameraFloorOffsetObject.transform : viewpoint != null ? viewpoint.parent : player;
            m_eyeHand.SetTrackingSpace(trackingSpace);
            m_gazeTarget = m_candidateTarget = -1;
            Vector3 forward = viewpoint != null ? viewpoint.forward : Vector3.forward;
            forward.y = 0;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Vector3 origin = viewpoint != null ? viewpoint.position : Vector3.zero;
            float eyeHeight = viewpoint != null ? viewpoint.position.y : 1.4f;
            transform.SetPositionAndRotation(new Vector3(origin.x, eyeHeight, origin.z) + forward * 1.05f,
                Quaternion.LookRotation(forward));
            // Unity 6.3 applies a 0.01 panel-unit transform to the root element.
            transform.localScale = Vector3.one * 0.12f;
            L10n.Set(m_title, title);
            L10n.Set(m_description, description);
            m_choices.Clear();
            m_choices.AddRange(choices);
            m_choicesRoot.Clear();
            m_buttons.Clear();
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                Button button = new Button(() => Select(index));
                button.AddToClassList("world-menu__choice");
                var number = new Label((i + 1).ToString("00")) { pickingMode = PickingMode.Ignore };
                number.AddToClassList("world-menu__choice-number");
                button.Add(number);
                var icon = new Image
                {
                    image = choices[i].Icon != null ? choices[i].Icon : RiteGlyph.Icon(ChoiceMotif(choices[i])),
                    tintColor = choices[i].Icon != null ? Color.white : new Color(0.89f, 0.81f, 0.61f),
                    pickingMode = PickingMode.Ignore
                };
                icon.AddToClassList("world-menu__choice-icon");
                button.Add(icon);
                var choiceTitle = new Label(choices[i].Title) { pickingMode = PickingMode.Ignore };
                L10n.Set(choiceTitle, choices[i].Title);
                choiceTitle.AddToClassList("world-menu__choice-title");
                button.Add(choiceTitle);
                var detail = new Label(choices[i].Detail) { pickingMode = PickingMode.Ignore };
                L10n.Set(detail, choices[i].Detail);
                detail.AddToClassList("world-menu__choice-detail");
                button.Add(detail);
                button.SetEnabled(choices[i].Enabled);
                m_choicesRoot.Add(button);
                m_buttons.Add(button);
            }
            m_selected = -1;
            SetHover(-1, -1);
            m_armedAt = Time.unscaledTime + 0.4f;
            L10n.Set(m_selection, "Select an inscription");
            L10n.Set(m_hint, k_hint);
            m_confirm.SetEnabled(false);
            m_root.style.display = DisplayStyle.Flex;
            IsOpen = true;
            Debug.Log($"[HandMenu] Open: {title}, choices={choices.Length}, trackingSpace={trackingSpace}");
        }

        private static RiteMotif ChoiceMotif(Choice choice)
        {
            string title = choice.Title;
            if (title.IndexOf("STORM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("CHAIN", StringComparison.OrdinalIgnoreCase) >= 0)
                return RiteMotif.Storm;
            if (title.IndexOf("LANCE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("SLASH", StringComparison.OrdinalIgnoreCase) >= 0)
                return RiteMotif.Lance;
            if (title.IndexOf("AEGIS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("REST", StringComparison.OrdinalIgnoreCase) >= 0)
                return RiteMotif.Aegis;
            if (title.IndexOf("BOSS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("ELITE", StringComparison.OrdinalIgnoreCase) >= 0)
                return RiteMotif.Ember;
            return RiteMotif.Lantern;
        }

        public void Select(int index)
        {
            if (!IsOpen || index < 0 || index >= m_choices.Count || !m_choices[index].Enabled) return;
            m_selected = index;
            for (int i = 0; i < m_buttons.Count; i++)
                m_buttons[i].EnableInClassList("world-menu__choice--selected", i == index);
            L10n.Set(m_selection, m_choices[index].Title + " — ready to confirm");
            m_confirm.SetEnabled(true);
        }

        public void Confirm()
        {
            if (!IsOpen || m_selected < 0) return;
            Action action = m_choices[m_selected].Action;
            Hide();
            action?.Invoke();
        }

        public void Hide()
        {
            IsOpen = false;
            m_selected = -1;
            SetHover(-1, -1);
            m_eyeHand?.ResetInput();
            // Keep UIDocument attached: disabling it recreates the root visual tree on reopen.
            var renderer = GetComponent<UIRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        private void Update()
        {
            if (!AcceptingInput) return;
#if UNITY_EDITOR
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
                if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
                if (keyboard.digit3Key.wasPressedThisFrame) Select(2);
                if (keyboard.digit4Key.wasPressedThisFrame) Select(3);
                if (keyboard.enterKey.wasPressedThisFrame) Confirm();
            }
#endif
            if (!IsOpen) return;
            bool pressed = m_eyeHand.ReadPress();
            if (!m_eyeHand.TryGetRay(out Ray ray)) return;
            int rawTarget = RaycastButton(ray, out Vector3 hit, out bool onPanel);
            if (rawTarget != m_candidateTarget)
            {
                m_candidateTarget = rawTarget;
                m_candidateAt = Time.unscaledTime;
            }
            if (rawTarget >= 0)
            {
                m_lastTargetAt = Time.unscaledTime;
                if (m_gazeTarget < 0 || Time.unscaledTime - m_candidateAt >= 0.06f)
                    m_gazeTarget = rawTarget;
            }
            else if (Time.unscaledTime - m_lastTargetAt > 0.18f) m_gazeTarget = -1;
            SetHover(m_gazeTarget, m_gazeTarget);
            m_eyeHand.Draw(hit, onPanel, m_gazeTarget >= 0, transform.rotation);
            if (Time.unscaledTime < m_armedAt) return;
            // Consume at most one press; an action may synchronously present the next menu.
            int target = pressed ? m_gazeTarget : -1;
            if (target < 0) return;
            Debug.Log($"[EyeHandMenu] Select target={target}, menu={Title}, eyes={m_eyeHand.UsingEyes}");
            if (target == m_buttons.Count) Confirm();
            else Select(target);
        }

        private int RaycastButton(Ray ray, out Vector3 hit, out bool onPanel)
        {
            hit = ray.GetPoint(k_rayDistance);
            onPanel = false;
            if (m_root.panel == null || Vector3.Dot(transform.forward, ray.direction) <= 0.001f) return -1;
            var plane = new Plane(transform.forward, transform.position);
            if (!plane.Raycast(ray, out float distance) || distance > k_rayDistance) return -1;
            Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance));
            // worldTransform contains Unity's panel-unit scale, centering and Y inversion.
            // Convert into each element's own space instead of assuming a pixels-to-meters factor.
            Vector3 rootPoint = m_root.worldTransform.inverse.MultiplyPoint3x4(local);
            if (!new Rect(Vector2.zero, m_root.layout.size).Contains(new Vector2(rootPoint.x, rootPoint.y))) return -1;
            hit = ray.GetPoint(distance);
            onPanel = true;
            for (int i = 0; i < m_buttons.Count; i++)
            {
                if (m_choices[i].Enabled && ContainsPoint(m_buttons[i], local)) return i;
            }
            return m_selected >= 0 && ContainsPoint(m_confirm, local) ? m_buttons.Count : -1;
        }

        private static bool ContainsPoint(VisualElement element, Vector3 documentPoint)
        {
            Vector3 point = element.worldTransform.inverse.MultiplyPoint3x4(documentPoint);
            const float tolerance = 8f;
            return element.enabledInHierarchy &&
                new Rect(-tolerance, -tolerance, element.layout.width + tolerance * 2f,
                    element.layout.height + tolerance * 2f).Contains(new Vector2(point.x, point.y));
        }

        private void SetHover(int left, int right)
        {
            if (m_leftHover != left || m_rightHover != right)
            {
                m_leftHover = left;
                m_rightHover = right;
                for (int i = 0; i < m_buttons.Count; i++)
                    m_buttons[i].EnableInClassList("world-menu__choice--ray-hover", i == left || i == right);
                m_confirm.EnableInClassList("world-menu__confirm--ray-hover",
                    left == m_buttons.Count || right == m_buttons.Count);
            }
            string hint = left < 0 && right < 0 ? m_eyeHand.Hint :
                left == m_buttons.Count || right == m_buttons.Count ? "Pinch to confirm" : "Pinch to select";
            if (L10n.SourceText(m_hint) != hint) L10n.Set(m_hint, hint);
        }

        private void OnApplicationFocus(bool focus)
        {
            m_hasFocus = focus;
            ResetPointers();
        }

        private void OnApplicationPause(bool pause)
        {
            m_applicationPaused = pause;
            ResetPointers();
        }

        private void OnDisable()
        {
            if (m_document != null) Hide();
        }

        private void ResetPointers()
        {
            m_gazeTarget = m_candidateTarget = -1;
            SetHover(-1, -1);
            m_eyeHand?.ResetInput();
        }

        private void OnDestroy()
        {
            m_localization?.Dispose();
            if (s_instance == this) s_instance = null;
            if (m_settings != null) Destroy(m_settings);
        }
    }
}
