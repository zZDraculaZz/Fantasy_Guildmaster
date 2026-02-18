using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyGuildmaster.Data
{
    [Serializable]
    public sealed class GameData
    {
        public List<RegionData> regions = new();
    }

    [Serializable]
    public sealed class RegionData
    {
        public string id;
        public string name;
        public NormalizedPosition pos;
        public int danger;
        public List<string> threats = new();
        public string faction;
        public int travelDays;
        public string iconKey;
    }

    [Serializable]
    public struct NormalizedPosition
    {
        [Range(0f, 1f)] public float x;
        [Range(0f, 1f)] public float y;

        public Vector2 ToVector2()
        {
            return new Vector2(x, y);
        }
    }
}
