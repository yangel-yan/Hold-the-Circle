using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands;
using ByteDance.PICO.XR;

namespace HoldTheCircle
{
    /// <summary>Reads the demo's aim/pinch inputs without depending on its scene hierarchy or action asset.</summary>
    internal sealed class HandMenuPointer
    {
        private const float k_pressThreshold = 0.8f;
        private const float k_releaseThreshold = 0.55f;
        private const HandAimStatus k_validAim = HandAimStatus.AimComputed | HandAimStatus.AimRayValid;
        private readonly HandSide m_side;
        private readonly GameObject m_visual;
        private readonly LineRenderer m_line;
        private readonly RiteGlyph m_reticle;
        private Transform m_trackingSpace;
        private bool m_released;
        private bool m_hasRay;
        private Ray m_ray;
        private string m_source;
        private bool m_wasTracked;
        private float m_nextLogAt;
        private bool m_hasIndexDirection;
        private Vector3 m_indexDirectionInWrist;

        public HandMenuPointer(HandSide side)
        {
            m_side = side;
            m_visual = new GameObject(side + " Menu Ray");
            m_line = m_visual.AddComponent<LineRenderer>();
            m_line.sharedMaterial = RiteGlyph.RibbonMaterial;
            m_line.useWorldSpace = true;
            m_line.positionCount = 2;
            m_line.startWidth = 0.0018f;
            m_line.endWidth = 0.0008f;
            m_line.numCapVertices = 2;
            m_line.shadowCastingMode = ShadowCastingMode.Off;
            m_line.receiveShadows = false;
            m_line.lightProbeUsage = LightProbeUsage.Off;
            m_line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            m_reticle = RiteGlyph.Create(m_visual.transform, "Pinch Reticle", RiteMotif.Aegis,
                Vector2.one * 0.024f, Color.white);
            Reset();
        }

        public void SetTrackingSpace(Transform trackingSpace)
        {
            m_trackingSpace = trackingSpace;
            Reset();
        }

        public void Reset()
        {
            // Opening a menu or regaining tracking while pinched must not synthesize a click.
            m_released = false;
            m_hasRay = false;
            m_hasIndexDirection = false;
            if (m_visual != null) m_visual.SetActive(false);
        }

        public bool Poll(out Ray ray, out bool pressed)
        {
            pressed = false;
            ray = default;
            bool tracked = TryRead(out Vector3 position, out Quaternion rotation, out float pinch, out string source);
            float rotationLength = Quaternion.Dot(rotation, rotation);
            tracked &= IsFinite(position.sqrMagnitude) && IsFinite(rotationLength) &&
                rotationLength > 0.5f && IsFinite(pinch);
            if (source != m_source || tracked != m_wasTracked)
            {
                Reset();
                m_source = source;
                m_wasTracked = tracked;
                if (Debug.isDebugBuild && Time.unscaledTime >= m_nextLogAt)
                {
                    Debug.Log($"[HandMenu] {m_side} source={source}, tracked={tracked}");
                    m_nextLogAt = Time.unscaledTime + 2f;
                }
            }
            if (!tracked || m_trackingSpace == null)
            {
                Reset();
                return false;
            }

            Vector3 origin = m_trackingSpace.TransformPoint(position);
            Vector3 direction = m_trackingSpace.TransformDirection(rotation * Vector3.forward).normalized;
            if (m_hasRay)
            {
                float blend = 1f - Mathf.Exp(-35f * Mathf.Max(Time.unscaledDeltaTime, 1f / 120f));
                origin = Vector3.Lerp(m_ray.origin, origin, blend);
                direction = Vector3.Slerp(m_ray.direction, direction, blend).normalized;
            }
            m_ray = new Ray(origin, direction);
            m_hasRay = true;
            ray = m_ray;
            if (pinch <= k_releaseThreshold) m_released = true;
            else if (pinch >= k_pressThreshold && m_released)
            {
                pressed = true;
                m_released = false;
            }
            return true;
        }

