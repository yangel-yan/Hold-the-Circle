using UnityEngine;
using UnityEngine.Rendering;

namespace HoldTheCircle
{
    /// <summary>Rigged PBR presentation with motion synchronized to combat and pool lifetime.</summary>
    public sealed class EnemyVisual : MonoBehaviour
    {
        private static readonly int k_accentId = Shader.PropertyToID("_AccentColor");
        private static readonly int k_flashId = Shader.PropertyToID("_Flash");
        private static readonly int k_threatId = Shader.PropertyToID("_Threat");
        private static readonly int k_dissolveId = Shader.PropertyToID("_Dissolve");
        private Transform m_model;
        private string m_modelRole;
        private Animator m_animator;
        private Renderer[] m_renderers;
        private Transform[] m_groundContacts;
        private static readonly HumanBodyBones[] k_contactBones =
        {
            HumanBodyBones.Head, HumanBodyBones.Hips, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest
        };
        private static readonly float[] k_contactClearance = { 0.16f, 0.18f, 0.30f, 0.30f, 0.16f, 0.16f, 0.22f, 0.28f, 0.25f };
        private MaterialPropertyBlock m_properties;
        private string m_motion;
        private float m_playbackSpeed = 1f;
        private float m_actionUntil;
        private float m_deathAt = -1f;
        public const float DeathDuration = 2.8f;
        public Transform Head => m_animator != null ? m_animator.GetBoneTransform(HumanBodyBones.Head) : null;
        public bool DeathComplete => m_deathAt >= 0f && Time.time - m_deathAt >= DeathDuration;

        public void Configure(EnemyArchetype archetype)
        {
            m_properties ??= new MaterialPropertyBlock();
            string role = archetype == EnemyArchetype.Tank ? "Bulwark" : "Warden";
            if (m_model != null && m_modelRole != role)
            {
                m_model.gameObject.SetActive(false);
                Destroy(m_model.gameObject);
                m_model = null;
            }
            if (m_model == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Art/LanternGuardians/" + role);
                if (prefab == null)
                {
                    Debug.LogError("Missing rigged enemy art. Run Tools/Hold The Circle/Build Animated Enemies.");
                    return;
                }
                m_model = Instantiate(prefab, transform).transform;
                m_modelRole = role;
                m_renderers = m_model.GetComponentsInChildren<Renderer>();
                m_animator = m_model.GetComponentInChildren<Animator>();
                m_groundContacts = new Transform[k_contactBones.Length];
                for (int i = 0; i < k_contactBones.Length; i++)
                    m_groundContacts[i] = m_animator.GetBoneTransform(k_contactBones[i]);
                foreach (Renderer renderer in m_renderers)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            Material material = Resources.Load<Material>("Art/LanternGuardians/" + role);
            foreach (Renderer renderer in m_renderers) renderer.sharedMaterial = material;
            // Character is authored at human scale. Combat silhouettes remain below the player's face.
            m_model.localScale = archetype switch
            {
                EnemyArchetype.Charger => new Vector3(0.72f, 0.78f, 0.72f),
                EnemyArchetype.Tank => new Vector3(0.95f, 0.87f, 0.95f),
                EnemyArchetype.Weaver => new Vector3(0.73f, 0.9f, 0.73f),
                _ => Vector3.one * 0.74f
            };
            m_model.localPosition = Vector3.zero;
            m_model.localRotation = Quaternion.identity;
            Color accent = archetype switch
            {
                EnemyArchetype.Charger => new Color(1f, 0.46f, 0.16f),
                EnemyArchetype.Tank => new Color(0.68f, 0.53f, 1f),
                EnemyArchetype.Weaver => new Color(0.28f, 0.95f, 0.8f),
                _ => new Color(0.58f, 0.78f, 1f)
            };
            m_properties.SetColor(k_accentId, accent);
            m_deathAt = -1f;
            m_actionUntil = 0f;
            m_animator.Rebind();
            m_animator.Update(0f);
            m_motion = null;
            SetMotion("Idle", 1f, Random.value);
            Present(0f, 0f);
        }

        public void Present(float flash, float threat, float speed = 0f, bool frozen = false)
        {
            if (m_model == null) return;
            if (m_deathAt < 0f && Time.time >= m_actionUntil)
            {
                string motion = speed < 0.08f ? "Idle" : speed < 1.5f ? "Walk" : "Run";
                SetMotion(motion, motion == "Idle" ? 1f : Mathf.Clamp(speed / (motion == "Walk" ? 1.1f : 2.3f), 0.55f, 1.6f));
            }
            m_animator.speed = frozen ? 0f : m_playbackSpeed;
            m_properties.SetFloat(k_flashId, flash);
            m_properties.SetFloat(k_threatId, threat);
            m_properties.SetFloat(k_dissolveId, m_deathAt < 0f ? 0f :
                Mathf.InverseLerp(DeathDuration - 0.5f, DeathDuration, Time.time - m_deathAt));
            foreach (Renderer renderer in m_renderers) renderer.SetPropertyBlock(m_properties);
        }

        public void WindUp(float seconds)
        {
            if (m_deathAt >= 0f || m_animator == null) return;
            m_actionUntil = Time.time + seconds * 1.7f;
            m_motion = "Attack";
            m_playbackSpeed = m_animator.speed = Mathf.Max(0.1f, 1.55f / seconds);
            m_animator.CrossFadeInFixedTime("Attack", 0.09f, 0, 0.4f);
        }

        public void Die()
        {
            if (m_animator == null || m_deathAt >= 0f) return;
            m_deathAt = Time.time;
            m_motion = "Death";
            m_playbackSpeed = m_animator.speed = 1.75f;
            m_animator.CrossFadeInFixedTime("Death", 0.055f, 0, 0f);
        }

        private void LateUpdate()
        {
            if (m_groundContacts == null) return;
            // Retargeted boots and fingers can extend below the source character's floor.
            // Correct the rendered body after animation without moving its gameplay anchor.
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < m_groundContacts.Length; i++)
            {
                if (m_deathAt < 0f && i != 2 && i != 3) continue;
                if (m_groundContacts[i] != null)
                {
                    // The heavy lantern projects forward from the breastplate.
                    // Include it when the body falls face-first.
                    float clearance = m_modelRole == "Bulwark" && i >= 6 ? 0.88f : k_contactClearance[i];
                    lowest = Mathf.Min(lowest, m_groundContacts[i].position.y -
                        clearance * m_model.lossyScale.y);
                }
            }
            float scale = Mathf.Max(0.0001f, transform.lossyScale.y);
            float unlifted = lowest - m_model.localPosition.y * scale;
            float lift = Mathf.Max(0f, transform.position.y + 0.008f * scale - unlifted) / scale;
            m_model.localPosition = Vector3.up * lift;
        }

        private void SetMotion(string motion, float speed, float phase = 0f)
        {
            m_playbackSpeed = m_animator.speed = speed;
            if (m_motion == motion) return;
            m_motion = motion;
            m_animator.CrossFadeInFixedTime(motion, 0.12f, 0, phase);
        }

        public void SetAccent(Color accent)
        {
            m_properties.SetColor(k_accentId, accent);
        }
    }
}
