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

// PicoGen_Tools.cs
// PICO asset-generation surface exposed as Unity MCP tools, mirroring the sibling
// PXR_MCP_Tools design (enum-constrained Action, uniform result envelope, boundary
// exception capture).
//
//   pico_generation_object    -> Tencent Hunyuan 3D    (text/image -> GLB)
//   pico_generation_texture   -> Tencent Hunyuan Image (text -> PNG, free trial)
//   pico_generation_animation -> Tencent Hunyuan Motion(text -> FBX; experimental)
//   pico_generation_audio     -> no provider yet (returns not_implemented)
//   pico_generation_status    -> providers + modality coverage snapshot
//
// Async, non-blocking contract shared by all four:
//   submit -> pending + JobId ; status -> poll ; fetch -> download+import ; models.

using System;
using ByteDance.PICO.Generation.Editor;
using Unity.AI.MCP.Editor.ToolRegistry;

namespace ByteDance.PICO.Generation.Tools
{
    public static class PicoGen_Tools
    {
        public enum GenAction { Submit, Status, Fetch, Models }

        // ---- Params ----
        public class ObjectParams
        {
            [McpDescription("Operation: submit, status, fetch, models.", Required = true, EnumType = typeof(GenAction))]
            public string Action { get; set; }
            [McpDescription("For submit: text prompt (text-to-3D). Mutually exclusive with ImageUrl/ImageBase64.")]
            public string Prompt { get; set; }
            [McpDescription("For submit: URL of an input image (image-to-3D). Mutually exclusive with Prompt.")]
            public string ImageUrl { get; set; }
            [McpDescription("For submit: base64 of an input image (image-to-3D). Mutually exclusive with Prompt.")]
            public string ImageBase64 { get; set; }
            [McpDescription("For submit: output format (GLB, OBJ, FBX, USDZ, STL). Default GLB.")]
            public string Format { get; set; }
            [McpDescription("For submit: request PBR materials.", Default = false)]
            public bool EnablePbr { get; set; }
            [McpDescription("For submit/fetch: asset name; drives Assets/Game/Models/{Name}/.")]
            public string Name { get; set; }
            [McpDescription("For status/fetch: the JobId returned by submit.")]
            public string JobId { get; set; }
        }

        public class TextureParams
        {
            [McpDescription("Operation: submit, status, fetch, models.", Required = true, EnumType = typeof(GenAction))]
            public string Action { get; set; }
            [McpDescription("For submit: text prompt describing the texture (max 1024 chars).")]
            public string Prompt { get; set; }
            [McpDescription("For submit/fetch: asset name; drives Assets/Game/Textures/{Name}/.")]
            public string Name { get; set; }
            [McpDescription("For status/fetch: the JobId returned by submit.")]
            public string JobId { get; set; }
        }

        public class AudioParams
        {
            [McpDescription("Operation: submit, status, fetch, models.", Required = true, EnumType = typeof(GenAction))]
            public string Action { get; set; }
            [McpDescription("For submit: text prompt describing the sound/voice.")]
            public string Prompt { get; set; }
            [McpDescription("For submit/fetch: asset name.")]
            public string Name { get; set; }
            [McpDescription("For status/fetch: the JobId returned by submit.")]
            public string JobId { get; set; }
        }

        public class AnimationParams
        {
            [McpDescription("Operation: submit, status, fetch, models.", Required = true, EnumType = typeof(GenAction))]
            public string Action { get; set; }
            [McpDescription("For submit: text prompt describing the motion (max 128 chars).")]
            public string Prompt { get; set; }
            [McpDescription("For submit: motion length in seconds, 1-12 (default 5).", Default = 5)]
            public int DurationSeconds { get; set; }
            [McpDescription("For submit/fetch: asset name; drives Assets/Game/Models/{Name}/.")]
            public string Name { get; set; }
            [McpDescription("For status/fetch: the JobId returned by submit.")]
            public string JobId { get; set; }
        }

        public class StatusParams { /* no parameters */ }

