using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Global hitstop / time-freeze controller. Brief slowdowns on impactful
    /// events (kill, crit, boss damage) make every blow feel weighty.
    /// VR-safe: hitstop is short (40-90ms) and uses Time.timeScale dampening
    /// rather than full freeze so head tracking stays smooth and continuous.
    /// </summary>
    public class HitstopManager : MonoBehaviour
    {
        private static HitstopManager s_instance;

        private float m_remaining;
        private float m_targetScale;
        private bool m_paused;
        public static bool IsPaused => s_instance != null && s_instance.m_paused;

        public static void SetPaused(bool paused)
        {
            Instance.m_paused = paused;
            Instance.m_remaining = 0f;
            Time.timeScale = paused ? 0f : 1f;
        }

        public static HitstopManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    var go = new GameObject("HitstopManager");
                    s_instance = go.AddComponent<HitstopManager>();
                    DontDestroyOnLoad(go);
                }
                return s_instance;
            }
        }

        // Quick dampening for normal hits.
        public static void Hit() => Instance.Trigger(0.04f, 0.55f);

        // Slightly heavier for crits — reads as "extra impact".
        public static void Crit() => Instance.Trigger(0.06f, 0.4f);

        // Big punch for kills + screen-clear moments.
        public static void Kill() => Instance.Trigger(0.09f, 0.25f);

        // Major moments: boss phase change, boss kill.
        public static void Cinematic(float duration = 0.18f, float scale = 0.15f) =>
            Instance.Trigger(duration, scale);

        private void Trigger(float duration, float scale)
        {
            if (m_paused || Time.timeScale <= 0f) return;
            // Always take the strongest pending hitstop so a kill during a
            // pile of regular hits still reads as the climactic moment.
            if (duration > m_remaining)
            {
                m_remaining = duration;
                m_targetScale = scale;
                Time.timeScale = scale;
            }
        }

        private void Update()
        {
            if (m_paused || m_remaining <= 0f) return;

            // Hitstop runs in unscaled time so it actually finishes even when
            // Time.timeScale is heavily slowed.
            m_remaining -= Time.unscaledDeltaTime;
            if (m_remaining <= 0f)
            {
                m_remaining = 0f;
                Time.timeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                Time.timeScale = 1f;
                s_instance = null;
            }
        }
    }
}
