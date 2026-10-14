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

// PicoGen_Types.cs
// Shared value types used across the whole PICO Generation layer.

using System.Collections.Generic;

namespace ByteDance.PICO.Generation.Editor
{
    // The four generation modalities exposed as pico_generation_* MCP tools.
    public enum PicoGenModality
    {
        Object,     // 3D mesh / prefab      (Hunyuan 3D)
        Texture,    // 2D texture image      (Hunyuan Image)
        Audio,      // sound / voice         (no provider yet)
        Animation,  // 3D motion clip (FBX)  (Hunyuan Motion)
    }

    // Lifecycle of an async generation job, normalized across every provider.
    public enum PicoGenJobState
    {
        Unknown,
        Queued,     // accepted, waiting to run
        Running,    // in progress
        Succeeded,  // done, result files ready
        Failed,     // terminal failure
    }

    // What the caller asked for. Not every field applies to every modality;
    // providers read only what they support and ignore the rest.
    public class PicoGenRequest
    {
        public PicoGenModality Modality;

        // Text prompt (text-to-X). For Hunyuan 3D it is mutually exclusive with
        // the image inputs; for Image/Motion it is the primary input.
        public string Prompt;

        // Image-to-X inputs (object modality only).
        public string ImageUrl;
        public string ImageBase64;

        // Optional explicit model id / version override; null => provider default.
        public string ModelId;

        // Desired output file format (provider-specific; e.g. "GLB").
        public string Format;

        // Object modality: request PBR materials.
        public bool EnablePbr;

        // Animation modality: motion length in seconds (1-12).
        public int DurationSeconds;

        // Human-readable asset name; drives the import folder.
        public string Name;
    }

    // A provider-issued handle to one async job.
    public class PicoGenJob
    {
        public string JobId;
        public string ProviderId;
        public PicoGenModality Modality;
        public PicoGenJobState State = PicoGenJobState.Unknown;

        public string StatusMessage;
        public string ErrorCode;
        public string ErrorMessage;

        public List<PicoGenResultFile> Files = new();

        // Project-relative asset path after a successful Fetch+import, else null.
        public string ImportedAssetPath;
    }

    public class PicoGenResultFile
    {
        public string Url;
        public string Type;   // e.g. "GLB", "FBX", "PNG"
        public string PreviewUrl;
    }

    public class PicoGenModelInfo
    {
        public string ModelId;
        public string Description;
    }
}
