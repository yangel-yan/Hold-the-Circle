using System;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Floating, glowing, hand-grabbable crystal that represents one of the
    /// three specialization branches (Lance / Storm / Aegis) for a specific
    /// hand. The player picks by moving the matching palm into the crystal -
    /// the same embodied VR pickup pattern used for upgrade orbs.
    /// Code-built so the picker needs no prefab or scene wiring.
    /// </summary>
    public class SpecializationCrystal : MonoBehaviour
    {
        private const float k_radiusInMeters = 0.11f;
        private const float k_selectDistanceInMeters = 0.13f;
        private const float k_bobAmplitudeInMeters = 0.04f;
        private const float k_bobFrequency = 1.2f;
        private const float k_spinSpeedInDegrees = 45f;

        private SpecializationSO m_specialization;
        private int m_handIndex;
        private Transform m_targetHand;
        private Action<int, SpecializationSO> m_onSelected;

        public int HandIndex => m_handIndex;

        private Transform m_visual;
        private Material m_material;
        private Vector3 m_basePosition;
        private float m_phase;
        private bool m_selected;

        // Spawn one crystal bound to a specific hand so only that palm can claim it.
        public static SpecializationCrystal Spawn(Vector3 position, SpecializationSO spec,
            int handIndex, Transform targetHand, Action<int, SpecializationSO> onSelected)
        {
            var root = new GameObject($"SpecCrystal_{spec.Branch}_Hand{handIndex}");
            root.transform.position = position;
            var crystal = root.AddComponent<SpecializationCrystal>();
            crystal.Build(spec, handIndex, targetHand, onSelected);
            return crystal;
        }

        private void Build(SpecializationSO spec, int handIndex, Transform targetHand,
            Action<int, SpecializationSO> onSelected)
        {
            m_specialization = spec;
            m_handIndex = handIndex;
            m_targetHand = targetHand;
            m_onSelected = onSelected;
            m_basePosition = transform.position;
            m_phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            // Use a cube rotated 45 degrees on Y to read as a faceted crystal.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            Destroy(cube.GetComponent<Collider>());
            m_visual = cube.transform;
            m_visual.SetParent(transform, false);
            m_visual.localScale = Vector3.one * (k_radiusInMeters * 1.4f);
            m_visual.localRotation = Quaternion.Euler(0f, 45f, 0f);

            Color tint = spec.Tint;
            m_material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m_material.EnableKeyword("_EMISSION");
            m_material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m_material.SetColor("_BaseColor", tint);
            m_material.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * 6f);
            cube.GetComponent<MeshRenderer>().sharedMaterial = m_material;
        }

        private void Update()
        {
            if (m_selected) return;

            m_phase += Time.deltaTime;
            float bob = Mathf.Sin(m_phase * k_bobFrequency) * k_bobAmplitudeInMeters;
            transform.position = m_basePosition + Vector3.up * bob;
            m_visual.Rotate(0f, k_spinSpeedInDegrees * Time.deltaTime, 0f, Space.Self);

            if (IsTargetHandTouching())
            {
                m_selected = true;
                m_onSelected?.Invoke(m_handIndex, m_specialization);
            }
        }

        private bool IsTargetHandTouching()
        {
            if (m_targetHand == null) return false;
            float threshold = k_selectDistanceInMeters + k_radiusInMeters;
            return (m_targetHand.position - transform.position).sqrMagnitude <= threshold * threshold;
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
