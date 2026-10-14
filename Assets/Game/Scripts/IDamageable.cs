namespace HoldTheCircle
{
    using UnityEngine;

    /// <summary>
    /// Contract for any entity that can receive damage (player, enemies).
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }

        // Returns true if this hit was lethal.
        bool ApplyDamage(float amount);

        // Directional variant used by projectiles so the target can react to
        // the impact (knockback, flash) along the incoming travel direction.
        // Default implementation ignores the direction for callers that don't
        // care about hit reactions.
        bool ApplyDamage(float amount, Vector3 hitDirection)
        {
            return ApplyDamage(amount);
        }
    }
}
