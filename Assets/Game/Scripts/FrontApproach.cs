using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>Encounter heading stays fixed when the player glances away.</summary>
    internal static class FrontApproach
    {
        public static Vector3 CaptureForward(Transform player)
        {
            Vector3 forward = player != null ? player.forward : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }

        public static Vector3 Position(Transform player, Vector3 forward, float depth, float lane, float height = 0f)
        {
            Vector3 origin = player != null ? player.position : Vector3.zero;
            origin.y = height;
            return origin + forward * depth + Vector3.Cross(Vector3.up, forward) * lane;
        }

        public static Vector3 Constrain(Vector3 position, Transform player, Vector3 forward)
        {
            Vector3 origin = player != null ? player.position : Vector3.zero;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 offset = position - origin;
            float depth = Mathf.Max(1.2f, Vector3.Dot(offset, forward));
            float lane = Mathf.Clamp(Vector3.Dot(offset, right), -depth * 0.7f, depth * 0.7f);
            Vector3 result = origin + forward * depth + right * lane;
            result.y = position.y;
            return result;
        }
    }
}
