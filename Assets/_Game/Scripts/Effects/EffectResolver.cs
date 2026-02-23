using System.Collections.Generic;
using FantasyGuildmaster.Services;

namespace FantasyGuildmaster.Effects
{
    public static class EffectResolver
    {
        public static List<ResolvedEffect> Resolve(List<EffectDef> defs, uint baseSeed, int saltStart)
        {
            var resolved = new List<ResolvedEffect>();
            if (defs == null || defs.Count == 0)
            {
                return resolved;
            }

            for (var i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null || string.IsNullOrEmpty(def.type))
                {
                    continue;
                }

                var chance = def.chance < 0 ? 100 : def.chance;
                chance = UnityEngine.Mathf.Clamp(chance, 0, 100);
                if (chance < 100)
                {
                    var seed = DeterministicRng.Hash(
                        unchecked((int)baseSeed),
                        saltStart + i,
                        unchecked((int)DeterministicRng.HashString(def.type)),
                        unchecked((int)DeterministicRng.HashString(def.id)),
                        def.delta,
                        def.tier);
                    var roll = UnityEngine.Mathf.FloorToInt(DeterministicRng.Roll01(seed) * 100f) + 1;
                    if (roll > chance)
                    {
                        continue;
                    }
                }

                resolved.Add(new ResolvedEffect
                {
                    type = def.type,
                    delta = def.delta,
                    id = def.id,
                    tier = def.tier
                });
            }

            return resolved;
        }
    }
}
