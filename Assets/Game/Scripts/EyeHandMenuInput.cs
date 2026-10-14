using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using ByteDance.PICO.XR;
using ByteDance.PICO.Interaction;

namespace HoldTheCircle
{
    /// <summary>Official PICO eye gaze and threshold readers bridged to the world-space menu.</summary>
    [DefaultExecutionOrder(-31500)]
    public sealed class EyeHandMenuInput : MonoBehaviour
    {
        private const string k_eyePermission = "com.picovr.permission.EYE_TRACKING";
        private readonly PXR_ReleaseThresholdButtonReader[] m_readers = new PXR_ReleaseThresholdButtonReader[2];
        private readonly bool[] m_available = new bool[2];
        private readonly bool[] m_released = new bool[2];
        private PXR_EyeGazeCursorDriver m_eye;
        private Transform m_trackingSpace;
        private RiteGlyph m_cursor;
        private bool m_supported;
        private bool m_supportChecked;
        private bool m_permissionRequested;
        private bool m_hasRay;
        private Ray m_ray;
        private string m_lastStatus;
        private float m_nextDiagnostic;
        private float m_nextSupportProbe;
        public bool UsingEyes { get; private set; }
        public string Hint => UsingEyes
            ? "Look at a choice. Pinch either hand, release, then look at CONFIRM and pinch."
            : "Eye tracking unavailable. Aim with your head and pinch either hand.";

        private void Awake()
        {
            if (FindFirstObjectByType<PXR_InteractorManager>() == null)
                new GameObject("PICO Eye Hand Interaction").AddComponent<PXR_InteractorManager>();
            m_eye = gameObject.AddComponent<PXR_EyeGazeCursorDriver>();
            // Read the provider directly; the menu does not use physics colliders or its cursor driver Update.
            m_eye.enabled = false;
            for (int i = 0; i < m_readers.Length; i++)
            {
                var reader = gameObject.AddComponent<PXR_ReleaseThresholdButtonReader>();
                reader.pressThreshold = 0.75f;
                reader.releaseThreshold = 0.65f;
                reader.valueInput = new XRInputButtonReader("Menu pinch")
                {
                    inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue
                };
                m_readers[i] = reader;
            }
            m_cursor = RiteGlyph.Create(transform, "Gaze Cursor", RiteMotif.Aegis,
                Vector2.one * 0.021f, Color.white);
            m_cursor.gameObject.SetActive(false);
        }

        public void SetTrackingSpace(Transform trackingSpace)
        {
            m_trackingSpace = trackingSpace;
            ResetInput();
        }

        public void ResetInput()
        {
            m_hasRay = false;
            for (int i = 0; i < m_released.Length; i++) m_released[i] = false;
            if (m_cursor != null) m_cursor.gameObject.SetActive(false);
        }

        private void Update()
        {
            bool accepting = WorldMenu.Current != null && WorldMenu.Current.AcceptingInput;
            for (int i = 0; i < m_readers.Length; i++)
            {
                float value = 0f;
                m_available[i] = accepting && TryReadPinch(i == 0 ? HandSide.Left : HandSide.Right, out value);
                m_readers[i].valueInput.manualValue = m_available[i] ? value : 0f;
                if (!m_available[i]) m_released[i] = false;
            }
        }

        public bool ReadPress()
        {
            bool pressed = false;
            for (int i = 0; i < m_readers.Length; i++)
            {
                if (!m_available[i]) continue;
                if (m_readers[i].valueInput.manualValue <= m_readers[i].releaseThreshold) m_released[i] = true;
                if (!m_released[i] || !m_readers[i].ReadWasPerformedThisFrame()) continue;
                m_released[i] = false;
                pressed = true;
                Debug.Log($"[EyeHandMenu] Pinch {(i == 0 ? "Left" : "Right")}");
            }
            return pressed;
        }

