using TMPro;
using UnityEngine;
using UnityEngine.Pool;

namespace HoldTheCircle
{
    /// <summary>
    /// World-space floating damage number that pops up at hit position, drifts
    /// up, scales out and fades. Built entirely at runtime — no prefab. Uses
    /// TMP world-space text with an outline so it stays readable against the
    /// VST passthrough background. Crit hits read as bigger and gold; combo
    /// kills escalate the size further.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        private const float k_lifetimeInSeconds = 0.65f;
        private const float k_riseDistanceInMeters = 0.28f;
        private const float k_punchDurationInSeconds = 0.18f;
        private const float k_baseFontSize = 48f;
        private const float k_critFontSize = 64f;

        private static readonly Color k_normalTint = new(1f, 1f, 1f, 1f);
        private static readonly Color k_critTint = new(1f, 0.85f, 0.2f, 1f);
        private static readonly Color k_killTint = new(1f, 0.79f, 0.46f, 1f);
        private static ObjectPool<DamageNumber> s_pool;

        private TMP_Text m_text;
        private Transform m_camera;
        private Vector3 m_startPosition;
        private float m_elapsed;
        private float m_lifetime;
        private float m_riseDistance;
        private Vector3 m_horizontalDrift;
        private float m_baseFontSize;

        public static void Spawn(Vector3 position, float damage, bool isCrit, bool wasLethal, int comboTier = 0)
        {
            s_pool ??= new ObjectPool<DamageNumber>(Create,
                d => d.gameObject.SetActive(true), d => d.gameObject.SetActive(false),
                d => { if (d != null) Destroy(d.gameObject); }, false, 32, 64);
            DamageNumber dmg = s_pool.Get();
            dmg.transform.position = position + Vector3.up * 0.18f;
            dmg.Initialize(damage, isCrit, wasLethal, comboTier);
        }

        private static DamageNumber Create()
        {
            var dmg = new GameObject("DamageNumber").AddComponent<DamageNumber>();
            var canvasObj = new GameObject("Canvas");
            canvasObj.transform.SetParent(dmg.transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 120f);
            rect.localScale = Vector3.one * 0.002f;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(canvasObj.transform, false);
            dmg.m_text = textObj.AddComponent<TextMeshProUGUI>();
            dmg.m_text.alignment = TextAlignmentOptions.Center;
            dmg.m_text.fontStyle = FontStyles.Bold;
            dmg.m_text.textWrappingMode = TextWrappingModes.NoWrap;
            dmg.m_text.raycastTarget = false;
            dmg.m_text.fontSharedMaterial = Resources.Load<Material>("Art/RiteNumbers") ?? dmg.m_text.fontSharedMaterial;
            dmg.m_text.rectTransform.sizeDelta = rect.sizeDelta;
            dmg.m_text.rectTransform.anchoredPosition = Vector2.zero;
            return dmg;
        }

        private void Initialize(float damage, bool isCrit, bool wasLethal, int comboTier)
        {
            m_elapsed = 0f;
            transform.localScale = Vector3.one;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            m_horizontalDrift = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.14f;
            m_riseDistance = k_riseDistanceInMeters;
            m_lifetime = k_lifetimeInSeconds;
            m_camera = Camera.main != null ? Camera.main.transform : null;
            m_startPosition = transform.position;
            if (m_camera != null) transform.rotation = Quaternion.LookRotation(transform.position - m_camera.position);
            m_baseFontSize = (isCrit ? k_critFontSize : k_baseFontSize) * (1f + Mathf.Min(comboTier, 3) * 0.08f);
            m_text.fontSize = m_baseFontSize;
            int dmgInt = Mathf.Max(1, Mathf.RoundToInt(damage));
            Color tint;
            if (wasLethal)
            {
                m_text.text = $"<b>{dmgInt}!</b>";
                tint = k_killTint;
            }
            else if (isCrit)
            {
                m_text.text = $"<b>{dmgInt}</b>";
                tint = k_critTint;
            }
            else
            {
                m_text.text = dmgInt.ToString();
                tint = k_normalTint;
            }
            m_text.color = tint;

        }

        private void LateUpdate()
        {
            m_elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(m_elapsed / m_lifetime);

            // Rise + drift outward + ease-out.
            float rise = Mathf.Sqrt(t) * m_riseDistance;
            transform.position = m_startPosition + Vector3.up * rise + m_horizontalDrift * t;

            // Punch scale: 0 -> 1.4 -> 1.0
            float punchT = Mathf.Clamp01(m_elapsed / k_punchDurationInSeconds);
            float punch = punchT < 1f
                ? Mathf.Sin(punchT * Mathf.PI) * 0.4f + Mathf.Lerp(0.6f, 1f, punchT)
                : 1f;

            // Final 30% of life: shrink + fade.
            float settle = Mathf.Clamp01((t - 0.7f) / 0.3f);
            float scale = punch * (1f - settle * 0.5f);
            transform.localScale = Vector3.one * scale;

            if (m_text != null)
            {
                Color c = m_text.color;
                c.a = 1f - settle;
                m_text.color = c;
            }

            // Always face the camera so the number is readable from any angle.
            if (m_camera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - m_camera.position);
            }

            if (t >= 1f)
            {
                s_pool.Release(this);
            }
        }
    }
}
