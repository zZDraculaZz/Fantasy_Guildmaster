using UnityEngine;

namespace FantasyGuildmaster.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        public int Gold { get; private set; }

        public void AddGold(int amount)
        {
            Gold += amount;
        }
    }
}
