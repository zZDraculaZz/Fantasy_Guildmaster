using System;
using UnityEngine;

namespace FantasyGuildmaster.Core
{
    public sealed class GameState : MonoBehaviour
    {
        [SerializeField] private int startingGold = 100;

        public int Gold { get; private set; }

        public event Action<int> OnGoldChanged;

        private void Awake()
        {
            Gold = Mathf.Max(0, startingGold);
            OnGoldChanged?.Invoke(Gold);
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }
    }
}
