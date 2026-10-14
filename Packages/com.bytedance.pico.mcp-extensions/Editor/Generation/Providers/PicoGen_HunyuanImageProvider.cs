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

// PicoGen_HunyuanImageProvider.cs
// Texture modality: Tencent Cloud Hunyuan Image (text-to-image), a free-trial
// friendly way to produce 2D textures (50 free calls granted on first activation).
//   Endpoint : hunyuan.tencentcloudapi.com, version 2023-09-01, region ap-guangzhou
//   Submit   : SubmitHunyuanImageJob -> JobId
//   Query    : QueryHunyuanImageJob  -> JobStatusCode 1/2/4/5 + ResultImage[] (URLs)
// This response shape differs from the ai3d family: numeric status codes and a
// plain URL array (no per-file Type), so it is parsed separately here.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ByteDance.PICO.Generation.Editor
{
    [PicoGenProvider(Priority = 100)]
    public class PicoGen_HunyuanImageProvider : IPicoGenProvider
    {
        static readonly PicoGen_TencentEndpoint Endpoint =
            new PicoGen_TencentEndpoint("hunyuan.tencentcloudapi.com", "hunyuan", "2023-09-01");

        const string k_submitAction = "SubmitHunyuanImageJob";
        const string k_queryAction  = "QueryHunyuanImageJob";

        public string ProviderId => "tencent-hunyuan-image";

        public IEnumerable<PicoGenModality> SupportedModalities
        {
            get { yield return PicoGenModality.Texture; }
        }

        public bool IsReady(out string notReadyReason)
        {
            if (!PicoGen_Settings.HasCredentials)
            {
                notReadyReason = "Tencent Cloud credentials are not set. Open Preferences > PICO > AI Generation.";
                return false;
            }
            notReadyReason = null;
            return true;
        }

        public PicoGenJob Submit(PicoGenRequest request)
        {
            var job = new PicoGenJob { ProviderId = ProviderId, Modality = PicoGenModality.Texture };

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                job.State = PicoGenJobState.Failed;
                job.ErrorMessage = "Prompt is required for text-to-image (max 1024 chars).";
                return job;
            }

            // Revise=0 keeps the prompt literal; LogoAdd=0 drops the AI watermark
            // (allowed for private texture use). Region must be ap-guangzhou.
            var body = new PicoGen_Json()
                .AddString("Prompt", request.Prompt)
                .AddInt("Revise", 0)
                .AddInt("LogoAdd", 0)
                .Build();

            var resp = PicoGen_TencentClient.Call(Endpoint, k_submitAction, body);
            if (!resp.ok) { job.State = PicoGenJobState.Failed; job.ErrorMessage = resp.error; return job; }

            var parsed = JsonUtility.FromJson<SubmitEnvelope>(resp.body);
            if (parsed?.Response == null)
            {
                job.State = PicoGenJobState.Failed;
                job.ErrorMessage = "Malformed submit response: " + PicoGen_TencentParse.Truncate(resp.body);
                return job;
            }
            if (!string.IsNullOrEmpty(parsed.Response.Error?.Code))
            {
                job.State = PicoGenJobState.Failed;
                job.ErrorCode = parsed.Response.Error.Code;
                job.ErrorMessage = parsed.Response.Error.Message;
                return job;
            }

            job.JobId = parsed.Response.JobId;
            job.State = string.IsNullOrEmpty(job.JobId) ? PicoGenJobState.Failed : PicoGenJobState.Queued;
            if (job.State == PicoGenJobState.Failed) job.ErrorMessage = "Submit returned no JobId.";
            return job;
        }

        public PicoGenJob Query(string jobId, PicoGenModality modality)
        {
            var job = new PicoGenJob { ProviderId = ProviderId, Modality = modality, JobId = jobId };

            var body = new PicoGen_Json().AddString("JobId", jobId).Build();
            var resp = PicoGen_TencentClient.Call(Endpoint, k_queryAction, body);
            if (!resp.ok) { job.State = PicoGenJobState.Failed; job.ErrorMessage = resp.error; return job; }

            var parsed = JsonUtility.FromJson<QueryEnvelope>(resp.body);
            if (parsed?.Response == null)
            {
                job.State = PicoGenJobState.Failed;
                job.ErrorMessage = "Malformed query response: " + PicoGen_TencentParse.Truncate(resp.body);
                return job;
            }
            if (!string.IsNullOrEmpty(parsed.Response.Error?.Code))
            {
                job.State = PicoGenJobState.Failed;
                job.ErrorCode = parsed.Response.Error.Code;
                job.ErrorMessage = parsed.Response.Error.Message;
                return job;
            }

            job.ErrorCode = parsed.Response.JobErrorCode;
            job.ErrorMessage = parsed.Response.JobErrorMsg;
            job.State = PicoGen_TencentParse.MapImageStatusCode(parsed.Response.JobStatusCode);
            job.StatusMessage = parsed.Response.JobStatusMsg;

            if (parsed.Response.ResultImage != null)
                foreach (var url in parsed.Response.ResultImage)
                    if (!string.IsNullOrEmpty(url))
                        job.Files.Add(new PicoGenResultFile { Url = url, Type = "PNG" });

            return job;
        }

        public IReadOnlyList<PicoGenModelInfo> GetModels(PicoGenModality modality)
        {
            if (modality != PicoGenModality.Texture) return Array.Empty<PicoGenModelInfo>();
            return new List<PicoGenModelInfo>
            {
                new() { ModelId = "hunyuan-image", Description = "Hunyuan text-to-image (2D texture; free trial quota on first activation)" },
            };
        }

        // ---- JsonUtility DTOs (Hunyuan Image response shape) ----
        [Serializable] class SubmitEnvelope { public SubmitResponse Response; }
        [Serializable] class SubmitResponse { public string JobId; public string RequestId; public PicoGen_TcError Error; }

        [Serializable] class QueryEnvelope { public QueryResponse Response; }
        [Serializable]
        class QueryResponse
        {
            public string JobStatusCode;   // 1 wait / 2 run / 4 fail / 5 done
            public string JobStatusMsg;
            public string JobErrorCode;
            public string JobErrorMsg;
            public string[] ResultImage;   // result image URLs (valid ~1h)
            public string[] ResultDetails;
            public string RequestId;
            public PicoGen_TcError Error;
        }
    }
}
