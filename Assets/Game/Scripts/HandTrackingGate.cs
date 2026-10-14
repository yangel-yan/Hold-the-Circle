using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace HoldTheCircle
{
    public static class HandTrackingGate
    {
        private static readonly List<XRHandSubsystem> s_subsystems = new(2);
        private static XRHandSubsystem s_subsystem;

        public static bool TryGetHand(HandSide side, out XRHand hand)
        {
            if (s_subsystem == null || !s_subsystem.running)
            {
                s_subsystems.Clear();
                SubsystemManager.GetSubsystems(s_subsystems);
                s_subsystem = null;
                for (int i = 0; i < s_subsystems.Count; i++)
                {
                    if (!s_subsystems[i].running) continue;
                    s_subsystem = s_subsystems[i];
                    break;
                }
            }

            hand = s_subsystem != null
                ? side == HandSide.Left ? s_subsystem.leftHand : s_subsystem.rightHand
                : default;
            return s_subsystem != null && hand.isTracked;
        }

        public static bool IsTracked(HandSide side)
        {
#if UNITY_EDITOR
            return true;
#else
            return TryGetHand(side, out _);
#endif
        }
    }
}
