using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Owns the route MapGraph and turns each floor's choices into floating
    /// MapNodeBeacon holograms the player taps in VR. Raises NodeSelected once
    /// the player commits to one branch. The director listens and dispatches
    /// per-kind behavior (combat, elite, treasure, merchant, event).
    /// </summary>
    public class MapSystem : MonoBehaviour
    {
        private RunState m_runState;
        private Transform m_player;
        private Transform[] m_hands;

        private readonly MapGraph m_graph = new();
        private readonly List<MapNodeBeacon> m_activeBeacons = new(4);

        // The committed node so the director can dispatch on its kind.
        public event Action<MapNode> NodeSelected;

        public MapGraph Graph => m_graph;
        public bool IsExhausted => m_graph.CurrentFloor >= m_graph.FloorCount;

        public void Initialize(RunState runState, Transform player, Transform[] hands)
        {
            m_runState = runState;
            m_player = player;
            m_hands = hands;
        }

        // Generates a brand-new lattice for a fresh run. Existing beacons are
        // cleared so a restart never leaves stale holograms behind.
        public void GenerateNewMap()
        {
            ClearBeacons();
            Vector3 origin = m_player != null ? m_player.position : Vector3.zero;
            Vector3 forward = m_player != null ? m_player.forward : Vector3.forward;
            m_graph.Generate(origin, forward);
        }

        // Spawns one beacon per choice on the current floor. The director calls
        // this after a node has resolved (combat cleared, treasure picked, etc.)
        // to ask the player where to go next.
        public void PresentCurrentFloor()
        {
            ClearBeacons();
            List<MapNode> choices = m_graph.GetCurrentChoices();
            if (choices == null || choices.Count == 0) return;

            var options = new WorldMenu.Choice[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                MapNode node = choices[i];
                string detail = node.Kind switch
                {
                    MapNodeKind.Combat => "Hold the seal\nChoose a relic on victory",
                    MapNodeKind.Elite => "Stronger invasion\nRelic + bonus shards",
                    MapNodeKind.Treasure => "Claim a free relic",
                    MapNodeKind.Merchant => "Spend shards\nPurchase or remove a relic",
                    MapNodeKind.Event => "A pact with the lantern\nChoose risk or recovery",
                    _ => "Twin lantern sentinels\nFinal encounter"
                };
                options[i] = new WorldMenu.Choice(node.Kind.ToString().ToUpperInvariant(),
                    detail, () => HandleBeaconSelected(node));
            }
            WorldMenu.Show(m_player, m_hands, $"THE PATH  {m_graph.CurrentFloor + 1} / {m_graph.FloorCount}",
                "Choose a connected route. Your next encounter begins after confirmation.", options);
        }

        public void ResetForNewRun()
        {
            ClearBeacons();
        }

        private void HandleBeaconSelected(MapNode node)
        {
            if (!m_graph.GetCurrentChoices().Contains(node)) return;
            m_graph.AdvanceTo(node);
            ClearBeacons();
            NodeSelected?.Invoke(node);
        }

        private void ClearBeacons()
        {
            foreach (MapNodeBeacon beacon in m_activeBeacons)
            {
                if (beacon != null) Destroy(beacon.gameObject);
            }
            m_activeBeacons.Clear();
        }

        private void OnDestroy()
        {
            ClearBeacons();
        }
    }
}
