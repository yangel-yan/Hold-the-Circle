using System;
using UnityEngine;

namespace HoldTheCircle
{
    public enum UpgradeKind
    {
        FireRate,
        Damage,
        ExtraProjectile
    }

    /// <summary>
    /// A floating, glowing, grabbable upgrade choice shown after a wave is cleared.
    /// The player selects it by moving a hand (palm) into it - an embodied VR pick
    /// rather than a 2D button. Code-built so it needs no prefab or scene wiring.
    /// </summary>
    public class UpgradeOrb : MonoBehaviour
    {
        private const float k_orbRadiusInMeters = 0.09f;
        private const float k_selectDistanceInMeters = 0.12f;
        private const float k_bobAmplitudeInMeters = 0.03f;
        private const float k_bobFrequency = 1.5f;
        private const float k_spinSpeedInDegrees = 60f;

        private UpgradeKind m_kind;
        private Transform[] m_hands;
        private Action<UpgradeKind> m_onSelected;

        private Transform m_visual;
        private Material m_material;
        private Color m_tint;
        private Vector3 m_basePosition;
        private float m_phase;
        private bool m_selected;

        public static UpgradeOrb Spawn(Vector3 position, UpgradeKind kind, Color tint,
            Transform[] hands, Action<UpgradeKind> onSelected)
        {
            var root = new GameObject($"UpgradeOrb_{kind}");
            root.transform.position = position;
            var orb = root.AddComponent<UpgradeOrb>();
            orb.Build(kind, tint, hands, onSelected);
            return orb;
        }

        private void Build(UpgradeKind kind, Color tint, Transform[] hands, Action<UpgradeKind> onSelected)
        {
            m_kind = kind;
            m_tint = tint;
            m_hands = hands;
            m_onSelected = onSelected;
            m_basePosition = transform.position;
            m_phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Visual";
            Destroy(sphere.GetComponent<Collider>());
            m_visual = sphere.transform;
            m_visual.SetParent(transform, false);
            m_visual.localScale = Vector3.one * (k_orbRadiusInMeters * 2f);

            m_material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m_material.EnableKeyword("_EMISSION");
            m_material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m_material.SetColor("_BaseColor", tint);
            m_material.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b) * 5f);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = m_material;
        }

        private void Update()
        {
            if (m_selected) return;

            // Idle bob + spin to read as an interactable pickup.
            m_phase += Time.deltaTime;
            float bob = Mathf.Sin((m_phase * k_bobFrequency) + 0f) * k_bobAmplitudeInMeters;
            transform.position = m_basePosition + Vector3.up * bob;
            m_visual.Rotate(0f, k_spinSpeedInDegrees * Time.deltaTime, 0f, Space.Self);

            if (IsHandTouching())
            {
                m_selected = true;
                m_onSelected?.Invoke(m_kind);
            }
        }

        private bool IsHandTouching()
        {
            if (m_hands == null) return false;

            float threshold = k_selectDistanceInMeters + k_orbRadiusInMeters;
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
