using UnityEngine;

namespace FantasyGuildmaster.Core
{
    public static class GamePauseService
    {
        private static int _pauseCount;

        public static bool IsPaused => _pauseCount > 0;

        public static void Push(string reason)
        {
            _pauseCount++;
            Debug.Log($"[Pause] Push reason={reason}, count={_pauseCount} [TODO REMOVE]");
        }

        public static void Pop(string reason)
        {
            if (_pauseCount > 0)
            {
                _pauseCount--;
            }

            Debug.Log($"[Pause] Pop reason={reason}, count={_pauseCount} [TODO REMOVE]");
        }
    }
}
