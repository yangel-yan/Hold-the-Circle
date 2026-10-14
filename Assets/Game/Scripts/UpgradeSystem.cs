using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Presents three randomized, grabbable upgrade orbs in front of the player
    /// after each cleared wave. When the player touches one, applies it to the
    /// shared RunState and raises UpgradePicked so the director can continue.
    /// Code-built; no prefab or scene wiring required.
    /// </summary>
    public class UpgradeSystem : MonoBehaviour
    {
        private const int k_choiceCount = 3;
        private const float k_distanceInMeters = 0.9f;
        private const float k_heightInMeters = -0.1f;
        private const float k_spreadInMeters = 0.32f;

        private RunState m_runState;
        private Transform m_player;
        private Transform[] m_hands;

        private readonly List<UpgradeOrb> m_activeOrbs = new(k_choiceCount);

        // Raised once the player has touched one of the choices.
        public event Action UpgradePicked;

        public void Initialize(RunState runState, Transform player, Transform[] hands)
        {
            m_runState = runState;
            m_player = player;
            m_hands = hands;
        }

        // Spawn a fresh set of three distinct upgrade choices.
        public void PresentChoices()
        {
            ClearOrbs();

            List<UpgradeKind> pool = new() { UpgradeKind.FireRate, UpgradeKind.Damage, UpgradeKind.ExtraProjectile };

            Vector3 origin = m_player != null ? m_player.position : Vector3.zero;
            Vector3 forward = m_player != null ? m_player.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            for (int i = 0; i < k_choiceCount && pool.Count > 0; i++)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                UpgradeKind kind = pool[index];
                pool.RemoveAt(index);

                float offset = (i - (k_choiceCount - 1) * 0.5f) * k_spreadInMeters;
                Vector3 position = origin
                    + forward * k_distanceInMeters
                    + right * offset
                    + Vector3.up * k_heightInMeters;

                UpgradeOrb orb = UpgradeOrb.Spawn(position, kind, GetTint(kind), m_hands, HandleOrbSelected);
                m_activeOrbs.Add(orb);
            }
        }

        private void HandleOrbSelected(UpgradeKind kind)
        {
            ApplyUpgrade(kind);
            ClearOrbs();
            UpgradePicked?.Invoke();
        }

        private void ApplyUpgrade(UpgradeKind kind)
        {
            if (m_runState == null) return;

            switch (kind)
            {
                case UpgradeKind.FireRate:
                    m_runState.AddFireRate(0.35f);
                    break;
                case UpgradeKind.Damage:
                    m_runState.AddDamage(0.5f);
                    break;
                case UpgradeKind.ExtraProjectile:
                    m_runState.AddProjectiles(1);
                    break;
            }
        }

        private static Color GetTint(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.FireRate:
                    return new Color(0.4f, 1f, 0.6f, 1f); // green
                case UpgradeKind.Damage:
                    return new Color(1f, 0.5f, 0.3f, 1f); // orange
                default:
                    return new Color(0.6f, 0.7f, 1f, 1f); // blue
            }
        }

        private void ClearOrbs()
        {
            foreach (UpgradeOrb orb in m_activeOrbs)
            {
                if (orb != null) Destroy(orb.gameObject);
            }

            m_activeOrbs.Clear();
        }

        private void OnDestroy()
        {
            ClearOrbs();
        }
    }
}
