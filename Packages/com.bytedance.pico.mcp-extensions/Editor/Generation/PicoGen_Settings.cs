/*******************************************************************************
Copyright © 2015-2022 PICO Technology Co., Ltd.All rights reserved.

NOTICE：All information contained herein is, and remains the property of
PICO Technology Co., Ltd. The intellectual and technical concepts
contained herein are proprietary to PICO Technology Co., Ltd. and may be
covered by patents, patents in process, and are protected by trade secret or
copyright law. Dissemination of this information or reproduction of this
material is strictly forbidden unless prior written permission is obtained from
PICO Technology Co., Ltd.
*******************************************************************************/

// PicoGen_Settings.cs
// EditorPrefs-backed shared credentials + a Preferences page under
// "Preferences/PICO/AI Generation".
//
// All three Hunyuan products (3D / Motion / Image) authenticate with the SAME
// Tencent Cloud SecretId/SecretKey, so credentials are stored once here. Secrets
// live in EditorPrefs (per-machine, never committed) — the same BYOK philosophy
// as Unity's own Scenario integration.

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ByteDance.PICO.Generation.Editor
{
    public static class PicoGen_Settings
    {
        const string k_secretId  = "PICO.Generation.Tencent.SecretId";
        const string k_secretKey = "PICO.Generation.Tencent.SecretKey";
        const string k_region    = "PICO.Generation.Tencent.Region";

        const string k_defaultRegion = "ap-guangzhou";

        public static string SecretId
        {
            get => EditorPrefs.GetString(k_secretId, "");
            set => EditorPrefs.SetString(k_secretId, value ?? "");
        }

        public static string SecretKey
        {
            get => EditorPrefs.GetString(k_secretKey, "");
            set => EditorPrefs.SetString(k_secretKey, value ?? "");
        }

        public static string Region
        {
            get => EditorPrefs.GetString(k_region, k_defaultRegion);
            set => EditorPrefs.SetString(k_region, string.IsNullOrWhiteSpace(value) ? k_defaultRegion : value);
        }

        public static bool HasCredentials =>
            !string.IsNullOrWhiteSpace(SecretId) && !string.IsNullOrWhiteSpace(SecretKey);

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Preferences/PICO/AI Generation", SettingsScope.User)
            {
                label = "AI Generation",
                activateHandler = (searchContext, root) =>
                {
                    root.style.paddingLeft = 8;
                    root.style.paddingTop = 8;

                    root.Add(new Label("Tencent Cloud Hunyuan (BYOK)")
                    {
                        style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 }
                    });
                    root.Add(new HelpBox(
                        "One credential pair drives 3D (object), Motion (animation) and Image (texture). " +
                        "Stored in EditorPrefs on THIS machine only, never committed. Get SecretId / " +
                        "SecretKey from the Tencent Cloud CAM console; enable the ai3d and aiart/hunyuan " +
                        "services (free trial quota is granted on first activation).",
                        HelpBoxMessageType.Info));

                    var secretId = new TextField("Secret Id") { value = SecretId };
                    secretId.RegisterValueChangedCallback(e => SecretId = e.newValue);
                    root.Add(secretId);

                    var secretKey = new TextField("Secret Key") { value = SecretKey, isPasswordField = true };
                    secretKey.RegisterValueChangedCallback(e => SecretKey = e.newValue);
                    root.Add(secretKey);

                    var region = new TextField("Region") { value = Region };
                    region.RegisterValueChangedCallback(e => Region = e.newValue);
                    root.Add(region);
                },
                keywords = new[] { "PICO", "AI", "Generation", "Hunyuan", "Tencent", "3D", "Motion", "Image", "BYOK" },
            };
        }
    }
}
