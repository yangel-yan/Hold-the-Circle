using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Merchant node controller. Spawns a small in-room shop where the player
    /// can spend in-run shards on either a fresh relic or an existing-relic
    /// removal service. Designed as code-built shrines; no prefab wiring.
    ///
    /// Closes via Resolved when the player either buys something or steps on
    /// the "leave" plinth, so the director can roll the run forward.
    /// </summary>
    public class MerchantSystem : MonoBehaviour
    {
        // Shop costs. Tuned in W8 so a merchant visit feels like a real
        // commitment: a relic purchase costs ~8 kills, a removal ~5.
        public const int k_relicCost = 8;
        public const int k_removeCost = 5;

        private const float k_distanceInMeters = 0.95f;
        private const float k_spreadInMeters = 0.34f;

        private RunState m_runState;
        private RelicSystem m_relicSystem;
        private Transform m_player;
        private Transform[] m_hands;

        private RelicShrine m_relicShrine;
        private MerchantPlinth m_removePlinth;
        private MerchantPlinth m_leavePlinth;
        private RelicSO m_offeredRelic;
        private bool m_resolved;

        public event Action Resolved;

        public void Initialize(RunState runState, RelicSystem relicSystem, Transform player, Transform[] hands)
        {
            m_runState = runState;
            m_relicSystem = relicSystem;
            m_player = player;
            m_hands = hands;
        }

        // Open the shop: pick one rarity-weighted relic for sale, place a
        // remove-service plinth, and a leave plinth.
        public void Open()
        {
            Close();
            m_resolved = false;
            m_offeredRelic = m_relicSystem != null ? m_relicSystem.PeekRandom() : null;
            var owned = m_runState.Relics;
            string removed = owned.Count > 0 ? owned[0].Source.DisplayName : "No relic owned";
            WorldMenu.Show(m_player, m_hands, "THE LANTERN KEEPER",
                $"You carry {m_runState.Shards} shards. Confirm a purchase, removal, or departure.",
                new WorldMenu.Choice(m_offeredRelic != null ? m_offeredRelic.DisplayName : "Sold out",
                    $"{k_relicCost} shards\n{m_offeredRelic?.Description}",
                    () => HandleRelicTouched(m_offeredRelic),
                    m_offeredRelic != null && m_runState.Shards >= k_relicCost, RiteGlyph.RelicIcon(m_offeredRelic)),
                new WorldMenu.Choice("REMOVE RELIC", $"{removed}\n{k_removeCost} shards, 1 refunded",
                    HandleRemoveTouched, owned.Count > 0 && m_runState.Shards >= k_removeCost),
                new WorldMenu.Choice("DEPART", "Keep your shards", HandleLeaveTouched));
        }

        public void Close()
        {
            if (m_relicShrine != null)
            {
                Destroy(m_relicShrine.gameObject);
                m_relicShrine = null;
            }
            if (m_removePlinth != null)
            {
                Destroy(m_removePlinth.gameObject);
                m_removePlinth = null;
            }
            if (m_leavePlinth != null)
            {
                Destroy(m_leavePlinth.gameObject);
                m_leavePlinth = null;
            }
            m_offeredRelic = null;
        }

        private void HandleRelicTouched(RelicSO so)
        {
            if (m_resolved) return;
            if (m_runState == null || !m_runState.TrySpendShards(k_relicCost))
            {
                // Not enough currency: respawn the shrine so the player can
                // try again later.
                RespawnRelicShrine();
                return;
            }

            m_runState.AddRelic(so);
            m_relicSystem?.MarkClaimed(so);
            Finish();
        }

        private void HandleRemoveTouched()
        {
            if (m_resolved) return;
            // Pick the oldest relic the player owns and refund it.
            IReadOnlyList<RelicEffect> owned = m_runState != null ? m_runState.Relics : null;
            if (owned == null || owned.Count == 0)
            {
                // Nothing to remove: respawn the plinth so it stays interactive.
                RespawnRemovePlinth();
                return;
            }
            if (!m_runState.TrySpendShards(k_removeCost))
            {
                RespawnRemovePlinth();
                return;
            }

            RelicEffect target = owned[0];
            m_runState.RemoveRelic(target);
            // Small refund: removal still costs net 2 shards.
            m_runState.AddShards(1);
            Finish();
        }

        private void RespawnRelicShrine()
        {
            if (m_relicShrine == null || m_offeredRelic == null) return;
            Destroy(m_relicShrine.gameObject);
            Vector3 origin = m_player != null ? m_player.position : Vector3.zero;
            Vector3 forward = m_player != null ? m_player.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 pos = origin + forward * k_distanceInMeters + right * (-k_spreadInMeters);
            m_relicShrine = RelicShrine.Spawn(pos, m_offeredRelic, m_hands, HandleRelicTouched);
        }

        private void RespawnRemovePlinth()
        {
            if (m_removePlinth == null) return;
            Destroy(m_removePlinth.gameObject);
            Vector3 origin = m_player != null ? m_player.position : Vector3.zero;
            Vector3 forward = m_player != null ? m_player.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Vector3 removePos = origin + forward * k_distanceInMeters;
            m_removePlinth = MerchantPlinth.Spawn(removePos,
                new Color(1f, 0.45f, 0.45f, 1f), m_hands, HandleRemoveTouched);
        }

        private void HandleLeaveTouched()
        {
            if (m_resolved) return;
            Finish();
        }

        private void Finish()
        {
            m_resolved = true;
            Close();
            Resolved?.Invoke();
        }

        private void OnDestroy()
        {
            Close();
        }
    }

    /// <summary>
    /// Tiny floating cube the player taps to commit to a merchant action.
    /// Reused for "remove relic" and "leave shop" buttons.
    /// </summary>
    public class MerchantPlinth : MonoBehaviour
    {
        private const float k_radiusInMeters = 0.09f;
        private const float k_selectDistanceInMeters = 0.13f;

        private Transform[] m_hands;
        private Action m_onSelected;
        private Material m_material;
        private Transform m_visual;
        private Vector3 m_basePosition;
        private float m_phase;
        private bool m_selected;

        public static MerchantPlinth Spawn(Vector3 position, Color tint, Transform[] hands, Action onSelected)
        {
            var go = new GameObject("MerchantPlinth");
            go.transform.position = position;
            var plinth = go.AddComponent<MerchantPlinth>();
            plinth.Build(tint, hands, onSelected);
            return plinth;
        }

        private void Build(Color tint, Transform[] hands, Action onSelected)
        {
            m_hands = hands;
            m_onSelected = onSelected;
            m_basePosition = transform.position;
            m_phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            Destroy(cube.GetComponent<Collider>());
            m_visual = cube.transform;
            m_visual.SetParent(transform, false);
            m_visual.localScale = Vector3.one * (k_radiusInMeters * 1.6f);
            m_visual.localRotation = Quaternion.Euler(45f, 35f, 0f);

            m_material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m_material.EnableKeyword("_EMISSION");
            m_material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m_material.SetColor("_BaseColor", tint);
            m_material.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * 5f);
            cube.GetComponent<MeshRenderer>().sharedMaterial = m_material;
        }

        private void Update()
        {
            if (m_selected) return;

            m_phase += Time.deltaTime;
            float bob = Mathf.Sin(m_phase * 1.2f) * 0.025f;
            transform.position = m_basePosition + Vector3.up * bob;
            m_visual.Rotate(0f, 60f * Time.deltaTime, 0f, Space.Self);

            if (IsHandTouching())
            {
                m_selected = true;
                m_onSelected?.Invoke();
            }
        }

        private bool IsHandTouching()
        {
            if (m_hands == null) return false;
            float threshold = k_selectDistanceInMeters + k_radiusInMeters;
            float thresholdSqr = threshold * threshold;
            foreach (Transform hand in m_hands)
            {
                if (hand == null) continue;
                if ((hand.position - transform.position).sqrMagnitude <= thresholdSqr) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (m_material != null) Destroy(m_material);
        }
    }
}
