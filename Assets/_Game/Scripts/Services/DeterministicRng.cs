using UnityEngine;

namespace FantasyGuildmaster.Services
{
    public static class DeterministicRng
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint Hash(params int[] values)
        {
            var hash = FnvOffsetBasis;
            if (values == null)
            {
                return hash;
            }

            for (var i = 0; i < values.Length; i++)
            {
                unchecked
                {
                    hash ^= (uint)values[i];
                    hash *= FnvPrime;
                }
            }

            return hash;
        }

        public static uint HashString(string s)
        {
            var hash = FnvOffsetBasis;
            if (string.IsNullOrEmpty(s))
            {
                return hash;
            }

            for (var i = 0; i < s.Length; i++)
            {
                unchecked
                {
                    hash ^= s[i];
                    hash *= FnvPrime;
                }
            }

            return hash;
        }

        public static float Roll01(uint seed)
        {
            var state = seed;
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
            }

            return (state & 0x00FFFFFFu) / 16777216f;
        }

        public static bool RollSuccess(uint seed, float chance01)
        {
            var clampedChance = Mathf.Clamp(chance01, 0.05f, 0.95f);
            return Roll01(seed) < clampedChance;
        }
    }
}
