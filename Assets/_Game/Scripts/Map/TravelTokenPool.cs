using System.Collections.Generic;
using UnityEngine;

namespace FantasyGuildmaster.Map
{
    public sealed class TravelTokenPool
    {
        private readonly TravelToken _prefab;
        private readonly Transform _parent;
        private readonly Stack<TravelToken> _stack;

        public TravelTokenPool(TravelToken prefab, Transform parent, int preload)
        {
            _prefab = prefab;
            _parent = parent;
            _stack = new Stack<TravelToken>(preload);

            for (var i = 0; i < preload; i++)
            {
                var token = Create();
                token.gameObject.SetActive(false);
                _stack.Push(token);
            }
        }

        public TravelToken Get()
        {
            if (_stack.Count > 0)
            {
                var token = _stack.Pop();
                token.gameObject.SetActive(true);
                return token;
            }

            return Create();
        }

        public void Return(TravelToken token)
        {
            if (token == null)
            {
                return;
            }

            token.gameObject.SetActive(false);
            token.transform.SetParent(_parent, false);
            _stack.Push(token);
        }

        private TravelToken Create()
        {
            return Object.Instantiate(_prefab, _parent);
        }
    }
}
