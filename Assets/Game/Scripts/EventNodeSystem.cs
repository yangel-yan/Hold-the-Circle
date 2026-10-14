using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Text-choice event node. Presents a short flavor prompt + two glowing
    /// option plinths the player can tap. Each option carries a deterministic
    /// effect (risk-vs-reward) so designers can author them without needing to
    /// run the actual encounter.
    ///
    /// Code-built world-space UI; uses TMP for legibility. Resolves with a
    /// callback so GameDirector can drive ContinueAfterNode.
    /// </summary>
    public class EventNodeSystem : MonoBehaviour
    {
        public struct EventOption
        {
            public string Label;
            public Color Tint;
            public Action<RunState, PlayerHealth> Apply;
        }

        public struct EventDefinition
        {
            public string Title;
            public string Prompt;
            public EventOption Left;
            public EventOption Right;
        }

        private const float k_distanceInMeters = 1.0f;
        private const float k_spreadInMeters = 0.4f;

        private RunState m_runState;
        private PlayerHealth m_playerHealth;
        private Transform m_player;
        private Transform[] m_hands;

        private GameObject m_panelGo;
        private MerchantPlinth m_leftPlinth;
        private MerchantPlinth m_rightPlinth;
        private bool m_resolved;

        public event Action Resolved;

        private static readonly List<EventDefinition> k_pool = new()
        {
            new EventDefinition
            {
                Title = "Frostbound Pact",
                Prompt = "Bleed for power, or walk away unharmed?",
                Left = new EventOption
                {
                    Label = "Bleed +10% damage (-15 HP)",
                    Tint = new Color(1f, 0.4f, 0.45f, 1f),
                    Apply = (rs, hp) =>
                    {
                        rs.AddDamage(0.10f);
                        if (hp != null) hp.ApplyDamage(15f);
                    }
                },
                Right = new EventOption
                {
                    Label = "Walk away (+2 shards)",
                    Tint = new Color(0.55f, 1f, 0.7f, 1f),
                    Apply = (rs, hp) => rs.AddShards(2)
                }
            },
            new EventDefinition
            {
                Title = "Pulsing Crystal",
                Prompt = "Pry it open?",
                Left = new EventOption
                {
                    Label = "Pry +1 projectile",
                    Tint = new Color(0.5f, 0.85f, 1f, 1f),
                    Apply = (rs, hp) => rs.AddProjectiles(1)
                },
                Right = new EventOption
                {
                    Label = "Leave it (+1 shard)",
                    Tint = new Color(0.55f, 1f, 0.7f, 1f),
                    Apply = (rs, hp) => rs.AddShards(1)
                }
            },
            new EventDefinition
            {
                Title = "Sentinel Echo",
                Prompt = "Mimic the sentinels' cadence?",
                Left = new EventOption
                {
                    Label = "Mimic +15% fire rate",
                    Tint = new Color(1f, 0.7f, 0.3f, 1f),
                    Apply = (rs, hp) => rs.AddFireRate(0.15f)
                },
                Right = new EventOption
                {
                    Label = "Heal +20 HP",
                    Tint = new Color(0.55f, 1f, 0.7f, 1f),
                    Apply = (rs, hp) => hp?.Heal(20f)
                }
            }
        };

        public void Initialize(RunState runState, PlayerHealth playerHealth, Transform player, Transform[] hands)
        {
            m_runState = runState;
            m_playerHealth = playerHealth;
            m_player = player;
            m_hands = hands;
        }

        public void Open()
        {
            Close();
            m_resolved = false;

            EventDefinition def = k_pool[UnityEngine.Random.Range(0, k_pool.Count)];
            EventOption leftOpt = def.Left;
            EventOption rightOpt = def.Right;
            WorldMenu.Show(m_player, m_hands, def.Title, def.Prompt,
                new WorldMenu.Choice(leftOpt.Label, "Accept the pact", () => HandlePicked(leftOpt)),
                new WorldMenu.Choice(rightOpt.Label, "Choose recovery", () => HandlePicked(rightOpt)));
        }

        public void Close()
        {
            if (m_panelGo != null)
            {
                Destroy(m_panelGo);
                m_panelGo = null;
            }
            if (m_leftPlinth != null)
            {
                Destroy(m_leftPlinth.gameObject);
                m_leftPlinth = null;
            }
            if (m_rightPlinth != null)
            {
                Destroy(m_rightPlinth.gameObject);
                m_rightPlinth = null;
            }
        }

        private void BuildPanel(Vector3 origin, Vector3 forward, EventDefinition def)
        {
            m_panelGo = new GameObject("EventPanel");
            m_panelGo.transform.position = origin + forward * k_distanceInMeters + Vector3.up * 0.4f;
            m_panelGo.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(m_panelGo.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0.7f, 0.3f);
            rt.localScale = Vector3.one * 0.0025f;

            CreateLabel(canvasGo.transform, def.Title, 50, new Vector2(0f, 60f), new Color(1f, 1f, 1f, 1f));
            CreateLabel(canvasGo.transform, def.Prompt, 32, new Vector2(0f, 0f), new Color(0.9f, 0.9f, 0.9f, 1f));
            CreateLabel(canvasGo.transform, def.Left.Label, 26, new Vector2(-90f, -60f), def.Left.Tint);
            CreateLabel(canvasGo.transform, def.Right.Label, 26, new Vector2(90f, -60f), def.Right.Tint);
        }

        private static void CreateLabel(Transform parent, string text, int fontSize, Vector2 anchored, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = anchored;
            rt.sizeDelta = new Vector2(220f, 60f);
            var label = go.AddComponent<TextMeshProUGUI>();
            L10n.Set(label, text);
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
        }

        private void HandlePicked(EventOption option)
        {
            if (m_resolved) return;
            m_resolved = true;
            option.Apply?.Invoke(m_runState, m_playerHealth);
            Close();
            Resolved?.Invoke();
        }

        private void OnDestroy()
        {
            Close();
        }
    }
}
