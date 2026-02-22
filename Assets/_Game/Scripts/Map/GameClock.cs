using System;
using System.Collections;
using FantasyGuildmaster.Core;
using UnityEngine;

namespace FantasyGuildmaster.Map
{
    public sealed class GameClock : MonoBehaviour
    {
        public event Action<long> TickSecond;

        public int ElapsedSeconds { get; private set; }

        private Coroutine _ticker;

        private void OnEnable()
        {
            SimulationTime.Reset(0);
            ElapsedSeconds = 0;
            _ticker = StartCoroutine(TickCoroutine());
        }

        private void OnDisable()
        {
            if (_ticker != null)
            {
                StopCoroutine(_ticker);
                _ticker = null;
            }
        }

        private IEnumerator TickCoroutine()
        {
            var wait = new WaitForSeconds(1f);
            while (enabled)
            {
                if (GamePauseService.IsPaused)
                {
                    yield return null;
                    continue;
                }

                yield return wait;
                if (GamePauseService.IsPaused)
                {
                    continue;
                }

                ElapsedSeconds++;
                SimulationTime.AdvanceSeconds(1);
                TickSecond?.Invoke(SimulationTime.NowSeconds);
            }
        }
    }
}
