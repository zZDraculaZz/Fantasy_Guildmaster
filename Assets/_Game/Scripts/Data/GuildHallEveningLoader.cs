using System;
using System.IO;
using UnityEngine;

namespace FantasyGuildmaster.Data
{
    public static class GuildHallEveningLoader
    {
        private const string FileName = "guildhall_evening.json";

        public static GuildHallEveningData Load()
        {
            var path = Path.Combine(Application.streamingAssetsPath, FileName);
            if (!File.Exists(path))
            {
                Debug.LogError($"[GuildHallLoader] Missing file at: {path}");
                return new GuildHallEveningData();
            }

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<GuildHallEveningData>(json);
                return data ?? new GuildHallEveningData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GuildHallLoader] Failed to load file: {ex.Message}");
                return new GuildHallEveningData();
            }
        }
    }
}
