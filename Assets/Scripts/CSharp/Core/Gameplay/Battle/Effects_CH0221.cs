using System;
using System.Collections.Generic;
using Framework.Gameplay.Abilities;
using Framework.Gameplay.Abilities.Buff;
using Framework.Gameplay.Abilities.Stat;
using Framework.Gameplay.Buff;
using Framework.Gameplay.Player;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// CH0221 效果函数组（2026-10-29 用户拍板：每角色一个静态类，一个函数 = 一条 UI 显示效果）：
    /// 函数注册进 EffectMethodRegistry（三层字典：Character/CH0221/函数名），由 EffectMethodExecuteSO 查表调用。
    /// 函数内给目标 GAS 注册 buff（GAS Effect 能力管生命周期）；数值全部动态读表（SkillEffectTable/BuffTable），代码零数值配置。
    /// </summary>
    public static class Effects_CH0221
    {
        private const string UnitId = "CH0221";
        private const BetterHSMEntityCategory Category = BetterHSMEntityCategory.Character;

        // ---- 注册（静态构造由 EffectMethodRegistry 调用） ----

        public static void Register()
        {
            EffectMethodRegistry.Register(Category, UnitId, "NormalAttack", ApplyNormalAttack);
            EffectMethodRegistry.Register(Category, UnitId, "HealingChord", ApplyHealingChord);
            EffectMethodRegistry.Register(Category, UnitId, "EBuff", ApplyEBuff);
            EffectMethodRegistry.Register(Category, UnitId, "EXBuff", ApplyEXBuff);
            EffectMethodRegistry.Register(Category, UnitId, "EXHeal", ApplyEXHeal);
            EffectMethodRegistry.Register(Category, UnitId, "Harmony", ApplyHarmony);
        }

        // ---- 效果函数（一个函数 = 一条显示效果） ----

        // 普攻伤害：当前攻击目标（倍率 = 表 2210101 value × 等级成长）
        public static void ApplyNormalAttack(SkillEffectContext ctx)
        {
            BattleUnitState target = CurrentTarget(ctx);
            if (target == null || !target.IsAlive) return;
            int level = ResolveSkillLevel(ctx.Caster);
            float rate = EffectValue(ctx.Caster, 2210101, level); int dmg = BattleMath.CalcDamage(ctx.Caster.Stats.Atk, rate, 0f, target.Stats.Def,
                ctx.Caster.Stats.CritRate, ctx.Caster.Stats.CritDmg, null);
            target.Hp = Math.Max(0, target.Hp - dmg);
        }

        // E 治疗（Healing Chord）：治疗自身 + 全队 HP 最低目标（heal = 表 2210204 value × MaxHp；充能效率乘区挂账）
        public static void ApplyHealingChord(SkillEffectContext ctx)
        {
            int level = ResolveSkillLevel(ctx.Caster);
            float healRatio = EffectValue(ctx.Caster, 2210204, level);

            HealTarget(ctx, ctx.Caster, healRatio);
            BattleUnitState lowest = LowestHpTeammate(ctx, ctx.Caster);
            if (lowest != null) HealTarget(ctx, lowest, healRatio);
        }

        // E 拐 buff（三条随机取一：BuffTable 221/222/223，施加自身）
        public static void ApplyEBuff(SkillEffectContext ctx)
        {
            int[] candidates = { 221, 222, 223 };
            int picked = candidates[UnityEngine.Random.Range(0, candidates.Length)];
            ApplyBuffToTarget(ctx, ctx.Caster, picked, ResolveSkillLevel(ctx.Caster));
        }

        // EX 范围拐：范围内队友（team_in_range）加双暴组合 buff（BuffTable 230 = critrate+critdmg）
        public static void ApplyEXBuff(SkillEffectContext ctx)
        {
            int level = ResolveSkillLevel(ctx.Caster);
            float range = 8f; // 演奏半径（占位，数值待调）
            foreach (BattleUnitState t in QueryTeamInRange(ctx, range))
            {
                ApplyBuffToTarget(ctx, t, 230, level);
            }
        }

        // EX 濒危治疗：Hp<50% 的队友治疗（heal = 表 2210303 value × MaxHp）
        public static void ApplyEXHeal(SkillEffectContext ctx)
        {
            int level = ResolveSkillLevel(ctx.Caster);
            float healRatio = EffectValue(ctx.Caster, 2210303, level);
            foreach (BattleUnitState t in QueryTeamLowHp(ctx, ctx.Caster, 0.5f))
            {
                HealTarget(ctx, t, healRatio);
            }
        }

        // 被动（Harmony）：全队充能效率 + 增伤——常驻被动，非 clip 事件效果（挂账，后续被动系统接入）
        public static void ApplyHarmony(SkillEffectContext ctx)
        {
            Debug.Log("[Effects_CH0221] Harmony 被动：常驻效果，非事件触发——被动系统接入后实现。");
        }

        // ---- 共享 helper ----

        // 施加 buff 到目标（GAS Effect 能力管生命周期；BuffTable 多属性组合）
        private static void ApplyBuffToTarget(SkillEffectContext ctx, BattleUnitState target, int buffId, int level)
        {
            BuffRuntimeData buffData = ResolveBuffData(ctx, target);
            if (buffData == null) return;
            if (!BuffDataLibrary.TryGetBuffModifiers(buffId, out List<StatType> types, out List<float> values,
                    out List<float> growths, out float duration))
            {
                return;
            }
            var attributes = new (StatType, float)[types.Count];
            for (int i = 0; i < types.Count; i++)
            {
                attributes[i] = (types[i], values[i] + growths[i] * (level - 1));
            }
            buffData.ApplyBuff(new StatBuff(buffId, duration, attributes));
        }

        // 目标 GAS：施放者 = OwnerContext；其他目标 = FieldUnitRegistry 视图 → GAS
        private static BuffRuntimeData ResolveBuffData(SkillEffectContext ctx, BattleUnitState target)
        {
            if (target == ctx.Caster)
            {
                return ctx.OwnerContext != null
                    && ctx.OwnerContext.TryGet(AbilityRuntimeDataType.Buff, out BuffRuntimeData self) ? self : null;
            }
            if (FieldUnitRegistry.TryGetHead(target.UnitId, out Transform head))
            {
                GameplayAbilitySystem gas = head.GetComponentInParent<GameplayAbilitySystem>();
                if (gas != null && gas.OwnerContext != null
                    && gas.OwnerContext.TryGet(AbilityRuntimeDataType.Buff, out BuffRuntimeData targetBuff))
                {
                    return targetBuff;
                }
            }
            return null;
        }

        // 治疗：heal = ratio × MaxHp，clamp 上限
        private static void HealTarget(SkillEffectContext ctx, BattleUnitState target, float ratio)
        {
            if (target == null || !target.IsAlive) return;
            int heal = Mathf.RoundToInt(ratio * target.Stats.MaxHp);
            target.Hp = Math.Min(target.Stats.MaxHp, target.Hp + heal);
        }

        // 当前攻击目标
        private static BattleUnitState CurrentTarget(SkillEffectContext ctx)
        {
            if (string.IsNullOrEmpty(ctx.Caster.TargetUnitId) || ctx.Controller == null) return null;
            IReadOnlyList<BattleUnitState> units = ctx.Controller.Units;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].UnitId == ctx.Caster.TargetUnitId) return units[i];
            }
            return null;
        }

        // 全队 Hp 最低队友（不含自身）
        private static BattleUnitState LowestHpTeammate(SkillEffectContext ctx, BattleUnitState caster)
        {
            if (ctx.Controller == null) return null;
            BattleUnitState lowest = null;
            float lowestHp = float.MaxValue;
            IReadOnlyList<BattleUnitState> units = ctx.Controller.Units;
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState u = units[i];
                if (u.Camp == BattleCamp.Character && u != caster && u.IsAlive && u.Hp < lowestHp)
                {
                    lowestHp = u.Hp;
                    lowest = u;
                }
            }
            return lowest;
        }

        // 范围内队友（team_in_range，不含自身）
        private static IEnumerable<BattleUnitState> QueryTeamInRange(SkillEffectContext ctx, float range)
        {
            if (ctx.Controller == null) yield break;
            float rangeSq = range * range;
            IReadOnlyList<BattleUnitState> units = ctx.Controller.Units;
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState u = units[i];
                if (u.Camp == BattleCamp.Character && u != ctx.Caster && u.IsAlive
                    && (u.Position - ctx.Caster.Position).sqrMagnitude <= rangeSq)
                {
                    yield return u;
                }
            }
        }

        // 全队 Hp < 阈值的队友（不含自身）
        private static IEnumerable<BattleUnitState> QueryTeamLowHp(SkillEffectContext ctx, BattleUnitState caster, float threshold)
        {
            if (ctx.Controller == null) yield break;
            IReadOnlyList<BattleUnitState> units = ctx.Controller.Units;
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState u = units[i];
                if (u.Camp == BattleCamp.Character && u != caster && u.IsAlive
                    && u.Hp < threshold * u.Stats.MaxHp)
                {
                    yield return u;
                }
            }
        }

        // 技能等级（CastingSkillId → 槽位等级，仓库缺省 1）
        private static int ResolveSkillLevel(BattleUnitState caster)
        {
            if (caster.CastingSkillId == caster.Stats.SkillEId) return EntryLevel(caster, e => e.skillLevelE);
            if (caster.CastingSkillId == caster.Stats.SkillQId) return EntryLevel(caster, q => q.skillLevelQ);
            if (caster.CastingSkillId == caster.Stats.NormalAtkId) return EntryLevel(caster, n => n.skillLevelNormal);
            return 1;
        }

        private static int EntryLevel(BattleUnitState caster, Func<CharacterEntry, int> selector)
        {
            return CharacterWarehouseManager.Instance.TryGetEntry(caster.UnitId, out CharacterEntry entry)
                ? Math.Max(1, selector(entry))
                : 1;
        }

        // 效果数值（表效果行 value × 等级成长）
        private static float EffectValue(BattleUnitState caster, int effectId, int level)
        {
            return BattleDataModule.TryGetSkillEffect(effectId, out Framework.Gameplay.Unit.SkillEffectRow row)
                ? row.GetValueAtLevel(level)
                : 0f;
        }
    }
}