        // ---- Tools ----
        [McpTool("pico_generation_object",
            "Generate a 3D object/model (text-to-3D or image-to-3D) via Tencent Hunyuan 3D. " +
            "ASYNC: action=submit returns a JobId; poll action=status until state=succeeded; then action=fetch imports the GLB into Assets/Game/Models/{Name}/.",
            EnabledByDefault = true)]
        public static object PicoGenerationObject(ObjectParams p)
        {
            try
            {
                return RunModality(PicoGenModality.Object, "pico_generation_object",
                    ParseAction(p?.Action), new PicoGenRequest
                    {
                        Modality = PicoGenModality.Object,
                        Prompt = p?.Prompt, ImageUrl = p?.ImageUrl, ImageBase64 = p?.ImageBase64,
                        Format = p?.Format, EnablePbr = p?.EnablePbr ?? false, Name = p?.Name,
                    }, p?.JobId, p?.Name);
            }
            catch (Exception e) { return PicoGen_Result.FromException("pico_generation_object", e); }
        }

        [McpTool("pico_generation_texture",
            "Generate a 2D texture image via Tencent Hunyuan Image (free trial quota on first activation). " +
            "ASYNC: submit -> status -> fetch imports the PNG into Assets/Game/Textures/{Name}/.",
            EnabledByDefault = true)]
        public static object PicoGenerationTexture(TextureParams p)
        {
            try
            {
                return RunModality(PicoGenModality.Texture, "pico_generation_texture",
                    ParseAction(p?.Action), new PicoGenRequest
                    {
                        Modality = PicoGenModality.Texture, Prompt = p?.Prompt, Name = p?.Name,
                    }, p?.JobId, p?.Name);
            }
            catch (Exception e) { return PicoGen_Result.FromException("pico_generation_texture", e); }
        }

        [McpTool("pico_generation_audio",
            "Generate a sound effect / voice clip. ASYNC: submit -> status -> fetch. " +
            "No audio provider is registered yet (Hunyuan has no audio API); returns not_implemented until one is added.",
            EnabledByDefault = true)]
        public static object PicoGenerationAudio(AudioParams p)
        {
            try
            {
                return RunModality(PicoGenModality.Audio, "pico_generation_audio",
                    ParseAction(p?.Action), new PicoGenRequest
                    {
                        Modality = PicoGenModality.Audio, Prompt = p?.Prompt, Name = p?.Name,
                    }, p?.JobId, p?.Name);
            }
            catch (Exception e) { return PicoGen_Result.FromException("pico_generation_audio", e); }
        }

        [McpTool("pico_generation_animation",
            "Generate a 3D character animation (text-to-motion) via Tencent Hunyuan Motion. Output is an FBX with skeletal animation. " +
            "EXPERIMENTAL: the FBX uses HY-Motion's own rig; retargeting onto another character is up to you. " +
            "ASYNC: submit -> status -> fetch imports the FBX into Assets/Game/Models/{Name}/.",
            EnabledByDefault = true)]
        public static object PicoGenerationAnimation(AnimationParams p)
        {
            try
            {
                return RunModality(PicoGenModality.Animation, "pico_generation_animation",
                    ParseAction(p?.Action), new PicoGenRequest
                    {
                        Modality = PicoGenModality.Animation, Prompt = p?.Prompt,
                        DurationSeconds = p?.DurationSeconds ?? 0, Name = p?.Name,
                    }, p?.JobId, p?.Name);
            }
            catch (Exception e) { return PicoGen_Result.FromException("pico_generation_animation", e); }
        }

        [McpTool("pico_generation_status",
            "Snapshot of the PICO generation layer: registered providers, modality coverage, and readiness.",
            EnabledByDefault = true)]
        public static object PicoGenerationStatus(StatusParams ignored)
        {
            try
            {
                var providers = new System.Collections.Generic.List<object>();
                foreach (var prov in PicoGen_ProviderRegistry.All)
                {
                    prov.IsReady(out var reason);
                    providers.Add(new
                    {
                        providerId = prov.ProviderId,
                        modalities = System.Linq.Enumerable.ToArray(
                            System.Linq.Enumerable.Select(prov.SupportedModalities, m => m.ToString())),
                        ready = prov.IsReady(out _),
                        notReadyReason = reason,
                    });
                }

                var coverage = new System.Collections.Generic.Dictionary<string, bool>();
                foreach (PicoGenModality m in Enum.GetValues(typeof(PicoGenModality)))
                    coverage[m.ToString()] = PicoGen_ProviderRegistry.Resolve(m, out _) != null;

                return PicoGen_Result.Ok("PICO generation status collected.", new { providers, coverage });
            }
            catch (Exception e) { return PicoGen_Result.FromException("pico_generation_status", e); }
        }

