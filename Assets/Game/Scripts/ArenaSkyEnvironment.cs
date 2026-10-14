using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HoldTheCircle
{
    /// <summary>Background mode is independent of real-world collision cover.</summary>
    public static class ArenaSkyEnvironment
    {
        public const string MaterialResource = "Art/LanternNightSky";
        public static bool UsePassthrough => PlayerPrefs.GetInt("HTC.Passthrough", 0) == 1;

        public static void SetPassthrough(bool enabled, Camera camera)
        {
            PlayerPrefs.SetInt("HTC.Passthrough", enabled ? 1 : 0);
            PlayerPrefs.Save();
            Apply(camera, enabled);
        }

        public static void Apply(Camera camera, bool usePassthrough)
        {
            if (camera == null)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            ByteDance.PICO.XR.PXR_Manager.EnableVideoSeeThrough = usePassthrough;
#endif
            Material skybox = Resources.Load<Material>(MaterialResource);
            if (skybox != null) RenderSettings.skybox = skybox;
            if (usePassthrough)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData != null)
                {
                    cameraData.renderPostProcessing = false;
                }
                return;
            }

            if (skybox == null)
            {
                Debug.LogWarning("Lantern night sky material is missing from Resources/Art.");
                return;
            }

            RenderSettings.skybox = skybox;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.075f, 1f);
            if (Debug.isDebugBuild)
                Debug.Log($"[ArenaSky] mode={(usePassthrough ? "MR" : "NightSky")}, clear={camera.clearFlags}, shader={skybox.shader.name}");
        }
    }
}
