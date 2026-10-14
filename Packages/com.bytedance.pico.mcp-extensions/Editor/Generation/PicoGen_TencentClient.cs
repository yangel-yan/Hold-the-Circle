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

// PicoGen_TencentClient.cs
// One-stop signed-call helper for any Tencent Cloud "action" API. A provider
// supplies the product endpoint (host/service/version) and per-call action; this
// class handles TC3 signing + the common request headers + POST. Sharing this
// is what lets three different Hunyuan products (3D / Motion / Image) live behind
// the same credential pair with no duplicated plumbing.

using System;
using System.Collections.Generic;

namespace ByteDance.PICO.Generation.Editor
{
    // Immutable description of one Tencent Cloud product endpoint.
    public class PicoGen_TencentEndpoint
    {
        public string Host;      // e.g. "ai3d.tencentcloudapi.com"
        public string Service;   // e.g. "ai3d"
        public string Version;   // e.g. "2025-05-13"

        public PicoGen_TencentEndpoint(string host, string service, string version)
        {
            Host = host; Service = service; Version = version;
        }
    }

    public static class PicoGen_TencentClient
    {
        // Signed POST of a JSON body to a Tencent action. Credentials + region
        // come from the shared PicoGen_Settings store.
        public static PicoGen_HttpJson.Response Call(PicoGen_TencentEndpoint endpoint, string action, string jsonBody)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var auth = PicoGen_Tc3Signer.BuildAuthorization(
                PicoGen_Settings.SecretId, PicoGen_Settings.SecretKey,
                endpoint.Host, endpoint.Service, action, jsonBody, timestamp);

            var headers = new Dictionary<string, string>
            {
                ["Authorization"]  = auth,
                ["Host"]           = endpoint.Host,
                ["X-TC-Action"]    = action,
                ["X-TC-Version"]   = endpoint.Version,
                ["X-TC-Timestamp"] = timestamp.ToString(),
                ["X-TC-Region"]    = PicoGen_Settings.Region,
            };
            return PicoGen_HttpJson.PostJson("https://" + endpoint.Host + "/", jsonBody, headers);
        }
    }
}