        // ---- Shared modality implementation ----
        static PicoGen_Result RunModality(
            PicoGenModality modality, string toolName, GenAction action,
            PicoGenRequest request, string jobId, string name)
        {
            var provider = PicoGen_ProviderRegistry.Resolve(modality, out var resolveReason);
            if (provider == null)
                return PicoGen_Result.NotImplemented($"No provider available for {modality}.", resolveReason);

            switch (action)
            {
                case GenAction.Models:
                {
                    var models = provider.GetModels(modality);
                    return PicoGen_Result.Ok($"{models.Count} model(s) available for {modality}.",
                        new { provider = provider.ProviderId, models });
                }

                case GenAction.Submit:
                {
                    if (!provider.IsReady(out var notReady))
                        return PicoGen_Result.Skipped($"{provider.ProviderId} is not ready.", notReady);

                    var job = provider.Submit(request);
                    if (job.State == PicoGenJobState.Failed)
                        return PicoGen_Result.Error($"Submit failed for {modality}.", job.ErrorMessage ?? job.ErrorCode, job);

                    PicoGen_JobStore.Remember(job, name);
                    return PicoGen_Result.Pending(
                        $"{modality} job submitted (JobId={job.JobId}). Poll action=status until state=succeeded, then action=fetch.", job);
                }

                case GenAction.Status:
                {
                    if (string.IsNullOrWhiteSpace(jobId))
                        return PicoGen_Result.Error("Missing jobId.", "jobId is required for action=status");
                    return DescribeJob(provider.Query(jobId, modality));
                }

                case GenAction.Fetch:
                {
                    if (string.IsNullOrWhiteSpace(jobId))
                        return PicoGen_Result.Error("Missing jobId.", "jobId is required for action=fetch");

                    var job = provider.Query(jobId, modality);
                    if (job.State != PicoGenJobState.Succeeded)
                        return DescribeJob(job);

                    if (job.Files.Count == 0)
                        return PicoGen_Result.Error("Job succeeded but returned no files.", null, job);

                    // Prefer a GLB for models when several formats come back.
                    PicoGenResultFile chosen = null;
                    if (modality == PicoGenModality.Object)
                        foreach (var f in job.Files)
                            if (string.Equals(f.Type, "GLB", StringComparison.OrdinalIgnoreCase)) { chosen = f; break; }
                    chosen ??= job.Files[0];

                    var fetchName = string.IsNullOrWhiteSpace(name)
                        ? (PicoGen_JobStore.TryGet(jobId, out _, out _, out var remembered) ? remembered : null)
                        : name;

                    var path = PicoGen_AssetImporter.Import(chosen, fetchName, modality, out var importErr);
                    if (path == null)
                        return PicoGen_Result.Error("Import failed.", importErr, job);

                    job.ImportedAssetPath = path;
                    PicoGen_JobStore.Forget(jobId);
                    return PicoGen_Result.Ok($"{modality} imported to {path}.", job);
                }
            }

            return PicoGen_Result.Error("Unknown action.", "action must be one of: submit, status, fetch, models");
        }

        static PicoGen_Result DescribeJob(PicoGenJob job)
        {
            switch (job.State)
            {
                case PicoGenJobState.Succeeded:
                    return PicoGen_Result.Ok($"Job {job.JobId} succeeded; call action=fetch to import.", job);
                case PicoGenJobState.Failed:
                    return PicoGen_Result.Error($"Job {job.JobId} failed.", job.ErrorMessage ?? job.ErrorCode, job);
                case PicoGenJobState.Queued:
                case PicoGenJobState.Running:
                    return PicoGen_Result.Pending(
                        $"Job {job.JobId} is {job.State.ToString().ToLowerInvariant()}; keep polling action=status.", job);
                default:
                    return PicoGen_Result.Pending($"Job {job.JobId} state unknown; keep polling.", job);
            }
        }

        static GenAction ParseAction(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new ArgumentException("action is required");
            var normalized = raw.Replace("_", "").Replace("-", "").Replace(" ", "");
            foreach (var name in Enum.GetNames(typeof(GenAction)))
                if (string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase))
                    return (GenAction)Enum.Parse(typeof(GenAction), name);
            throw new ArgumentException($"invalid action '{raw}'; allowed: submit, status, fetch, models");
        }
    }
}
