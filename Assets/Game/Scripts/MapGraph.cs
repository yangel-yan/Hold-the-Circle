using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// One node on the procedural route map. Holds the kind that drives what
    /// happens after selection plus a small list of children pointing forward.
    /// </summary>
    public class MapNode
    {
        public int Floor;
        public int Column;
        public MapNodeKind Kind;
        public Vector3 Position;
        public readonly List<MapNode> Next = new();
        public bool Visited;
    }

    /// <summary>
    /// Slay-the-Spire style fork map: a small lattice of nodes the player picks
    /// through one floor at a time. Pure data — the picker layer turns each
    /// floor's choices into floating holograms.
    /// </summary>
    public class MapGraph
    {
        private const int k_floors = 6;
        private const int k_columns = 3;

        public int FloorCount => k_floors;
        public int CurrentFloor { get; private set; }
        public IReadOnlyList<MapNode> AllNodes => m_allNodes;
        public IReadOnlyList<List<MapNode>> Floors => m_floors;

        private readonly List<List<MapNode>> m_floors = new();
        private readonly List<MapNode> m_allNodes = new();
        private MapNode m_lastNode;

        public void Generate(Vector3 origin, Vector3 forward)
        {
            m_floors.Clear();
            m_allNodes.Clear();
            CurrentFloor = 0;
            m_lastNode = null;

            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            for (int f = 0; f < k_floors; f++)
            {
                var floor = new List<MapNode>();
                int columns = ColumnsForFloor(f);
                for (int c = 0; c < columns; c++)
                {
                    var node = new MapNode
                    {
                        Floor = f,
                        Column = c,
                        Kind = PickKindForFloor(f, c),
                        Position = origin
                            + forward * (1.1f + f * 0.18f)
                            + right * ((c - (columns - 1) * 0.5f) * 0.32f)
                            + Vector3.up * (0.05f + f * 0.05f),
                    };
                    floor.Add(node);
                    m_allNodes.Add(node);
                }
                m_floors.Add(floor);
            }

            // Wire each floor's nodes to one or two children on the next floor.
            for (int f = 0; f < k_floors - 1; f++)
            {
                var current = m_floors[f];
                var next = m_floors[f + 1];
                for (int i = 0; i < current.Count; i++)
                {
                    int target = Mathf.Clamp(Mathf.RoundToInt((float)i / Mathf.Max(1, current.Count - 1) * (next.Count - 1)), 0, next.Count - 1);
                    current[i].Next.Add(next[target]);
                    int second = Mathf.Clamp(target + (UnityEngine.Random.value < 0.5f ? -1 : 1), 0, next.Count - 1);
                    if (second != target) current[i].Next.Add(next[second]);
                }
            }

            // Force final floor to be the act boss (Phase 1 capstone fight).
            foreach (MapNode boss in m_floors[k_floors - 1]) boss.Kind = MapNodeKind.Boss;
        }

        public List<MapNode> GetCurrentChoices()
        {
            if (CurrentFloor >= m_floors.Count) return null;
            return m_lastNode != null ? m_lastNode.Next : m_floors[CurrentFloor];
        }

        public bool IsLastFloor()
        {
            return CurrentFloor >= m_floors.Count - 1;
        }

        public void AdvanceTo(MapNode node)
        {
            if (node == null || node.Floor != CurrentFloor) return;
            List<MapNode> choices = GetCurrentChoices();
            if (choices == null || !choices.Contains(node)) return;
            node.Visited = true;
            m_lastNode = node;
            CurrentFloor = node.Floor + 1;
        }

        private static int ColumnsForFloor(int floor)
        {
            if (floor == 0) return 2;
            if (floor == k_floors - 1) return 1;
            return k_columns;
        }

        // Pre-baked kind weights so the route stays varied but predictable.
        private static MapNodeKind PickKindForFloor(int floor, int column)
        {
            float roll = UnityEngine.Random.value;
            if (floor == 0) return MapNodeKind.Combat;
            if (floor == 2 && column == 1) return MapNodeKind.Treasure;
            if (floor == 3 && column == 0) return MapNodeKind.Elite;
            if (floor == 4 && column == 2) return MapNodeKind.Merchant;
            if (roll < 0.55f) return MapNodeKind.Combat;
            if (roll < 0.7f) return MapNodeKind.Elite;
            if (roll < 0.82f) return MapNodeKind.Treasure;
            if (roll < 0.92f) return MapNodeKind.Merchant;
            return MapNodeKind.Event;
        }
    }
}
