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

// PicoGen_Result.cs
// Shared result envelope returned by every pico_generation_* MCP tool. Identical
// in shape to PXR_MCP_Result so an LLM consuming both packages parses one uniform
// { status, summary, ... } vocabulary.

using System;

namespace ByteDance.PICO.Generation.Tools
{
    public class PicoGen_Result
    {
        public const string StatusOk             = "ok";
        public const string StatusPending        = "pending";
        public const string StatusAlreadyPresent = "already_present";
        public const string StatusSkipped        = "skipped";
        public const string StatusNotImplemented = "not_implemented";
        public const string StatusError          = "error";

        public string status;
        public string summary;
        public string warning;
        public string error;
        public object data;

        public static PicoGen_Result Ok(string summary, object data = null)
            => new PicoGen_Result { status = StatusOk, summary = summary, data = data };

        public static PicoGen_Result Pending(string summary, object data = null)
            => new PicoGen_Result { status = StatusPending, summary = summary, data = data };

        public static PicoGen_Result AlreadyPresent(string summary, object data = null)
            => new PicoGen_Result { status = StatusAlreadyPresent, summary = summary, data = data };

        public static PicoGen_Result Skipped(string summary, string warning, object data = null)
            => new PicoGen_Result { status = StatusSkipped, summary = summary, warning = warning, data = data };

        public static PicoGen_Result NotImplemented(string summary, string warning, object data = null)
            => new PicoGen_Result { status = StatusNotImplemented, summary = summary, warning = warning, data = data };

        public static PicoGen_Result Error(string summary, string error, object data = null)
            => new PicoGen_Result { status = StatusError, summary = summary, error = error, data = data };

        public static PicoGen_Result FromException(string toolName, Exception ex)
            => new PicoGen_Result
            {
                status  = StatusError,
                summary = "[" + toolName + "] threw " + ex.GetType().Name,
                error   = ex.Message,
            };
    }
}
