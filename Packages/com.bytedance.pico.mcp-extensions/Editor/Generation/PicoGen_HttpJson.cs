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

// PicoGen_HttpJson.cs
// Minimal blocking HTTP + a tiny JSON object builder shared by providers.
//
// Blocking on purpose: MCP tools run on the editor main thread and Submit/Query
// calls are FAST (the heavy generation is async server-side). JSON is parsed by
// UnityEngine.JsonUtility so the core asmdef needs no Newtonsoft dependency.
// Request bodies are built by PicoGen_Json because JsonUtility cannot OMIT empty
// fields, and Tencent rejects mutually-exclusive params (e.g. Prompt + ImageUrl).

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace ByteDance.PICO.Generation.Editor
{
    public static class PicoGen_HttpJson
    {
        static readonly HttpClient s_client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        public class Response
        {
            public bool ok;
            public int statusCode;
            public string body;
            public string error;
        }

        public static Response PostJson(string url, string jsonBody, IDictionary<string, string> headers)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                if (headers != null)
                    foreach (var kv in headers)
                    {
                        if (string.Equals(kv.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
                        req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                    }

                var resp = s_client.SendAsync(req).GetAwaiter().GetResult();
                var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return new Response
                {
                    ok = resp.IsSuccessStatusCode,
                    statusCode = (int)resp.StatusCode,
                    body = body,
                    error = resp.IsSuccessStatusCode ? null : $"HTTP {(int)resp.StatusCode}: {body}",
                };
            }
            catch (Exception e)
            {
                return new Response { ok = false, statusCode = 0, error = e.Message };
            }
        }

        public static byte[] DownloadBytes(string url, out string error)
        {
            error = null;
            try
            {
                var resp = s_client.GetAsync(url).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                {
                    error = $"HTTP {(int)resp.StatusCode} downloading result file";
                    return null;
                }
                return resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }
    }

    // Tiny JSON object builder: emits only the fields you add, correctly escaped.
    public class PicoGen_Json
    {
        readonly StringBuilder m_sb = new StringBuilder("{");
        bool m_first = true;

        public PicoGen_Json AddString(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return this;
            Sep();
            m_sb.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value)).Append('"');
            return this;
        }

        public PicoGen_Json AddBool(string key, bool value)
        {
            Sep();
            m_sb.Append('"').Append(Escape(key)).Append("\":").Append(value ? "true" : "false");
            return this;
        }

        public PicoGen_Json AddInt(string key, int value)
        {
            Sep();
            m_sb.Append('"').Append(Escape(key)).Append("\":").Append(value.ToString());
            return this;
        }

        public string Build() => m_sb.ToString() + "}";

        void Sep()
        {
            if (!m_first) m_sb.Append(',');
            m_first = false;
        }

        static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
