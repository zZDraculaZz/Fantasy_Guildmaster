using System;
using System.Collections.Generic;

namespace FantasyGuildmaster.Data
{
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
