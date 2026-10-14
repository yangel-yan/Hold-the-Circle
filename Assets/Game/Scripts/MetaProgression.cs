using UnityEngine;

namespace HoldTheCircle
{
    /// <summary>
    /// Cross-run persistence layer. Shards are the meta currency awarded at
    /// the end of every run (boss clear or death) and spent in the lobby
    /// shrine to unlock permanent perks (Phase 2).
    ///
    /// Phase 1 only persists totals via PlayerPrefs; the lobby UI is a stub
    /// hook so the loop closes end-to-end without blocking on art.
    /// </summary>
    public static class MetaProgression
    {
        private const string k_shardsKey = "HTC.MetaShards";
        private const string k_runsKey = "HTC.RunsCompleted";
        private const string k_bossKillsKey = "HTC.BossKills";

        public static int TotalShards => PlayerPrefs.GetInt(k_shardsKey, 0);
        public static int RunsCompleted => PlayerPrefs.GetInt(k_runsKey, 0);
        public static int BossKills => PlayerPrefs.GetInt(k_bossKillsKey, 0);

        // Award shards at run-end. Saves immediately so a crash does not eat
        // progress.
        public static void AwardShards(int amount)
        {
            if (amount <= 0) return;
            int total = TotalShards + amount;
            PlayerPrefs.SetInt(k_shardsKey, total);
            PlayerPrefs.Save();
        }

        public static void NoteRunCompleted(bool victory)
        {
            int runs = RunsCompleted + 1;
            PlayerPrefs.SetInt(k_runsKey, runs);
            if (victory)
            {
                PlayerPrefs.SetInt(k_bossKillsKey, BossKills + 1);
            }
            PlayerPrefs.Save();
        }

        // Lobby stub: Phase 2 will gate this behind UI confirm. Returns false
        // when the player cannot afford the unlock.
        public static bool TrySpendShards(int amount)
        {
            if (amount <= 0) return true;
            if (TotalShards < amount) return false;
            PlayerPrefs.SetInt(k_shardsKey, TotalShards - amount);
            PlayerPrefs.Save();
            return true;
        }

        // Editor / smoke-test helper. Wipes meta state so a fresh run starts
        // from a known baseline.
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(k_shardsKey);
            PlayerPrefs.DeleteKey(k_runsKey);
            PlayerPrefs.DeleteKey(k_bossKillsKey);
            PlayerPrefs.Save();
        }
    }
}
