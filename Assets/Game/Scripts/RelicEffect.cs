namespace HoldTheCircle
{
    /// <summary>
    /// Runtime instance of a relic. Subscribes to whichever RunEventBus hooks
    /// it cares about on Attach and unsubscribes on Detach so reset between
    /// runs never leaks stale handlers. RelicSO.CreateEffect picks the right
    /// concrete subclass based on the data's RelicKind.
    /// </summary>
    public abstract class RelicEffect
    {
        public RelicSO Source { get; }

        protected RelicEffect(RelicSO source)
        {
            Source = source;
        }

        public abstract void Attach(RunState state);
        public abstract void Detach(RunState state);
    }

    /// <summary>
    /// Stat-add relic: bumps a single multiplier or projectile count once on
    /// pickup. Cleanup is handled by RunState.Reset wiping every HandSlot.
    /// </summary>
    public class SimpleStatRelicEffect : RelicEffect
    {
        public SimpleStatRelicEffect(RelicSO source) : base(source) { }

        public override void Attach(RunState state)
        {
            switch (Source.Kind)
            {
                case RelicKind.FlatDamage:
                    state.AddDamage(Source.Amount);
                    break;
                case RelicKind.FlatFireRate:
                    state.AddFireRate(Source.Amount);
                    break;
                case RelicKind.ExtraProjectile:
                    state.AddProjectiles((int)Source.Amount);
                    break;
            }
        }

        public override void Detach(RunState state)
        {
            switch (Source.Kind)
            {
                case RelicKind.FlatDamage:
                    state.AddDamage(-Source.Amount);
                    break;
                case RelicKind.FlatFireRate:
                    state.AddFireRate(-Source.Amount);
                    break;
                case RelicKind.ExtraProjectile:
                    state.AddProjectiles(-(int)Source.Amount);
                    break;
            }
        }
    }

    /// <summary>
    /// Every kill permanently boosts fire rate by Source.Amount.
    /// </summary>
    public class FireRateOnKillRelicEffect : RelicEffect
    {
        private RunState m_state;

        public FireRateOnKillRelicEffect(RelicSO source) : base(source) { }

        public override void Attach(RunState state)
        {
            m_state = state;
            state.Events.EnemyKilled += HandleEnemyKilled;
        }

        public override void Detach(RunState state)
        {
            state.Events.EnemyKilled -= HandleEnemyKilled;
            m_state = null;
        }

        private void HandleEnemyKilled(EnemyKilledEvent _)
        {
            m_state?.AddFireRate(Source.Amount);
        }
    }

    /// <summary>
    /// Every kill permanently boosts damage by Source.Amount.
    /// </summary>
    public class DamageOnKillRelicEffect : RelicEffect
    {
        private RunState m_state;

        public DamageOnKillRelicEffect(RelicSO source) : base(source) { }

        public override void Attach(RunState state)
        {
            m_state = state;
            state.Events.EnemyKilled += HandleEnemyKilled;
        }

        public override void Detach(RunState state)
        {
            state.Events.EnemyKilled -= HandleEnemyKilled;
            m_state = null;
        }

        private void HandleEnemyKilled(EnemyKilledEvent _)
        {
            m_state?.AddDamage(Source.Amount);
        }
    }

    /// <summary>
    /// Heals Source.Amount HP every Source.SecondaryAmount kills. Useful as a
    /// soft-sustain relic that rewards aggressive play.
    /// </summary>
    public class HealEveryNKillsRelicEffect : RelicEffect
    {
        private RunState m_state;
        private int m_killCounter;
        private readonly int m_threshold;

        public HealEveryNKillsRelicEffect(RelicSO source) : base(source)
        {
            m_threshold = source.SecondaryAmount > 0 ? (int)source.SecondaryAmount : 5;
        }

        public override void Attach(RunState state)
        {
            m_state = state;
            m_killCounter = 0;
            state.Events.EnemyKilled += HandleEnemyKilled;
        }

        public override void Detach(RunState state)
        {
            state.Events.EnemyKilled -= HandleEnemyKilled;
            m_state = null;
        }

        private void HandleEnemyKilled(EnemyKilledEvent _)
        {
            m_killCounter++;
            if (m_killCounter < m_threshold) return;

            m_killCounter = 0;
            m_state?.PlayerHealth?.Heal(Source.Amount);
        }
    }

    /// <summary>
    /// Each time the player is hit, fire rate ramps up by Source.Amount.
    /// Pairs well with risky low-cover loadouts.
    /// </summary>
    public class FireRateOnHurtRelicEffect : RelicEffect
    {
        private RunState m_state;

        public FireRateOnHurtRelicEffect(RelicSO source) : base(source) { }

        public override void Attach(RunState state)
        {
            m_state = state;
            state.Events.DamageTaken += HandleDamageTaken;
        }

        public override void Detach(RunState state)
        {
            state.Events.DamageTaken -= HandleDamageTaken;
            m_state = null;
        }

        private void HandleDamageTaken(DamageTakenEvent _)
        {
            m_state?.AddFireRate(Source.Amount);
        }
    }

    /// <summary>
    /// Every cleared wave permanently boosts damage by Source.Amount.
    /// </summary>
    public class DamageOnWaveClearedRelicEffect : RelicEffect
    {
        private RunState m_state;

        public DamageOnWaveClearedRelicEffect(RelicSO source) : base(source) { }

        public override void Attach(RunState state)
        {
            m_state = state;
            state.Events.WaveCleared += HandleWaveCleared;
        }

        public override void Detach(RunState state)
        {
            state.Events.WaveCleared -= HandleWaveCleared;
            m_state = null;
        }

        private void HandleWaveCleared(int _)
        {
            m_state?.AddDamage(Source.Amount);
        }
    }

    /// <summary>
    /// Every Source.SecondaryAmount kills, grants one extra projectile per
    /// hand. Snowballs hard if the player can keep up the pressure.
    /// </summary>
    public class ProjectileEveryNKillsRelicEffect : RelicEffect
    {
        private RunState m_state;
        private int m_killCounter;
        private readonly int m_threshold;

        public ProjectileEveryNKillsRelicEffect(RelicSO source) : base(source)
        {
            m_threshold = source.SecondaryAmount > 0 ? (int)source.SecondaryAmount : 12;
        }

        public override void Attach(RunState state)
        {
            m_state = state;
            m_killCounter = 0;
            state.Events.EnemyKilled += HandleEnemyKilled;
        }

        public override void Detach(RunState state)
        {
            state.Events.EnemyKilled -= HandleEnemyKilled;
            m_state = null;
        }

        private void HandleEnemyKilled(EnemyKilledEvent _)
        {
            m_killCounter++;
            if (m_killCounter < m_threshold) return;

            m_killCounter = 0;
            m_state?.AddProjectiles(1);
        }
    }

    /// <summary>
    /// One-shot panic boost: the first time the player's HP drops below
    /// Source.SecondaryAmount fraction of max, grants Source.Amount damage
    /// permanently and unsubscribes itself.
    /// </summary>
    public class LowHpDamageOnceRelicEffect : RelicEffect
    {
        private RunState m_state;
        private bool m_triggered;
        private readonly float m_threshold;

        public LowHpDamageOnceRelicEffect(RelicSO source) : base(source)
        {
            m_threshold = source.SecondaryAmount > 0f ? source.SecondaryAmount : 0.3f;
        }

        public override void Attach(RunState state)
        {
            m_state = state;
            m_triggered = false;
            state.Events.DamageTaken += HandleDamageTaken;
        }

        public override void Detach(RunState state)
        {
            state.Events.DamageTaken -= HandleDamageTaken;
            m_state = null;
        }

        private void HandleDamageTaken(DamageTakenEvent e)
        {
            if (m_triggered) return;
            if (e.MaxHealth <= 0f) return;

            float fraction = e.CurrentHealth / e.MaxHealth;
            if (fraction > m_threshold) return;

            m_triggered = true;
            m_state?.AddDamage(Source.Amount);
        }
    }
}
