using System.Collections.Generic;

namespace FantasyGuildmaster.Effects
{
    public static class EffectFormatter
    {
        public static List<string> FormatLines(List<ResolvedEffect> effects)
        {
            var lines = new List<string>();
            if (effects == null)
            {
                return lines;
            }

            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                lines.Add(Format(effect));
            }

            return lines;
        }

        public static string Format(ResolvedEffect effect)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            switch (effect.type)
            {
                case EffectTypes.Gold:
                    return $"Gold {Signed(effect.delta)}";
                case EffectTypes.Rep:
                    return $"Reputation {Signed(effect.delta)}";
                case EffectTypes.Cohesion:
                    return $"Cohesion {Signed(effect.delta)}";
                case EffectTypes.Exhaust:
                    return "Exhausted";
                case EffectTypes.ClearExhaust:
                    return "Rested";
                case EffectTypes.InjuryAdd:
                    return $"Injury: {Fallback(effect.id, "Unknown")}";
                case EffectTypes.CurseAdd:
                    return $"Curse: {Fallback(effect.id, "Unknown")}";
                case EffectTypes.TagAdd:
                    return $"Tag Added: {Fallback(effect.id, "Unknown")}";
                case EffectTypes.TagRemove:
                    return $"Tag Removed: {Fallback(effect.id, "Unknown")}";
                case EffectTypes.ForcedSceneTrigger:
                    return $"Scene Triggered: {Fallback(effect.id, "Unknown")}";
                default:
                    return $"{effect.type}: {Fallback(effect.id, effect.delta.ToString())}";
            }
        }

        private static string Signed(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString();
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
