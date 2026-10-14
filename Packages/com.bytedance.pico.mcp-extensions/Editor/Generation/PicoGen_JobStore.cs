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

// PicoGen_JobStore.cs
// Remembers submitted jobs so a caller can poll them across editor recompiles /
// domain reloads. Backed by SessionState (survives a domain reload, cleared on
// editor restart). Generation JobIds are valid server-side for hours, so a caller
// that lost the SessionState can still Status by raw JobId.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ByteDance.PICO.Generation.Editor
{
    public static class PicoGen_JobStore
    {
        const string k_key = "PICO.Generation.Jobs";

        [System.Serializable]
        class Entry
        {
            public string jobId;
            public string providerId;
            public int modality;
            public string name;
        }

        [System.Serializable]
        class Wrapper { public List<Entry> items = new(); }

        public static void Remember(PicoGenJob job, string name)
        {
            if (job == null || string.IsNullOrEmpty(job.JobId)) return;
            var w = Load();
            w.items.RemoveAll(e => e.jobId == job.JobId);
            w.items.Add(new Entry
            {
                jobId = job.JobId,
                providerId = job.ProviderId,
                modality = (int)job.Modality,
                name = name,
            });
            Save(w);
        }

        public static void Forget(string jobId)
        {
            var w = Load();
            if (w.items.RemoveAll(e => e.jobId == jobId) > 0) Save(w);
        }

        public static bool TryGet(string jobId, out string providerId, out PicoGenModality modality, out string name)
        {
            providerId = null; modality = default; name = null;
            var e = Load().items.FirstOrDefault(x => x.jobId == jobId);
            if (e == null) return false;
            providerId = e.providerId;
            modality = (PicoGenModality)e.modality;
            name = e.name;
            return true;
        }

        public static List<string> AllJobIds() => Load().items.Select(e => e.jobId).ToList();

        static Wrapper Load()
        {
            var raw = SessionState.GetString(k_key, "");
            if (string.IsNullOrEmpty(raw)) return new Wrapper();
            try { return JsonUtility.FromJson<Wrapper>(raw) ?? new Wrapper(); }
            catch { return new Wrapper(); }
        }

        static void Save(Wrapper w) => SessionState.SetString(k_key, JsonUtility.ToJson(w));
    }
}