        public bool TryGetRay(out Ray ray)
        {
            Camera camera = Camera.main;
            ray = default;
            if (camera == null) return false;
            UsingEyes = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!m_supportChecked && Time.unscaledTime >= m_nextSupportProbe)
            {
                m_nextSupportProbe = Time.unscaledTime + 5f;
                bool supported = false;
                int count = 0;
                EyeTrackingMode[] modes = new EyeTrackingMode[8];
                int code = PXR_MotionTracking.GetEyeTrackingSupported(ref supported, ref count, ref modes);
                m_supportChecked = code == 0;
                m_supported = code == 0 && supported;
                LogStatus($"Support result={code}, supported={m_supported}, modes={count}");
            }
            if (m_supported && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(k_eyePermission) &&
                !m_permissionRequested)
            {
                m_permissionRequested = true;
                UnityEngine.Android.Permission.RequestUserPermission(k_eyePermission);
            }
            if (m_supported && UnityEngine.Android.Permission.HasUserAuthorizedPermission(k_eyePermission) &&
                m_trackingSpace != null)
            {
                m_eye.GetEyeForward(out int state, out Vector3 position, out Quaternion rotation);
                if ((state & (int)InputTrackingState.Rotation) != 0 &&
                    float.IsFinite(position.sqrMagnitude) && float.IsFinite(Quaternion.Dot(rotation, rotation)) &&
                    Quaternion.Dot(rotation, rotation) > 0.5f)
                {
                    ray = new Ray(m_trackingSpace.TransformPoint(position),
                        m_trackingSpace.TransformDirection(rotation * Vector3.forward).normalized);
                    UsingEyes = true;
                }
            }
#endif
            if (!UsingEyes) ray = new Ray(camera.transform.position, camera.transform.forward);
            if (m_hasRay)
            {
                float blend = 1f - Mathf.Exp(-28f * Time.unscaledDeltaTime);
                ray = new Ray(Vector3.Lerp(m_ray.origin, ray.origin, blend),
                    Vector3.Slerp(m_ray.direction, ray.direction, blend).normalized);
            }
            m_ray = ray;
            m_hasRay = true;
            LogStatus(UsingEyes ? "Gaze=Eye" : "Gaze=Head fallback");
            if (Debug.isDebugBuild && Time.unscaledTime > m_nextDiagnostic)
            {
                m_nextDiagnostic = Time.unscaledTime + 5f;
                Debug.Log($"[EyeHandMenu] eyes={UsingEyes}, pinchReady L={m_available[0]} R={m_available[1]}, " +
                    $"strength L={m_readers[0].valueInput.manualValue:F2} R={m_readers[1].valueInput.manualValue:F2}");
            }
            return true;
        }

        public void Draw(Vector3 position, bool visible, bool onButton, Quaternion rotation)
        {
            m_cursor.gameObject.SetActive(visible);
            if (!visible) return;
            // Cursor is parented to the scaled document; counteract that scale.
            m_cursor.transform.localScale = new Vector3(0.021f, 0.021f, 1f) / transform.lossyScale.x;
            m_cursor.transform.SetPositionAndRotation(position - rotation * Vector3.forward * 0.009f, rotation);
            m_cursor.SetTint(onButton ? new Color(1f, 0.81f, 0.4f) : new Color(0.55f, 0.86f, 1f, 0.65f));
        }

        private void LogStatus(string status)
        {
            if (status == m_lastStatus) return;
            m_lastStatus = status;
            Debug.Log("[EyeHandMenu] " + status);
        }

        private static bool TryReadPinch(HandSide side, out float value)
        {
            value = 0f;
            PicoHandInteraction device = side == HandSide.Left ? PicoHandInteraction.left : PicoHandInteraction.right;
            // This BYTE flag reads 1/255 for true. Aim and wrist pose validity are irrelevant to pinch.
            if (device != null && device.added && device.pinchReady.ReadValue() > 0f)
            {
                value = device.pinchValue.ReadValue();
                return float.IsFinite(value);
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            HandAimState aim = default;
            if (PXR_HandTracking.GetAimState(side == HandSide.Left ? HandType.HandLeft : HandType.HandRight, ref aim) &&
                (aim.aimStatus & HandAimStatus.AimComputed) != 0)
            {
                value = (aim.aimStatus & HandAimStatus.AimIndexPinching) != 0 ? 1f : aim.touchStrengthRay;
                return float.IsFinite(value);
            }
#endif
            if (!HandTrackingGate.TryGetHand(side, out XRHand hand) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb)) return false;
            value = Mathf.Clamp01((0.05f - Vector3.Distance(index.position, thumb.position)) / 0.035f);
            return true;
        }

        private void OnApplicationFocus(bool focus) => ResetInput();
        private void OnApplicationPause(bool pause) => ResetInput();
    }
}
