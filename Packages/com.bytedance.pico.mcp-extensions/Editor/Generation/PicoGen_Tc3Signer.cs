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

// PicoGen_Tc3Signer.cs
// Tencent Cloud TC3-HMAC-SHA256 request signer, shared by every Tencent-backed
// provider (Hunyuan 3D, Hunyuan Motion, Hunyuan Image). Isolating the algorithm
// here means a new Tencent product provider is just a new endpoint constant, not
// re-implemented crypto.
//
// Only POST-with-JSON is supported because every Hunyuan action is POST + JSON.

using System;
using System.Security.Cryptography;
using System.Text;

namespace ByteDance.PICO.Generation.Editor
{
    public static class PicoGen_Tc3Signer
    {
        const string k_signedHeaders = "content-type;host;x-tc-action";
        const string k_contentType   = "application/json; charset=utf-8";
        const string k_algorithm     = "TC3-HMAC-SHA256";
        const string k_terminator    = "tc3_request";

        // Build the "Authorization" header value for one request.
        public static string BuildAuthorization(
            string secretId, string secretKey, string host, string service,
            string action, string payload, long timestamp)
        {
            var hashedPayload = HexSha256(payload);
            var canonicalHeaders =
                "content-type:" + k_contentType + "\n" +
                "host:" + host + "\n" +
                "x-tc-action:" + action.ToLowerInvariant() + "\n";
            var canonicalRequest =
                "POST\n" +
                "/\n" +
                "\n" +
                canonicalHeaders + "\n" +
                k_signedHeaders + "\n" +
                hashedPayload;

            var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd");
            var credentialScope = date + "/" + service + "/" + k_terminator;
            var stringToSign =
                k_algorithm + "\n" +
                timestamp + "\n" +
                credentialScope + "\n" +
                HexSha256(canonicalRequest);

            var secretDate    = HmacSha256(Encoding.UTF8.GetBytes("TC3" + secretKey), date);
            var secretService = HmacSha256(secretDate, service);
            var secretSigning = HmacSha256(secretService, k_terminator);
            var signature     = HexEncode(HmacSha256(secretSigning, stringToSign));

            return k_algorithm +
                   " Credential=" + secretId + "/" + credentialScope +
                   ", SignedHeaders=" + k_signedHeaders +
                   ", Signature=" + signature;
        }

        public static string ContentType => k_contentType;

        static string HexSha256(string input)
        {
            using var sha = SHA256.Create();
            return HexEncode(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }

        static byte[] HmacSha256(byte[] key, string message)
        {
            using var mac = new HMACSHA256(key);
            return mac.ComputeHash(Encoding.UTF8.GetBytes(message));
        }

        static string HexEncode(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
