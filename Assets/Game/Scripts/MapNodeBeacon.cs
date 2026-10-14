using System;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// A floating, color-coded route node the player taps to choose where the
    /// run goes next. Distinct silhouettes per kind (sphere/cube/octa) so the
    /// VR player can read intent without text.
    /// </summary>
    public class MapNodeBeacon : MonoBehaviour
    {
        private const float k_radiusInMeters = 0.07f;
        private const float k_selectDistanceInMeters = 0.1f;
        private const float k_bobAmplitudeInMeters = 0.025f;
        private const float k_bobFrequency = 1.3f;
        private const float k_spinSpeedInDegrees = 90f;

        private MapNode m_node;
        private Transform[] m_hands;
        private Action<MapNode> m_onSelected;

        private Transform m_visual;
        private Material m_material;
        private Vector3 m_basePosition;
        private float m_phase;
        private bool m_selected;

        public static MapNodeBeacon Spawn(MapNode node, Transform[] hands, Action<MapNode> onSelected)
        {
            var root = new GameObject($"MapNode_{node.Kind}_F{node.Floor}C{node.Column}");
            root.transform.position = node.Position;
            var beacon = root.AddComponent<MapNodeBeacon>();
            beacon.Build(node, hands, onSelected);
            return beacon;
        }

        private void Build(MapNode node, Transform[] hands, Action<MapNode> onSelected)
        {
            m_node = node;
            m_hands = hands;
            m_onSelected = onSelected;
            m_basePosition = transform.position;
            m_phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            (PrimitiveType prim, Color tint) = ShapeFor(node.Kind);
            var visual = GameObject.CreatePrimitive(prim);
            visual.name = "Visual";
            Destroy(visual.GetComponent<Collider>());
            m_visual = visual.transform;
            m_visual.SetParent(transform, false);
            m_visual.localScale = Vector3.one * (k_radiusInMeters * 2f);

            if (prim == PrimitiveType.Cube)
            {
                m_visual.localRotation = Quaternion.Euler(35f, 45f, 0f);
            }

            m_material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m_material.EnableKeyword("_EMISSION");
            m_material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m_material.SetColor("_BaseColor", tint);
            m_material.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * 6f);
            visual.GetComponent<MeshRenderer>().sharedMaterial = m_material;
        }

        private static (PrimitiveType, Color) ShapeFor(MapNodeKind kind)
        {
            switch (kind)
            {
                case MapNodeKind.Combat: return (PrimitiveType.Sphere, new Color(1f, 0.45f, 0.35f));
                case MapNodeKind.Elite: return (PrimitiveType.Cube, new Color(1f, 0.25f, 0.55f));
                case MapNodeKind.Treasure: return (PrimitiveType.Cube, new Color(1f, 0.85f, 0.3f));
                case MapNodeKind.Merchant: return (PrimitiveType.Sphere, new Color(0.6f, 0.85f, 1f));
                case MapNodeKind.Event: return (PrimitiveType.Sphere, new Color(0.7f, 1f, 0.6f));
                case MapNodeKind.Boss: return (PrimitiveType.Capsule, new Color(0.95f, 0.25f, 0.85f));
            }
            return (PrimitiveType.Sphere, Color.white);
        }

        private void Update()
        {
            if (m_selected) return;

            m_phase += Time.deltaTime;
            float bob = Mathf.Sin(m_phase * k_bobFrequency) * k_bobAmplitudeInMeters;
            transform.position = m_basePosition + Vector3.up * bob;
            m_visual.Rotate(0f, k_spinSpeedInDegrees * Time.deltaTime, 0f, Space.Self);

            if (IsHandTouching())
            {
                m_selected = true;
                m_onSelected?.Invoke(m_node);
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
