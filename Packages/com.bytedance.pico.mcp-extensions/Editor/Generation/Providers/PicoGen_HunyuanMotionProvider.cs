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

// PicoGen_HunyuanMotionProvider.cs
// Animation modality: Tencent Cloud Hunyuan Motion (text-to-motion, HY-Motion-1.0).
//   Endpoint : ai3d.tencentcloudapi.com, version 2025-05-13
//   Submit   : SubmitHunyuanTo3DMotionJob  -> JobId
//   Query    : DescribeHunyuanTo3DMotionJob -> Status WAIT/RUN/DONE/FAIL + ResultFile3Ds
// Output is an FBX carrying skeletal animation (optionally skinned mesh). NOTE:
// the skeleton is HY-Motion's own rig; retargeting onto a different character rig
// is left to the user, so this modality is flagged experimental in the tool desc.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ByteDance.PICO.Generation.Editor
{
    [PicoGenProvider(Priority = 100)]
    public class PicoGen_HunyuanMotionProvider : IPicoGenProvider
    {
        static readonly PicoGen_TencentEndpoint Endpoint =
            new PicoGen_TencentEndpoint("ai3d.tencentcloudapi.com", "ai3d", "2025-05-13");

        const string k_submitAction = "SubmitHunyuanTo3DMotionJob";
        const string k_queryAction  = "DescribeHunyuanTo3DMotionJob";

        public string ProviderId => "tencent-hunyuan-motion";

        public IEnumerable<PicoGenModality> SupportedModalities
        {
            get { yield return PicoGenModality.Animation; }
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
            var job = new PicoGenJob { ProviderId = ProviderId, Modality = PicoGenModality.Animation };

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                job.State = PicoGenJobState.Failed;
                job.ErrorMessage = "Prompt is required for text-to-motion (max 128 chars).";
                return job;
            }

            var jb = new PicoGen_Json()
                .AddString("Prompt", request.Prompt)
                .AddString("Model", string.IsNullOrWhiteSpace(request.ModelId) ? "HY-Motion-1.0" : request.ModelId)
                .AddBool("EnableMesh", true);
            if (request.DurationSeconds >= 1 && request.DurationSeconds <= 12)
                jb.AddInt("Duration", request.DurationSeconds);
            var body = jb.Build();

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

            job.ErrorCode = parsed.Response.ErrorCode;
            job.ErrorMessage = parsed.Response.ErrorMessage;
            job.State = PicoGen_TencentParse.MapWaitRunDoneFail(parsed.Response.Status);
            job.StatusMessage = parsed.Response.Status;

            if (parsed.Response.ResultFile3Ds != null)
                foreach (var f in parsed.Response.ResultFile3Ds)
                    job.Files.Add(new PicoGenResultFile { Url = f.Url, Type = f.Type, PreviewUrl = f.PreviewImageUrl });

            return job;
        }

        public IReadOnlyList<PicoGenModelInfo> GetModels(PicoGenModality modality)
        {
            if (modality != PicoGenModality.Animation) return Array.Empty<PicoGenModelInfo>();
            return new List<PicoGenModelInfo>
            {
                new() { ModelId = "HY-Motion-1.0", Description = "Hunyuan text-to-motion v1.0 (outputs FBX skeletal animation)" },
            };
        }

        // ---- JsonUtility DTOs ----
        [Serializable] class SubmitEnvelope { public SubmitResponse Response; }
        [Serializable] class SubmitResponse { public string JobId; public string RequestId; public PicoGen_TcError Error; }

        [Serializable] class QueryEnvelope { public QueryResponse Response; }
        [Serializable]
        class QueryResponse
        {
            public string Status;
            public string ErrorCode;
            public string ErrorMessage;
            public File3D[] ResultFile3Ds;
            public string RequestId;
            public PicoGen_TcError Error;
        }
        [Serializable] class File3D { public string Type; public string Url; public string PreviewImageUrl; }
    }
}
