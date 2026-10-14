using System;
using System.Collections.Generic;
using Framework.Gameplay.Unit;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 技能效果结算上下文：执行器消费的全部输入（纯数据，无 Unity Object）。
    /// TargetUnit 与 TargetGuard 二选一（TargetIsGuardPoint 决定）。
    /// </summary>
    public struct SkillEffectContext
    {
        // 施法者
        public BattleUnitState Caster;

        // 目标单位（单体效果 v1；TargetIsGuardPoint=false 时有效）
        public BattleUnitState TargetUnit;

        // 目标守点（TargetIsGuardPoint=true 时有效）
        public GuardPointState TargetGuard;

        public bool TargetIsGuardPoint;

        // 效果行（effectType/value/range 等原始数据）
        public SkillEffectRow Effect;

        // 普攻解析包（rate/atkadd 已按等级折算）
        public BattleDataModule.NormalAttackData Attack;

        // 随机源（暴击判定；null = 不暴击）
        public Random Rng;
    }

    /// <summary>技能效果执行器接口：一种 effectType 一个执行器（同 ItemFeatureCatalog 注册表惯例）。</summary>
    public interface ISkillEffectExecutor
    {
        /// <summary>执行效果（直接改写目标状态；死亡/倒地判定归 BattleController 统一扫描）。</summary>
        void Execute(in SkillEffectContext context);
    }

    /// <summary>
    /// 技能效果目录（①SkillEffectTable 的 effectType → ③执行器分发点，SkillEffectRow 注释预告的落位）。
    /// 首切片只注册 damage/single_target（enemies_in_range 留技能切片，未注册的 effectType 返回 false 由调用方报错）。
    /// </summary>
    public sealed class SkillEffectCatalog
    {
        private readonly Dictionary<string, ISkillEffectExecutor> executors =
            new Dictionary<string, ISkillEffectExecutor>();

        /// <summary>注册执行器（同 effectType 重复注册抛错，装配期即暴露）。</summary>
        public void Register(string effectType, ISkillEffectExecutor executor)
        {
            if (string.IsNullOrEmpty(effectType)) throw new ArgumentException("effectType 为空", nameof(effectType));
            if (executors.ContainsKey(effectType)) throw new InvalidOperationException($"effectType 重复注册：{effectType}");
            executors[effectType] = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>执行一条效果行：未注册的 effectType 返回 false（调用方 LogError 定位）。</summary>
        public bool TryExecute(SkillEffectRow effect, in SkillEffectContext context)
        {
            if (effect == null) return false;
            if (!executors.TryGetValue(effect.EffectType, out ISkillEffectExecutor executor)) return false;
            executor.Execute(in context);
            return true;
        }
    }
}
