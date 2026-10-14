using System;
using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Centralized in-run event hub. Specializations and relics subscribe here
    /// instead of patching combat code, so new content layers cleanly on top.
    ///
    /// Typical order during one shot:
    ///   WaveFired -> (impact) EnemyHit -> (lethal) EnemyKilled -> WaveCleared.
    /// DamageTaken fires whenever the player loses HP, independent of waves.
    /// </summary>
    public class RunEventBus
    {
        public event Action<WaveFiredEvent> WaveFired;
        public event Action<CombatActionEvent> CombatActionPerformed;
        public event Action<EnemyHitEvent> EnemyHit;
        public event Action<EnemyKilledEvent> EnemyKilled;
        public event Action<int> WaveCleared;
        public event Action<DamageTakenEvent> DamageTaken;
        // Current kill-streak count; 0 means the streak just reset/expired.
        public event Action<int> ComboChanged;

        public void RaiseWaveFired(WaveFiredEvent e) => WaveFired?.Invoke(e);
        public void RaiseCombatAction(CombatActionEvent e) => CombatActionPerformed?.Invoke(e);
        public void RaiseEnemyHit(EnemyHitEvent e) => EnemyHit?.Invoke(e);
        public void RaiseEnemyKilled(EnemyKilledEvent e) => EnemyKilled?.Invoke(e);
        public void RaiseWaveCleared(int waveIndex) => WaveCleared?.Invoke(waveIndex);
        public void RaiseDamageTaken(DamageTakenEvent e) => DamageTaken?.Invoke(e);
        public void RaiseComboChanged(int combo) => ComboChanged?.Invoke(combo);
    }

    public enum CombatActionKind
    {
        SlashWave,
        ChainLightning
    }

    public struct CombatActionEvent
    {
        public int HandIndex;
        public CombatActionKind Kind;
    }

    public struct WaveFiredEvent
    {
        public int HandIndex;
        public Vector3 Origin;
        public Vector3 Direction;
        public float Damage;
    }

    public struct EnemyHitEvent
    {
        public Enemy Enemy;
        public float Damage;
        public Vector3 Position;
        // True when this hit rolled a critical strike, so audio / VFX / combo
        // reactors can escalate their feedback.
        public bool IsCrit;
    }

    public struct EnemyKilledEvent
    {
        public Enemy Enemy;
        public Vector3 Position;
    }

    public struct DamageTakenEvent
    {
        public float Amount;
        public float CurrentHealth;
        public float MaxHealth;
    }
}
