using System.Collections.Generic;
using Framework.Gameplay.Battle;

namespace Framework.Gameplay.Abilities.Stat
{
    /// <summary>
    /// 属性公式单一真源：final = (base + ΣFlat) × (1 + ΣPercentAdd) × ∏(1 + Mul)。
    /// double 累积、容差取整口径与养成结算一致；StatRuntimeData 重算与养成计算器组合段共用本类，
    /// 两链结果必须逐值一致（验证脚本有对账断言），禁止单侧私改公式。
    /// </summary>
    public static class StatMath
    {
        /// <summary>
        /// 结算单属性的 final 值：按类型分桶汇总条目后依序结算三段。
        /// </summary>
        /// <param name="baseValue">该属性白值（base）。</param>
        /// <param name="modifiers">全部加成条目（内部按目标属性过滤）。</param>
        /// <param name="type">目标属性。</param>
        /// <returns>final 值（double 累积，未取整）。</returns>
        public static double Evaluate(double baseValue, IReadOnlyList<StatModifier> modifiers, StatType type)
        {
            double flat = 0.0, percentAdd = 0.0, mul = 1.0;
            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    StatModifier mod = modifiers[i];
                    if (mod.Type != type) continue;
                    switch (mod.Kind)
                    {
                        case StatModifier.ModifierType.Flat: flat += mod.Value; break;
                        case StatModifier.ModifierType.PercentAdd: percentAdd += mod.Value; break;
                        case StatModifier.ModifierType.Mul: mul *= 1.0 + mod.Value; break;
                    }
                }
            }
            return (baseValue + flat) * (1.0 + percentAdd) * mul;
        }

        /// <summary>
        /// 浮点结算后向下取整：加微小容差抵消表示误差（如表值 1.15 在浮点中略小于真值），
        /// 避免策划口径的整数值因表示误差掉 1 点。
        /// </summary>
        public static int FloorToInt(double value)
        {
            return (int)System.Math.Floor(value + System.Math.Abs(value) * 1e-6 + 1e-4);
        }
    }

    /// <summary>
    /// 单位状态数据（stat 能力运行时数据）：白值 base 只读（构造注入，运行时一切变化走加成条目），
    /// 加成条目注册/注销仅置脏，final 在读取点惰性全量重算（一帧多变合并成一次重算，幂等无副作用）。
    /// 注册/注销/读取是仅有的三个外部入口，重算私有——final 永远从 base+条目全量重推，无增量漂移。
    /// 白值基底由 BattleDataModule 装配期缓存（TryCreateStatData 按基底出独立实例），
    /// CombatManager 出生时注册进 ownerContext（AbilityRuntimeDataType.Stat）。
    /// </summary>
    public sealed class StatRuntimeData : IAbilityRuntimeData
    {
        // 白值（base）：构造时拷贝定死，运行时只读——修改白值 = 走配置/升级重建设数据
        private readonly Dictionary<StatType, float> _base;

        // 加成条目表：进出只置脏，不碰 final
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        // final 缓存：读取点见脏重算
        private readonly Dictionary<StatType, float> _final = new Dictionary<StatType, float>();

        // final 缓存是否过期（注册/注销置 true，重算置 false）
        private bool _dirty = true;

        /// <summary>
        /// 创建状态数据：base 取调用方字典的拷贝（构造后外部修改原字典不影响本数据）。
        /// </summary>
        /// <param name="baseValues">白值表；允许只给部分键，缺失键按 0 结算。</param>
        public StatRuntimeData(IReadOnlyDictionary<StatType, float> baseValues)
        {
            _base = new Dictionary<StatType, float>();
            if (baseValues != null)
                foreach (KeyValuePair<StatType, float> pair in baseValues) _base[pair.Key] = pair.Value;
        }

        /// <summary>读取白值（base）；未配置的属性键返回 0。</summary>
        public float GetBase(StatType type) => _base.TryGetValue(type, out float value) ? value : 0f;

        /// <summary>
        /// 加成条目只读出口：供显示能力等下游按来源分组（buff 汇总）、按属性查影响（高亮）用。
        /// 只读不改状态——一切写仍走 AddModifier/RemoveBySource/Reset。
        /// </summary>
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        /// <summary>
        /// 注册一条加成；同来源允许多条（装备多词条/buff 叠层）。来源为空是编程错误，拒绝并报错。
        /// </summary>
        /// <param name="mod">加成条目。</param>
        public void AddModifier(StatModifier mod)
        {
            if (string.IsNullOrEmpty(mod.SourceId))
            {
                UnityEngine.Debug.LogError($"[Stat] 注册加成被拒绝：来源标识为空（type={mod.Type}）");
                return;
            }
            _modifiers.Add(mod);
            _dirty = true;
        }

        /// <summary>
        /// 按来源注销该来源的全部条目（一件装备/一个 buff 整组撤回）。
        /// </summary>
        /// <param name="sourceId">来源标识。</param>
        /// <returns>实际注销条数。</returns>
        public int RemoveBySource(string sourceId)
        {
            int removed = 0;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].SourceId != sourceId) continue;
                _modifiers.RemoveAt(i);
                removed++;
            }
            if (removed > 0) _dirty = true;
            return removed;
        }

        /// <summary>
        /// 读取 final 值；缓存过期时先全量重算（读取点惰性重算）。
        /// </summary>
        /// <param name="type">目标属性。</param>
        /// <param name="value">final 值；未配置白值且无加成时为 0。</param>
        /// <returns>恒 true（属性集合封闭，统一出 0 值而不是查询失败）。</returns>
        public bool TryGetFinal(StatType type, out float value)
        {
            if (_dirty) RecalculateAll();
            return _final.TryGetValue(type, out value);
        }

        /// <summary>
        /// 产出 ③ 战斗形状快照（BattleUnitStats）：整型三项容差取整，其余直取。
        /// </summary>
        /// <returns>final 快照。</returns>
        public BattleUnitStats GetBattleSnapshot()
        {
            if (_dirty) RecalculateAll();
            return new BattleUnitStats(
                StatMath.FloorToInt(TryGetFinal(StatType.MaxHp, out float maxHp) ? maxHp : 0f),
                StatMath.FloorToInt(TryGetFinal(StatType.Atk, out float atk) ? atk : 0f),
                StatMath.FloorToInt(TryGetFinal(StatType.Def, out float def) ? def : 0f),
                TryGetFinal(StatType.CritRate, out float critRate) ? critRate : 0f,
                TryGetFinal(StatType.CritDmg, out float critDmg) ? critDmg : 0f,
                TryGetFinal(StatType.MoveSpeed, out float moveSpeed) ? moveSpeed : 0f,
                TryGetFinal(StatType.AtkInterval, out float atkInterval) ? atkInterval : 0f,
                TryGetFinal(StatType.Range, out float range) ? range : 0f);
        }

        /// <summary>清空全部加成条目并置脏（白值保留——base 是白值不是运行时状态）。</summary>
        public void Reset()
        {
            _modifiers.Clear();
            _dirty = true;
        }

        // 全量重算：逐属性从 base+条目重推 final（幂等纯函数，多算无害）
        private void RecalculateAll()
        {
            _final.Clear();
            foreach (KeyValuePair<StatType, float> pair in _base)
                _final[pair.Key] = (float)StatMath.Evaluate(pair.Value, _modifiers, pair.Key);
            _dirty = false;
        }
    }
}
