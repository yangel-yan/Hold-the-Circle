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

// PicoGen_ProviderRegistry.cs
// Routes a modality to a concrete provider. Discovery is automatic via TypeCache
// (mirrors how the Unity MCP tool registry finds [McpTool] methods). Each
// [PicoGenProvider] class is instantiated once and indexed by the modalities it
// claims, ordered by Priority so an override provider can shadow the default.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ByteDance.PICO.Generation.Editor
{
    [InitializeOnLoad]
    public static class PicoGen_ProviderRegistry
    {
        static readonly Dictionary<PicoGenModality, List<IPicoGenProvider>> s_byModality = new();
        static readonly List<IPicoGenProvider> s_all = new();

        static PicoGen_ProviderRegistry()
        {
            Discover();
        }

        static void Discover()
        {
            s_all.Clear();
            s_byModality.Clear();

            var typed = TypeCache.GetTypesWithAttribute<PicoGenProviderAttribute>()
                .Select(t => new
                {
                    Type = t,
                    Priority = (t.GetCustomAttributes(typeof(PicoGenProviderAttribute), false)
                                    .FirstOrDefault() as PicoGenProviderAttribute)?.Priority ?? 100
                })
                .OrderBy(x => x.Priority);

            foreach (var entry in typed)
            {
                if (entry.Type.IsAbstract || !typeof(IPicoGenProvider).IsAssignableFrom(entry.Type))
                    continue;
                IPicoGenProvider instance;
                try { instance = (IPicoGenProvider)Activator.CreateInstance(entry.Type); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PICO Generation] Failed to instantiate provider {entry.Type.Name}: {e.Message}");
                    continue;
                }

                s_all.Add(instance);
                foreach (var m in instance.SupportedModalities)
                {
                    if (!s_byModality.TryGetValue(m, out var list))
                        s_byModality[m] = list = new List<IPicoGenProvider>();
                    list.Add(instance);
                }
            }
        }

        public static IReadOnlyList<IPicoGenProvider> All => s_all;

        // Resolve the provider for a modality. Returns the first READY provider; if
        // none is ready, returns the top-priority one so the caller can surface its
        // notReadyReason; null when no provider claims the modality at all.
        public static IPicoGenProvider Resolve(PicoGenModality modality, out string reason)
        {
            reason = null;
            if (!s_byModality.TryGetValue(modality, out var list) || list.Count == 0)
            {
                reason = $"No generation provider is registered for the '{modality}' modality. " +
                         "Implement IPicoGenProvider + [PicoGenProvider] to add one.";
                return null;
            }

            foreach (var p in list)
                if (p.IsReady(out _)) return p;

            var top = list[0];
            top.IsReady(out reason);
            return top;
        }

        public static IPicoGenProvider ById(string providerId)
            => s_all.FirstOrDefault(p => string.Equals(p.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
    }
}
