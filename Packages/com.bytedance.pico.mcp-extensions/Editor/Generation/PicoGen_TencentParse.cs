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

// PicoGen_TencentParse.cs
// Shared response bits for Tencent providers: the common Error DTO and the two
// status-code vocabularies Hunyuan uses (WAIT/RUN/DONE/FAIL for 3D & Motion;
// numeric 1/2/4/5 for Image).

using System;

namespace ByteDance.PICO.Generation.Editor
{
    [Serializable]
    public class PicoGen_TcError
    {
        public string Code;
        public string Message;
    }

    public static class PicoGen_TencentParse
    {
        // ai3d family (Hunyuan 3D, Hunyuan Motion): Status = WAIT/RUN/DONE/FAIL.
        public static PicoGenJobState MapWaitRunDoneFail(string status)
        {
            switch ((status ?? "").ToUpperInvariant())
            {
                case "WAIT": return PicoGenJobState.Queued;
                case "RUN":  return PicoGenJobState.Running;
                case "DONE": return PicoGenJobState.Succeeded;
                case "FAIL": return PicoGenJobState.Failed;
                default:     return PicoGenJobState.Unknown;
            }
        }

        // Hunyuan Image: JobStatusCode = 1 wait / 2 run / 4 fail / 5 done.
        public static PicoGenJobState MapImageStatusCode(string code)
        {
            switch ((code ?? "").Trim())
            {
                case "1": return PicoGenJobState.Queued;
                case "2": return PicoGenJobState.Running;
                case "4": return PicoGenJobState.Failed;
                case "5": return PicoGenJobState.Succeeded;
                default:  return PicoGenJobState.Unknown;
            }
        }

        public static string Truncate(string s)
            => string.IsNullOrEmpty(s) ? s : (s.Length > 300 ? s.Substring(0, 300) + "..." : s);
    }
}
