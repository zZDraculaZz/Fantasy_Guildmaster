using System;
using System.IO;
using UnityEngine;

namespace FantasyGuildmaster.Data
{
    public static class GameDataLoader
    {
        private const string FileName = "game_data.json";

        public static GameData Load()
        {
            var path = Path.Combine(Application.streamingAssetsPath, FileName);
            if (!File.Exists(path))
            {
                Debug.LogError($"[GameDataLoader] Missing game data file at: {path}");
                return new GameData();
            }

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<GameData>(json);
                return data ?? new GameData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameDataLoader] Failed to load game data: {ex.Message}");
                return new GameData();
            }
        }
    }
}
