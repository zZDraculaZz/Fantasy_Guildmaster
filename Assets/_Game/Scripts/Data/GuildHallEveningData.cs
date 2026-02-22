using System;
using System.Collections.Generic;

namespace FantasyGuildmaster.Data
{
    [Serializable]
    public sealed class GuildHallCharacterData
    {
        public string id;
        public string displayName;
        public string talkSceneId;
        public float posX = 0.5f;
        public float posY = 0.5f;
        public string badge = "none";
        public bool enabled = true;
        public string lockedReason;
        public string portraitKey;
    }

    [Serializable]
    public sealed class GuildHallLineData
    {
        public string speaker;
        public string text;
    }

    [Serializable]
    public sealed class GuildHallSceneData
    {
        public string id;
        public List<GuildHallLineData> lines = new();
    }

    [Serializable]
    public sealed class GuildHallEveningData
    {
        public string forcedIntroSceneId;
        public List<GuildHallCharacterData> characters = new();
        public List<GuildHallSceneData> scenes = new();

        public GuildHallSceneData FindScene(string sceneId)
        {
            if (string.IsNullOrWhiteSpace(sceneId) || scenes == null)
            {
                return null;
            }

            for (var i = 0; i < scenes.Count; i++)
            {
                if (scenes[i] != null && string.Equals(scenes[i].id, sceneId, StringComparison.Ordinal))
                {
                    return scenes[i];
                }
            }

            return null;
        }
    }
}