        public void Draw(Ray ray, Vector3 end, bool onPanel, bool onButton, Quaternion panelRotation)
        {
            m_visual.SetActive(true);
            Color tint = onButton ? new Color(1f, 0.8f, 0.38f, 0.95f) : new Color(0.44f, 0.78f, 0.92f, 0.4f);
            m_line.startColor = new Color(tint.r, tint.g, tint.b, tint.a * 0.35f);
            m_line.endColor = tint;
            m_line.SetPosition(0, ray.origin);
            m_line.SetPosition(1, end);
            m_reticle.gameObject.SetActive(onPanel);
            if (!onPanel) return;
            m_reticle.transform.SetPositionAndRotation(end - panelRotation * Vector3.forward * 0.008f, panelRotation);
            m_reticle.SetTint(tint);
        }

        public void Dispose()
        {
            Object.Destroy(m_visual);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private bool TryRead(out Vector3 position, out Quaternion rotation, out float pinch, out string source)
        {
            bool hasAim = TryReadAim(out position, out rotation, out pinch, out source);
            // The platform aim is shoulder-biased. A finger pointer follows the proximal
            // index segment instead; neither the wrist-to-palm vector nor the curled tip
            // represents the direction the player pointed before pinching.
            if (HandTrackingGate.TryGetHand(m_side, out XRHand hand) &&
                hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wrist) &&
                hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out Pose proximal) &&
                hand.GetJoint(XRHandJointID.IndexIntermediate).TryGetPose(out Pose intermediate))
            {
                if (!hasAim)
                {
                    if (!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose tip) ||
                        !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb)) return false;
                    pinch = Mathf.Clamp01((0.05f - Vector3.Distance(tip.position, thumb.position)) / 0.035f);
                }
                Vector3 indexDirection = intermediate.position - proximal.position;
                if (indexDirection.sqrMagnitude > 0.000025f)
                {
                    if (!m_hasIndexDirection || pinch < 0.35f)
                    {
                        m_indexDirectionInWrist = Quaternion.Inverse(wrist.rotation) * indexDirection.normalized;
                        m_hasIndexDirection = true;
                    }
                    // Keep the pre-pinch angle relative to the wrist so closing the fingers
                    // cannot drag the cursor off a button, while wrist aiming still works.
                    position = proximal.position;
                    rotation = Quaternion.LookRotation(wrist.rotation * m_indexDirectionInWrist);
                    source = hasAim ? source + "+Index" : "XRHands+Index";
                    return true;
                }
            }
            m_hasIndexDirection = false;
            return hasAim;
        }

        private bool TryReadAim(out Vector3 position, out Quaternion rotation, out float pinch, out string source)
        {
            position = default;
            rotation = Quaternion.identity;
            pinch = 0f;
            source = "None";
#if ENABLE_PICO_XR_SDK
            // SDK 6 exposes the same pointer pose used by HandsDemoScene, on a PICO-specific layout.
            PicoHandInteraction device = m_side == HandSide.Left ? PicoHandInteraction.left : PicoHandInteraction.right;
            if (device != null && device.added)
            {
                source = "PicoHandInteraction";
                // SDK 6 queues a bool into a BYTE ButtonControl: true reads as
                // 1/255, below isPressed's 0.5 threshold. It is a flag, not a pinch axis.
                if (device.isTracked.ReadValue() > 0f && (device.compatAimFlags & k_validAim) == k_validAim)
                {
                    position = device.pointer.position.ReadValue();
                    rotation = device.pointer.rotation.ReadValue();
                    pinch = device.pinchValue.ReadValue();
                    return true;
                }
            }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            // Bypass stale Input System state after controllers switch to hands.
            HandAimState aim = default;
            if (PXR_HandTracking.GetAimState(m_side == HandSide.Left ? HandType.HandLeft : HandType.HandRight, ref aim))
            {
                source = "PXRAim";
                if ((aim.aimStatus & k_validAim) == k_validAim)
                {
                    position = aim.aimRayPose.Position.ToVector3();
                    rotation = aim.aimRayPose.Orientation.ToQuat();
                    pinch = (aim.aimStatus & HandAimStatus.AimIndexPinching) != 0 ? 1f : aim.touchStrengthRay;
                    return true;
                }
            }
#endif
            return false;
        }
    }
}
