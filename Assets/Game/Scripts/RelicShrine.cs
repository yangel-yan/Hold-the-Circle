using System;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// A floating, glowing relic offering. Player picks one by moving a hand
    /// into it. Visualized as a tall tetrahedron-feeling shape (octahedron via
    /// rotated cube) so it reads as "treasure" rather than "stat orb". Code
    /// built; no prefab/scene wiring.
    /// </summary>
    public class RelicShrine : MonoBehaviour
    {
        private const float k_radiusInMeters = 0.1f;
        private const float k_selectDistanceInMeters = 0.13f;
        private const float k_bobAmplitudeInMeters = 0.035f;
        private const float k_bobFrequency = 1.1f;
        private const float k_spinSpeedInDegrees = 70f;

        private RelicSO m_relic;
        private Transform[] m_hands;
        private Action<RelicSO> m_onSelected;

        private Transform m_visual;
        private Material m_material;
        private Vector3 m_basePosition;
        private float m_phase;
        private bool m_selected;

        public static RelicShrine Spawn(Vector3 position, RelicSO relic, Transform[] hands,
            Action<RelicSO> onSelected)
        {
            var root = new GameObject($"RelicShrine_{relic.DisplayName}");
            root.transform.position = position;
            var shrine = root.AddComponent<RelicShrine>();
            shrine.Build(relic, hands, onSelected);
            return shrine;
        }

        private void Build(RelicSO relic, Transform[] hands, Action<RelicSO> onSelected)
        {
            m_relic = relic;
            m_hands = hands;
            m_onSelected = onSelected;
            m_basePosition = transform.position;
            m_phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            // Diamond / gem shape: cube tilted on two axes plus stretched height.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            Destroy(cube.GetComponent<Collider>());
            m_visual = cube.transform;
            m_visual.SetParent(transform, false);
            m_visual.localScale = new Vector3(k_radiusInMeters * 1.2f, k_radiusInMeters * 1.6f, k_radiusInMeters * 1.2f);
            m_visual.localRotation = Quaternion.Euler(45f, 45f, 0f);

            Color tint = relic.Tint;
            m_material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m_material.EnableKeyword("_EMISSION");
            m_material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m_material.SetColor("_BaseColor", tint);
            m_material.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * 7f);
            cube.GetComponent<MeshRenderer>().sharedMaterial = m_material;
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
                m_onSelected?.Invoke(m_relic);
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
                if ((hand.position - transform.position).sqrMagnitude <= thresholdSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (m_material != null)
            {
                Destroy(m_material);
            }
        }
    }
}
