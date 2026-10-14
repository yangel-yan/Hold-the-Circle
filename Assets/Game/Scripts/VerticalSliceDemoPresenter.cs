using System;
using TMPro;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// World-space guidance for the three-minute vertical slice. The tutorial
    /// advances from real combat actions instead of timers, so the player must
    /// demonstrate both hand verbs before the first encounter begins.
    /// </summary>
    public class VerticalSliceDemoPresenter : MonoBehaviour
    {
        private enum TutorialStep
        {
            Inactive,
            RightSlash,
            LeftChain,
            Complete
        }

        private static readonly Color k_tutorialTint = new(0.55f, 0.9f, 1f, 1f);
        private static readonly Color k_combatTint = new(1f, 0.65f, 0.25f, 1f);
        private static readonly Color k_relicTint = new(0.85f, 0.55f, 1f, 1f);
        private static readonly Color k_completeTint = new(0.45f, 1f, 0.65f, 1f);

        private RunState m_runState;
        private Transform m_player;
        private Transform m_panel;
        private TextMeshPro m_label;
        private TutorialStep m_step;

        public event Action RightSlashLearned;
        public event Action TutorialCompleted;

        public void Initialize(RunState runState, Transform player)
        {
            m_runState = runState;
            m_player = player;
            BuildPanel();

            if (m_runState != null)
            {
                m_runState.Events.CombatActionPerformed += HandleCombatAction;
            }
        }

        public void BeginTutorial()
        {
            m_step = TutorialStep.RightSlash;
            Show(
                "STEP 1 / 2\nSWIPE YOUR RIGHT HAND",
                "A fast horizontal swipe throws an energy wave.",
                k_tutorialTint);
        }

        public void Hide()
        {
            m_step = TutorialStep.Inactive;
            if (m_panel != null) m_panel.gameObject.SetActive(false);
        }

        public void ShowNormalCombat()
        {
            Show(
                "NORMAL ENCOUNTER",
                "Use both hands. Keep the kill chain alive.",
                k_combatTint);
        }

        public void ShowRelicChoice()
        {
            Show(
                "CHOOSE ONE RELIC",
                "Choose an inscription, then confirm.",
                k_relicTint);
        }

        public void ShowEliteCombat()
        {
            Show(
                "ELITE ENCOUNTER",
                "Prioritize ranged Weavers and break the Tanks.",
                new Color(1f, 0.3f, 0.35f, 1f));
        }

        public void ShowComplete(int score, int kills)
        {
            m_step = TutorialStep.Complete;
            Show(
                "VERTICAL SLICE COMPLETE",
                $"Score {score:N0}  |  {kills} kills\nUse Restart to run it again.",
                k_completeTint);
        }

        private void LateUpdate()
        {
            if (m_panel == null || m_player == null) return;
            m_label.enabled = WorldMenu.Current == null || !WorldMenu.Current.IsOpen;

            Vector3 forward = m_player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            m_panel.position = m_player.position
                + forward * 1.45f
                + Vector3.up * 0.48f;
            m_panel.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private void HandleCombatAction(CombatActionEvent e)
        {
            if (m_step == TutorialStep.RightSlash
                && e.Kind == CombatActionKind.SlashWave)
            {
                m_step = TutorialStep.LeftChain;
                Show(
                    "STEP 2 / 2\nPOINT YOUR LEFT PALM",
                    "Hold an enemy in the blue cone to chain lightning.",
                    k_tutorialTint);
                RightSlashLearned?.Invoke();
                return;
            }

            if (m_step != TutorialStep.LeftChain
                || e.Kind != CombatActionKind.ChainLightning)
            {
                return;
            }

            m_step = TutorialStep.Complete;
            TutorialCompleted?.Invoke();
        }

        private void BuildPanel()
        {
            var root = new GameObject("VerticalSliceGuide");
            root.transform.SetParent(transform, false);

            m_label = root.AddComponent<TextMeshPro>();
            // TextMeshPro replaces Transform with RectTransform when attached.
            m_panel = root.transform;
            m_label.alignment = TextAlignmentOptions.Center;
            m_label.fontSize = 2.6f;
            m_label.textWrappingMode = TextWrappingModes.NoWrap;
            m_label.rectTransform.sizeDelta = new Vector2(9f, 3.2f);
            m_label.outlineWidth = 0.18f;
            m_label.outlineColor = new Color32(0, 0, 0, 220);
            m_label.richText = true;
            root.transform.localScale = Vector3.one * 0.11f;
        }

        private void Show(string title, string body, Color tint)
        {
            if (m_label == null) return;
            m_panel.gameObject.SetActive(true);

            string hex = ColorUtility.ToHtmlStringRGB(tint);
            L10n.Set(m_label,
                $"<color=#{hex}><b>{title}</b></color>\n"
                + $"<size=62%>{body}</size>");
        }

        private void OnDestroy()
        {
            if (m_runState != null)
            {
                m_runState.Events.CombatActionPerformed -= HandleCombatAction;
            }
        }
    }
}
