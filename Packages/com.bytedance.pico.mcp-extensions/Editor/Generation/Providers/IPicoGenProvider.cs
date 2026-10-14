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

// IPicoGenProvider.cs
// The extension seam: one interface every generation backend implements. Adding
// a capability (e.g. an audio provider) is "write one class + [PicoGenProvider]";
// the registry discovers it and the tools route to it automatically.

using System.Collections.Generic;

namespace ByteDance.PICO.Generation.Editor
{
    public interface IPicoGenProvider
    {
        string ProviderId { get; }

        IEnumerable<PicoGenModality> SupportedModalities { get; }

        bool IsReady(out string notReadyReason);

        PicoGenJob Submit(PicoGenRequest request);

        PicoGenJob Query(string jobId, PicoGenModality modality);

        IReadOnlyList<PicoGenModelInfo> GetModels(PicoGenModality modality);
    }
}
