using System;
using System.Collections.Generic;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Map;
using UnityEngine;

namespace FantasyGuildmaster.Effects
{
    [Serializable]
    public sealed class EffectContext
    {
        public string dayIndex;
        public string contractId;
        public string regionId;
        public string encounterId;
        public string hunterId;
        public string squadId;

        public Func<string, SquadData> resolveSquad;
        public Func<string, HunterData> resolveHunter;
        public Action<int> addGold;
        public Action<int> addRep;
        public Action onStateChanged;
    }

    public static class EffectApplier
    {
        public static void Apply(List<ResolvedEffect> effects, EffectContext ctx)
        {
            if (effects == null || effects.Count == 0)
            {
                return;
            }

            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null || string.IsNullOrEmpty(effect.type))
                {
                    continue;
                }

                ApplyOne(effect, ctx);
            }

            ctx?.onStateChanged?.Invoke();
        }

        private static void ApplyOne(ResolvedEffect effect, EffectContext ctx)
        {
            switch (effect.type)
            {
                case EffectTypes.Gold:
                    ctx?.addGold?.Invoke(effect.delta);
                    break;
                case EffectTypes.Rep:
                    ctx?.addRep?.Invoke(effect.delta);
                    break;
                case EffectTypes.Cohesion:
                {
                    var squad = ctx?.resolveSquad?.Invoke(ctx.squadId);
                    if (squad == null)
                    {
                        Debug.LogWarning($"[Effects] COHESION ignored, missing squad. contract={ctx?.contractId}");
                        return;
                    }

                    squad.cohesion = Mathf.Clamp(squad.cohesion + effect.delta, 0, 100);
                    break;
                }
                case EffectTypes.Exhaust:
                {
                    if (!string.IsNullOrEmpty(ctx?.squadId))
                    {
                        var squad = ctx.resolveSquad?.Invoke(ctx.squadId);
                        if (squad != null)
                        {
                            squad.exhausted = true;
                            squad.exhaustedReason = "Needs rest";
                            return;
                        }
                    }

                    if (!string.IsNullOrEmpty(ctx?.hunterId))
                    {
                        var hunter = ctx.resolveHunter?.Invoke(ctx.hunterId);
                        if (hunter != null)
                        {
                            hunter.exhaustedToday = true;
                            return;
                        }
                    }

                    Debug.LogWarning($"[Effects] EXHAUST ignored, missing target. contract={ctx?.contractId}");
                    break;
                }
                case EffectTypes.ClearExhaust:
                {
                    if (!string.IsNullOrEmpty(ctx?.squadId))
                    {
                        var squad = ctx.resolveSquad?.Invoke(ctx.squadId);
                        if (squad != null)
                        {
                            squad.exhausted = false;
                            squad.exhaustedReason = string.Empty;
                            return;
                        }
                    }

                    if (!string.IsNullOrEmpty(ctx?.hunterId))
                    {
                        var hunter = ctx.resolveHunter?.Invoke(ctx.hunterId);
                        if (hunter != null)
                        {
                            hunter.exhaustedToday = false;
                            return;
                        }
                    }

                    Debug.LogWarning($"[Effects] CLEAR_EXHAUST ignored, missing target. contract={ctx?.contractId}");
                    break;
                }
                case EffectTypes.InjuryAdd:
                case EffectTypes.CurseAdd:
                case EffectTypes.TagAdd:
                case EffectTypes.TagRemove:
                case EffectTypes.ForcedSceneTrigger:
                    Debug.Log($"[Effects] {effect.type} resolved id={effect.id} tier={effect.tier}");
                    break;
                default:
                    Debug.LogWarning($"[Effects] Unknown effect type: {effect.type}");
                    break;
            }
        }
    }
}
