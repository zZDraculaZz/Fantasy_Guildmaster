using System.Collections.Generic;
using UnityEngine;

namespace FantasyGuildmaster.Map
{
    public sealed class ContractIconPool
    {
        private readonly ContractIcon _prefab;
        private readonly Transform _parent;
        private readonly Stack<ContractIcon> _pool;

        public ContractIconPool(ContractIcon prefab, Transform parent, int preloadCount)
        {
            _prefab = prefab;
            _parent = parent;
            _pool = new Stack<ContractIcon>(preloadCount);

            for (var i = 0; i < preloadCount; i++)
            {
                var icon = CreateInstance();
                icon.gameObject.SetActive(false);
                _pool.Push(icon);
            }
        }

        public ContractIcon Get()
        {
            if (_pool.Count > 0)
            {
                var icon = _pool.Pop();
                icon.gameObject.SetActive(true);
                return icon;
            }

            return CreateInstance();
        }

        public void Return(ContractIcon icon)
        {
            if (icon == null)
            {
                return;
            }

            icon.gameObject.SetActive(false);
            icon.transform.SetParent(_parent, false);
            _pool.Push(icon);
        }

        private ContractIcon CreateInstance()
        {
            return Object.Instantiate(_prefab, _parent);
        }
    }
}
