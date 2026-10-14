using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Per-arena ambient/BGM driver. Builds an AudioListener-friendly soundscape
    /// using procedurally generated AudioClips so no asset wiring is needed:
    ///   - Looping low drone bed for the Frostbite Cloister mood.
    ///   - Sub bass pulse for combat tension (kicked on each WaveStarted).
    ///   - Spatialized one-shots for enemy hits / kills / wave fired.
    ///
    /// Subscribes to the run-wide event bus on Bind() so any future audio reactor
    /// (relics, specs) can plug in alongside without touching combat code.
    /// </summary>
    public class ArenaAudioSystem : MonoBehaviour
    {
        private const int k_sampleRate = 22050;

        private RunState m_runState;
        private Transform m_player;

        private AudioSource m_bgmSource;
        private AudioSource m_pulseSource;
        private AudioClip m_bgmClip;
        private AudioClip m_pulseClip;
        private AudioClip m_hitClip;
        private AudioClip m_killClip;
        private AudioClip m_castClip;
        private AudioClip m_hurtClip;
        private AudioClip m_comboClip;

        public void Initialize(RunState runState, Transform player)
        {
            m_runState = runState;
            m_player = player;

            BuildClips();
            BuildBgmSource();
            BuildPulseSource();

            if (m_runState != null)
            {
                m_runState.Events.EnemyHit += HandleEnemyHit;
                m_runState.Events.EnemyKilled += HandleEnemyKilled;
                m_runState.Events.WaveFired += HandleWaveFired;
                m_runState.Events.WaveCleared += HandleWaveCleared;
                m_runState.Events.DamageTaken += HandleDamageTaken;
                m_runState.Events.ComboChanged += HandleComboChanged;
            }
        }

        private void OnDestroy()
        {
            if (m_runState != null)
            {
                m_runState.Events.EnemyHit -= HandleEnemyHit;
                m_runState.Events.EnemyKilled -= HandleEnemyKilled;
                m_runState.Events.WaveFired -= HandleWaveFired;
                m_runState.Events.WaveCleared -= HandleWaveCleared;
                m_runState.Events.DamageTaken -= HandleDamageTaken;
                m_runState.Events.ComboChanged -= HandleComboChanged;
            }
        }

        // Re-trigger the combat pulse when a node combat starts. Called from
        // GameDirector when entering Elite/Combat layouts.
        public void OnCombatStarted()
        {
            if (m_pulseSource != null && !m_pulseSource.isPlaying)
            {
                m_pulseSource.Play();
            }
        }

        public void OnCombatEnded()
        {
            if (m_pulseSource != null) m_pulseSource.Stop();
        }

        private void BuildBgmSource()
        {
            var bgmObj = new GameObject("ArenaBgm");
            bgmObj.transform.SetParent(transform, false);
            m_bgmSource = bgmObj.AddComponent<AudioSource>();
            m_bgmSource.clip = m_bgmClip;
            m_bgmSource.loop = true;
            m_bgmSource.volume = 0.18f;
            m_bgmSource.spatialBlend = 0f; // 2D ambient bed
            m_bgmSource.playOnAwake = false;
            m_bgmSource.Play();
        }

        private void BuildPulseSource()
        {
            var pulseObj = new GameObject("ArenaPulse");
            pulseObj.transform.SetParent(transform, false);
            m_pulseSource = pulseObj.AddComponent<AudioSource>();
            m_pulseSource.clip = m_pulseClip;
            m_pulseSource.loop = true;
            m_pulseSource.volume = 0.22f;
            m_pulseSource.spatialBlend = 0f;
            m_pulseSource.playOnAwake = false;
        }

        private void BuildClips()
        {
            m_bgmClip = SynthesizeDrone(durationInSeconds: 8f, baseFrequency: 110f, name: "FrostbiteDrone");
            m_pulseClip = SynthesizePulse(durationInSeconds: 1.6f, baseFrequency: 55f, name: "CombatPulse");
            // Hit / kill are now percussive impacts (tone + noise crack + pitch
            // drop) instead of clean sine blips, so they punch through the mix
            // and read as "struck something solid".
            m_hitClip = SynthesizeImpact(durationInSeconds: 0.16f, startFrequency: 900f, endFrequency: 380f,
                noiseAmount: 0.5f, name: "Hit");
            m_killClip = SynthesizeImpact(durationInSeconds: 0.42f, startFrequency: 1300f, endFrequency: 180f,
                noiseAmount: 0.65f, name: "Kill");
            m_castClip = SynthesizeBlip(durationInSeconds: 0.18f, frequency: 660f, name: "Cast");
            // Player hurt: low-frequency body-thud with noise, no bright tone,
            // so damage feels heavy and threatening from the headset center.
            m_hurtClip = SynthesizeImpact(durationInSeconds: 0.45f, startFrequency: 220f, endFrequency: 70f,
                noiseAmount: 0.8f, name: "Hurt");
            // Clean bell-ish tone for combo escalation; pitched up per step.
            m_comboClip = SynthesizeBlip(durationInSeconds: 0.22f, frequency: 520f, name: "Combo");
        }

        // --- procedural synthesis -------------------------------------------------

        // Stacked detuned sines + slow LFO for an icy ambient drone.
        private static AudioClip SynthesizeDrone(float durationInSeconds, float baseFrequency, string name)
        {
            int sampleCount = Mathf.RoundToInt(durationInSeconds * k_sampleRate);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / k_sampleRate;
                float lfo = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 0.07f);
                float partials =
                    Mathf.Sin(t * Mathf.PI * 2f * baseFrequency) * 0.45f +
                    Mathf.Sin(t * Mathf.PI * 2f * baseFrequency * 1.5f) * 0.22f +
                    Mathf.Sin(t * Mathf.PI * 2f * baseFrequency * 0.5f) * 0.18f;
                data[i] = partials * (0.65f + 0.35f * lfo);
            }

            var clip = AudioClip.Create(name, sampleCount, 1, k_sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Slow heartbeat-style envelope for combat tension.
        private static AudioClip SynthesizePulse(float durationInSeconds, float baseFrequency, string name)
        {
            int sampleCount = Mathf.RoundToInt(durationInSeconds * k_sampleRate);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / k_sampleRate;
                float beatPhase = t / durationInSeconds;
                float envelope = Mathf.Pow(1f - beatPhase, 4f);
                if (beatPhase > 0.45f && beatPhase < 0.6f)
                {
                    envelope += Mathf.Pow(1f - (beatPhase - 0.45f) / 0.15f, 4f) * 0.6f;
                }
                data[i] = Mathf.Sin(t * Mathf.PI * 2f * baseFrequency) * envelope * 0.6f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, k_sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Short percussive blip with exponential decay for hit / kill / cast feedback.
        private static AudioClip SynthesizeBlip(float durationInSeconds, float frequency, string name)
        {
            int sampleCount = Mathf.RoundToInt(durationInSeconds * k_sampleRate);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / k_sampleRate;
                float envelope = Mathf.Exp(-t * 18f);
                data[i] = Mathf.Sin(t * Mathf.PI * 2f * frequency) * envelope * 0.7f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, k_sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Percussive impact: a tone that drops in pitch over its (short) life,
        // blended with a burst of decaying white noise. The noise "crack" plus
        // the downward pitch sweep is what sells a physical strike vs a beep.
        private static AudioClip SynthesizeImpact(
            float durationInSeconds, float startFrequency, float endFrequency, float noiseAmount, string name)
        {
            int sampleCount = Mathf.RoundToInt(durationInSeconds * k_sampleRate);
            float[] data = new float[sampleCount];
            // Deterministic noise so clips are identical between builds.
            var random = new System.Random(name.GetHashCode());
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / k_sampleRate;
                float progress = (float)i / sampleCount;

                // Sharp attack, exponential tail.
                float envelope = Mathf.Exp(-t * 14f);

                // Glide the oscillator from start -> end frequency. Integrate
                // the instantaneous frequency to keep the phase continuous.
                float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                phase += frequency / k_sampleRate;
                float tone = Mathf.Sin(phase * Mathf.PI * 2f);

                // Noise burst that fades faster than the tone for a "crack".
                float noise = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 32f);

                float sample = Mathf.Lerp(tone, noise, noiseAmount * 0.5f) + noise * noiseAmount * 0.5f;
                data[i] = Mathf.Clamp(sample * envelope * 0.8f, -1f, 1f);
            }

            var clip = AudioClip.Create(name, sampleCount, 1, k_sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // --- one-shot helpers -----------------------------------------------------

        // Spawn a temporary spatialized AudioSource at the world position.
        // Auto-destroys when the clip is finished. Pitch lets callers shift a
        // shared clip up/down (combo escalation, crit ping) without new assets.
        private static void PlaySpatial(AudioClip clip, Vector3 position, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("AudioOneShot");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 1f;        // full 3D
            src.minDistance = 1f;
            src.maxDistance = 18f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            // Clip length scales inversely with pitch, so account for it when
            // scheduling the cleanup so faster-pitched clips aren't cut short.
            Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
        }

        private void HandleEnemyHit(EnemyHitEvent e)
        {
            // Crits hit louder and a touch higher so they cut through a busy
            // mix as a distinct "ping!".
            if (e.IsCrit)
            {
                PlaySpatial(m_killClip, e.Position, 0.7f, 1.25f);
            }
            else
            {
                PlaySpatial(m_hitClip, e.Position, 0.45f);
            }
        }

        private void HandleEnemyKilled(EnemyKilledEvent e)
        {
            PlaySpatial(m_killClip, e.Position, 0.6f);
        }

        // Subtle 3D blip at the firing hand so the player hears each cast in space.
        private void HandleWaveFired(WaveFiredEvent e)
        {
            PlaySpatial(m_castClip, e.Origin, 0.25f);
        }

        private void HandleWaveCleared(int waveIndex)
        {
            // Soft confirmation cue near the player when a wave clears.
            if (m_player != null)
            {
                PlaySpatial(m_killClip, m_player.position + Vector3.up * 1.4f, 0.35f);
            }
        }

        // Low body-thud at the headset when the player takes damage. Played
        // close (2D-ish, at head height) so it reads as "I got hit", not as a
        // distant world sound.
        private void HandleDamageTaken(DamageTakenEvent e)
        {
            if (m_player != null)
            {
                PlaySpatial(m_hurtClip, m_player.position + Vector3.up * 1.5f, 0.7f);
            }
        }

        // Rising-pitch chime as the kill streak climbs: each combo step nudges
        // the pitch up, capped so it never gets shrill. combo == 0 is the reset
        // and stays silent. Played at the player so the streak feedback is
        // centered rather than coming from a random world point.
        private void HandleComboChanged(int combo)
        {
            if (combo < 2 || m_player == null) return;

            float pitch = Mathf.Min(1f + (combo - 1) * 0.08f, 2.2f);
            // Milestones (5 / 10 / 20) punch louder for a clear "tier up" cue.
            bool milestone = combo == 5 || combo == 10 || combo == 20;
            float volume = milestone ? 0.6f : 0.35f;
            PlaySpatial(m_comboClip, m_player.position + Vector3.up * 1.6f, volume, pitch);
        }
    }
}
