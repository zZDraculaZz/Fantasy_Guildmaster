using System;
using System.Collections.Generic;
using FantasyGuildmaster.Effects;

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
    public sealed class GuildHallTriggerData
    {
        public string type;
        public int day;
        public string sceneId;
    }

    [Serializable]
    public sealed class GuildHallChoiceData
    {
        public string label;
        public List<EffectDef> effects = new();
    }

    [Serializable]
    public sealed class GuildHallForcedSceneData
    {
        public string id;
        public string text;
        public GuildHallTriggerData trigger;
        public List<GuildHallChoiceData> choices = new();
    }

    [Serializable]
    public sealed class GuildHallHubActionData
    {
        public string id;
        public int costAP = 1;
        public string uiText;
        public string desc;
    }

    [Serializable]
    public sealed class GuildHallEveningData
    {
        public string forcedIntroSceneId;
        public List<GuildHallCharacterData> characters = new();
        public List<GuildHallSceneData> scenes = new();
        public List<GuildHallForcedSceneData> forcedScenes = new();
        public List<GuildHallHubActionData> hubActions = new();

        public GuildHallForcedSceneData FindForcedScene(string sceneId)
        {
            if (string.IsNullOrWhiteSpace(sceneId) || forcedScenes == null)
            {
                return null;
            }

            for (var i = 0; i < forcedScenes.Count; i++)
            {
                if (forcedScenes[i] != null && string.Equals(forcedScenes[i].id, sceneId, StringComparison.Ordinal))
                {
                    return forcedScenes[i];
                }
            }

            return null;
        }

        public int GetHubActionCost(string actionId, int defaultCost)
        {
            if (hubActions == null || hubActions.Count == 0)
            {
                return defaultCost;
            }

            for (var i = 0; i < hubActions.Count; i++)
            {
                var action = hubActions[i];
                if (action != null && string.Equals(action.id, actionId, StringComparison.Ordinal))
                {
                    return Math.Max(1, action.costAP);
                }
            }

            return defaultCost;
        }
    }
}
