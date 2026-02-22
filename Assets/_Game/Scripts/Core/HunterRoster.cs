using System.Collections.Generic;
using UnityEngine;

namespace FantasyGuildmaster.Core
{
    public sealed class HunterRoster : MonoBehaviour
    {
        [SerializeField] private List<HunterData> hunters = new();

        public List<HunterData> Hunters => hunters;

        public HunterData GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (var i = 0; i < hunters.Count; i++)
            {
                if (hunters[i] != null && hunters[i].id == id) return hunters[i];
            }
            return null;
        }

        public List<HunterData> GetHuntersInSquad(string squadId)
        {
            var list = new List<HunterData>();
            for (var i = 0; i < hunters.Count; i++)
            {
                var h = hunters[i];
                if (h != null && h.squadId == squadId)
                {
                    list.Add(h);
                }
            }
            return list;
        }

        public List<HunterData> GetFreeHunters()
        {
            var list = new List<HunterData>();
            for (var i = 0; i < hunters.Count; i++)
            {
                var h = hunters[i];
                if (h != null && !h.loneWolf && string.IsNullOrEmpty(h.squadId))
                {
                    list.Add(h);
                }
            }
            return list;
        }

        public List<HunterData> GetSoloHunters()
        {
            var list = new List<HunterData>();
            for (var i = 0; i < hunters.Count; i++)
            {
                var h = hunters[i];
                if (h != null && string.IsNullOrEmpty(h.squadId))
                {
                    list.Add(h);
                }
            }
            return list;
        }

        public void EnsureSeededDefaultHunters(int dayIndex)
        {
            if (hunters.Count > 0) return;

            AddHunter("hunter_1", "Rhea", HunterRank.D, false, dayIndex);
            AddHunter("hunter_2", "Brom", HunterRank.D, false, dayIndex);
            AddHunter("hunter_3", "Kael", HunterRank.C, false, dayIndex);
            AddHunter("hunter_4", "Mira", HunterRank.C, false, dayIndex);
            AddHunter("hunter_5", "Soren", HunterRank.D, false, dayIndex);
            AddHunter("hunter_6", "Tess", HunterRank.C, false, dayIndex);
            AddHunter("hunter_7", "Vex", HunterRank.C, true, dayIndex);
            AddHunter("hunter_8", "Nyx", HunterRank.B, true, dayIndex);
            AddHunter("hunter_9", "Orin", HunterRank.C, true, dayIndex);

            Debug.Log("[HunterRoster] Seeded hunters: 9, loneWolves=3 [TODO REMOVE]");
        }

        private void AddHunter(string id, string name, HunterRank rank, bool loneWolf, int dayIndex)
        {
            hunters.Add(new HunterData
            {
                id = id,
                name = name,
                rank = rank,
                loneWolf = loneWolf,
                hp = 100,
                maxHp = 100,
                joinedDay = dayIndex,
                squadId = null,
                exhaustedToday = false
            });
        }
    }
}
