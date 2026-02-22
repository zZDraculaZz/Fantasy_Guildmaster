using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FantasyGuildmaster.Core
{
    public static class GamePauseService
    {
        private static int _pauseCount;
        private static readonly Dictionary<string, int> _reasonsCount = new();

        public static bool IsPaused => _pauseCount > 0;
        public static int Count => _pauseCount;

        public static void Push(string reason)
        {
            _pauseCount++;
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "Unknown";
            }

            if (_reasonsCount.TryGetValue(reason, out var value))
            {
                _reasonsCount[reason] = value + 1;
            }
            else
            {
                _reasonsCount[reason] = 1;
            }

            Debug.Log($"[Pause] Push reason={reason}, count={_pauseCount}, reasons={FormatReasons()} [TODO REMOVE]");
        }

        public static void Pop(string reason)
        {
            _pauseCount = Mathf.Max(0, _pauseCount - 1);
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "Unknown";
            }

            if (_reasonsCount.TryGetValue(reason, out var value))
            {
                value--;
                if (value <= 0)
                {
                    _reasonsCount.Remove(reason);
                }
                else
                {
                    _reasonsCount[reason] = value;
                }
            }

            Debug.Log($"[Pause] Pop reason={reason}, count={_pauseCount}, reasons={FormatReasons()} [TODO REMOVE]");
        }

        public static void ResetAll(string reason)
        {
            _pauseCount = 0;
            _reasonsCount.Clear();
            Debug.LogError($"[Pause] ResetAll reason={reason} [TODO REMOVE]");
        }

        private static string FormatReasons()
        {
            if (_reasonsCount.Count == 0)
            {
                return "none";
            }

            var sb = new StringBuilder();
            var first = true;
            foreach (var pair in _reasonsCount)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                sb.Append(pair.Key).Append(':').Append(pair.Value);
                first = false;
            }

            return sb.ToString();
        }
    }
}
