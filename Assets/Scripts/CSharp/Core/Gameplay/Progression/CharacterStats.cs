using System;
using System.Collections.Generic;
using Framework.Gameplay.Abilities.Stat;
using Framework.Gameplay.Unit;
using UnityEngine;

namespace Framework.Gameplay.Progression
{
    /// <summary>
    /// 角色属性结算结果：战斗与养成 UI 的只读属性快照。
    /// </summary>
    public readonly struct CharacterStats
    {
        // 最大生命
        public readonly int MaxHp;

        // 攻击
        public readonly int Atk;

        // 防御
        public readonly int Def;

        // 暴击率（0~1 小数）
        public readonly float CritRate;

        // 暴击伤害加成倍率（暴击时在正常伤害上额外加成的比例，不含 1.0 基数）
        public readonly float CritDmg;

        // 充能效率（乘区系数，1.0 = 100%）：本次回能 = 回能基数 × 充能效率
        public readonly float EnergyRecharge;

        // 伤害加成（加法区，0 = 无加成）：最终伤害 ×（1 + 伤害加成）
        public readonly float DamageBonus;

        // 治疗加成（加法区，0 = 无加成）：治疗量 ×（1 + 治疗加成）；战斗框架接入后消费
        public readonly float HealBonus;

        public CharacterStats(int maxHp, int atk, int def, float critRate, float critDmg,
            float energyRecharge, float damageBonus, float healBonus = 0f)
        {
            MaxHp = maxHp;
            Atk = atk;
            Def = def;
            CritRate = critRate;
            CritDmg = critDmg;
            EnergyRecharge = energyRecharge;
            DamageBonus = damageBonus;
            HealBonus = healBonus;
        }
    }

    /// <summary>
    /// 武器属性贡献：由服务层从武器表解析后传入计算器的纯数据载荷（计算器不碰表）。
    /// 白值并入基数参与百分比乘区（原神式口径，2026-10-08 拍板）；
    /// 主属性作为加成键值对走与 bonus 节点相同的加法区。
    /// </summary>
    public readonly struct WeaponStatContribution
    {
        // 白值类型：atk / def / hp（WeaponRow.BaseStatTypes）；null/空 = 无武器
        public readonly string WhiteType;

        // 已按武器等级结算取整后的白值
        public readonly int WhiteValue;

        // 加成键列表（当前 = 主属性一条；词条为行为效果不进属性）
        public readonly string[] BonusKeys;

        // 与 BonusKeys 一一对应的加成值（已按武器等级结算）
        public readonly float[] BonusValues;

        public WeaponStatContribution(string whiteType, int whiteValue, string[] bonusKeys, float[] bonusValues)
        {
            WhiteType = whiteType;
            WhiteValue = whiteValue;
            BonusKeys = bonusKeys ?? Array.Empty<string>();
            BonusValues = bonusValues ?? Array.Empty<float>();
        }

        /// <summary>是否携带有效武器贡献。</summary>
        public bool HasWeapon => !string.IsNullOrEmpty(WhiteType) && WhiteValue > 0;
    }

    /// <summary>
    /// 装备属性贡献：由服务层从装备表解析后传入计算器的纯数据载荷（计算器不碰表）。
    /// 主 + 3 副词条 + 生效合奏加成依序摊平为键值对；平面键（atk/hp/def）并入基数参与
    /// 百分比乘区（同武器白值口径），百分比键走与 bonus 节点/武器主属性相同的加法区。
    /// </summary>
    public readonly struct TrinketStatContribution
    {
        // 属性键列表（主词条/副词条/合奏加成混排，平面与百分比键混合）
        public readonly string[] Keys;

        // 与 Keys 一一对应的加成值
        public readonly float[] Values;

        public TrinketStatContribution(string[] keys, float[] values)
        {
            Keys = keys ?? Array.Empty<string>();
            Values = values ?? Array.Empty<float>();
        }

        /// <summary>是否携带有效装备贡献。</summary>
        public bool HasTrinkets => Keys != null && Keys.Length > 0;
    }

    /// <summary>
    /// 角色属性计算器：纯函数结算，依赖全部经参数注入（不碰任何单例，可独立测试）。
    /// 公式：最终 = 向下取整((基础 × 等级系数 + 武器白值 + 装备平面词条) × (1 + Σ同键百分比加成))，星级乘区 v1 恒为 1；
    /// 武器白值与装备平面词条在等级系数之后、百分比乘区之前并入基数（原神式口径）；
    /// 暴击/充能效率/伤害加成/治疗加成走加法区（基础常量 + Σ同键加成，武器主属性/bonus 节点/装备百分比词条同区）。
    /// 属性加成只认本角色 bonusIds 列出的 bonus 节点；武器贡献与装备贡献不在此列，直接计入。
    /// 结算拆三段（2026-10-20 stat 能力接入）：白值合成 <see cref="BuildWhiteBase"/>、加成收集
    /// <see cref="CollectBonusModifiers"/>（产出 StatModifier 条目）、组合 <see cref="ComputeStats"/>——
    /// 公式走 StatMath 单一真源，stat 链与本类结果必须逐值一致；老 <see cref="Calculate"/> 组合三段保持行为不变。
    /// </summary>
    public static class CharacterStatsCalculator
    {
        // 属性加成 effectKey 约定值（UnlockNodeTable.effectKey 列，type = bonus 时）
        private const string EffectKeyHpPct = "hp_pct";
        private const string EffectKeyAtkPct = "atk_pct";
        private const string EffectKeyDefPct = "def_pct";
        private const string EffectKeyCritRate = "crit_rate";
        private const string EffectKeyCritDmg = "crit_dmg";
        private const string EffectKeyEnergyRecharge = "energy_recharge";
        private const string EffectKeyDmgBonus = "dmg_bonus";
        private const string EffectKeyHealBonus = "heal_bonus";

        // 暴击率基础值：表无基础暴击列，暂定代码常量（可调）
        private const float BaseCritRate = 0.05f;

        // 暴击伤害基础加成：暴击时额外 +50% 伤害
        private const float BaseCritDmg = 0.5f;

        // 充能效率基础值：100%（本次回能 = 回能基数 × 充能效率；战斗框架接入后消费）
        private const float BaseEnergyRecharge = 1f;

        // 伤害加成基础值：0%（最终伤害 ×(1 + 伤害加成)；战斗框架接入后消费）
        private const float BaseDamageBonus = 0f;

        // 治疗加成基础值：0%（治疗量 ×(1 + 治疗加成)；战斗框架接入后消费）
        private const float BaseHealBonus = 0f;

        /// <summary>
        /// 结算角色属性。
        /// </summary>
        /// <param name="row">角色配置行（1 级白板值）。</param>
        /// <param name="levelMult">等级成长系数（LevelTable.multiplier）。</param>
        /// <param name="unlockedNodeIds">已解锁节点 id 集合。</param>
        /// <param name="nodeLookup">节点查询：id → 行；未命中返回 null 并跳过该节点。</param>
        /// <param name="weapon">武器属性贡献（默认无武器）。</param>
        /// <param name="trinkets">装备属性贡献（默认无装备）。</param>
        /// <param name="star">星级；v1 恒传 1（星级乘区未启用，抽卡接入后扩展）。</param>
        /// <returns>属性结算结果。</returns>
        public static CharacterStats Calculate(CharacterRow row, float levelMult,
            IReadOnlyCollection<int> unlockedNodeIds, Func<int, UnlockNodeRow> nodeLookup,
            WeaponStatContribution weapon = default, TrinketStatContribution trinkets = default, int star = 1)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            return ComputeStats(
                BuildWhiteBase(row, levelMult, weapon, trinkets),
                CollectBonusModifiers(row, unlockedNodeIds ?? Array.Empty<int>(), nodeLookup, weapon, trinkets));
        }

        /// <summary>
        /// 白值合成出口（stat 能力 base 源）：角色基础 × 等级系数 + 武器白值 + 装备平面词条
        /// （原神式口径：数字型贡献全部并入基数段），另带暴击/暴伤/充能/伤加/疗加的基础常量。
        /// </summary>
        public static Dictionary<StatType, float> BuildWhiteBase(CharacterRow row, float levelMult,
            WeaponStatContribution weapon, TrinketStatContribution trinkets)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            // default 载荷不走构造函数、数组为 null（无装备/未穿时服务层返回 default），归一为空数组防 NRE
            string[] trinketKeys = trinkets.Keys ?? Array.Empty<string>();
            float[] trinketValues = trinkets.Values ?? Array.Empty<float>();

            // 装备平面词条（atk/hp/def）并入基数（同武器白值口径）
            double trinketHpFlat = 0.0, trinketAtkFlat = 0.0, trinketDefFlat = 0.0;
            for (int i = 0; i < trinketKeys.Length && i < trinketValues.Length; i++)
            {
                switch (trinketKeys[i])
                {
                    case WeaponRow.BaseStatTypes.Hp: trinketHpFlat += trinketValues[i]; break;
                    case WeaponRow.BaseStatTypes.Atk: trinketAtkFlat += trinketValues[i]; break;
                    case WeaponRow.BaseStatTypes.Def: trinketDefFlat += trinketValues[i]; break;
                }
            }

            // 武器白值在等级系数之后并入基数（原神式口径）
            bool hasWeapon = weapon.HasWeapon;
            double hpBase = row.Hp * (double)levelMult + (hasWeapon && weapon.WhiteType == WeaponRow.BaseStatTypes.Hp ? weapon.WhiteValue : 0) + trinketHpFlat;
            double atkBase = row.Atk * (double)levelMult + (hasWeapon && weapon.WhiteType == WeaponRow.BaseStatTypes.Atk ? weapon.WhiteValue : 0) + trinketAtkFlat;
            double defBase = row.Def * (double)levelMult + (hasWeapon && weapon.WhiteType == WeaponRow.BaseStatTypes.Def ? weapon.WhiteValue : 0) + trinketDefFlat;

            return new Dictionary<StatType, float>
            {
                [StatType.MaxHp] = (float)hpBase,
                [StatType.Atk] = (float)atkBase,
                [StatType.Def] = (float)defBase,
                [StatType.CritRate] = BaseCritRate,
                [StatType.CritDmg] = BaseCritDmg,
                [StatType.EnergyRecharge] = BaseEnergyRecharge,
                [StatType.DamageBonus] = BaseDamageBonus,
                [StatType.HealBonus] = BaseHealBonus,
            };
        }

        /// <summary>
        /// 加成收集出口（stat 能力初始 modifier 源）：bonus 节点（本角色 bonusIds 归属过滤）、武器主属性、
        /// 装备百分比词条依序产出条目（加法区求和与旧实现同序，保证逐值一致）；
        /// 装备平面词条不在此列（并入白值段）。全部条目为 PercentAdd/Flat，Mul 区战斗内词条接入后启用。
        /// </summary>
        public static List<StatModifier> CollectBonusModifiers(CharacterRow row, IReadOnlyCollection<int> unlockedNodeIds,
            Func<int, UnlockNodeRow> nodeLookup, WeaponStatContribution weapon, TrinketStatContribution trinkets)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            var modifiers = new List<StatModifier>();

            // bonus 节点：属性加成只认本角色 bonusIds 列出的节点（外来节点与 talent 节点不计入）
            int[] ownedBonusIds = row.BonusIdList;
            foreach (int nodeId in unlockedNodeIds ?? (IReadOnlyCollection<int>)Array.Empty<int>())
            {
                if (Array.IndexOf(ownedBonusIds, nodeId) < 0) continue;
                UnlockNodeRow node = nodeLookup?.Invoke(nodeId);
                if (node == null || node.Type != UnlockNodeRow.NodeTypes.Bonus) continue;
                if (!MapEffectKey(node.EffectKey, out StatType type, out StatModifier.ModifierType kind))
                {
                    Debug.LogWarning($"未知属性加成键 '{node.EffectKey}'（来源节点 {nodeId}），已跳过");
                    continue;
                }
                modifiers.Add(new StatModifier($"node_{nodeId}", type, kind, node.EffectValue));
            }

            // 武器主属性：与 bonus 节点同键表、同加法区，不受 bonusIds 归属过滤
            string[] weaponBonusKeys = weapon.BonusKeys ?? Array.Empty<string>();
            float[] weaponBonusValues = weapon.BonusValues ?? Array.Empty<float>();
            for (int i = 0; i < weaponBonusKeys.Length && i < weaponBonusValues.Length; i++)
            {
                if (!MapEffectKey(weaponBonusKeys[i], out StatType type, out StatModifier.ModifierType kind))
                {
                    Debug.LogWarning($"未知属性加成键 '{weaponBonusKeys[i]}'（来源：武器主属性），已跳过");
                    continue;
                }
                modifiers.Add(new StatModifier("weapon_main", type, kind, weaponBonusValues[i]));
            }

            // 装备词条按键路由：平面键（atk/hp/def）并入白值段不在此列，百分比键走加法区；
            // 均不受 bonusIds 归属过滤
            string[] trinketKeys = trinkets.Keys ?? Array.Empty<string>();
            float[] trinketValues = trinkets.Values ?? Array.Empty<float>();
            for (int i = 0; i < trinketKeys.Length && i < trinketValues.Length; i++)
            {
                if (trinketKeys[i] == WeaponRow.BaseStatTypes.Hp || trinketKeys[i] == WeaponRow.BaseStatTypes.Atk
                    || trinketKeys[i] == WeaponRow.BaseStatTypes.Def) continue;
                if (!MapEffectKey(trinketKeys[i], out StatType type, out StatModifier.ModifierType kind))
                {
                    Debug.LogWarning($"未知属性加成键 '{trinketKeys[i]}'（来源：装备词条），已跳过");
                    continue;
                }
                modifiers.Add(new StatModifier("trinkets", type, kind, trinketValues[i]));
            }
            return modifiers;
        }

        /// <summary>组合结算：白值 + 加成条目 → CharacterStats；公式走 StatMath 单一真源（与 stat 链共用）。</summary>
        private static CharacterStats ComputeStats(Dictionary<StatType, float> whiteBase, List<StatModifier> modifiers)
        {
            // 星级乘区恒为 1（v1 拍板：战斗恒传 star=1，乘区表随抽卡系统接入）
            // 结算 double 累积，整型三项向下取整（比率属性原样带出）
            return new CharacterStats(
                StatMath.FloorToInt(StatMath.Evaluate(whiteBase[StatType.MaxHp], modifiers, StatType.MaxHp)),
                StatMath.FloorToInt(StatMath.Evaluate(whiteBase[StatType.Atk], modifiers, StatType.Atk)),
                StatMath.FloorToInt(StatMath.Evaluate(whiteBase[StatType.Def], modifiers, StatType.Def)),
                (float)StatMath.Evaluate(whiteBase[StatType.CritRate], modifiers, StatType.CritRate),
                (float)StatMath.Evaluate(whiteBase[StatType.CritDmg], modifiers, StatType.CritDmg),
                (float)StatMath.Evaluate(whiteBase[StatType.EnergyRecharge], modifiers, StatType.EnergyRecharge),
                (float)StatMath.Evaluate(whiteBase[StatType.DamageBonus], modifiers, StatType.DamageBonus),
                (float)StatMath.Evaluate(whiteBase[StatType.HealBonus], modifiers, StatType.HealBonus));
        }

        /// <summary>
        /// 养成加成键 → 属性条目映射：hp/atk/def 百分比走 PercentAdd 乘区；暴击/暴伤/充能/伤加/疗加
        /// 走 Flat 加法区（基础常量同区直加，非乘区——与旧口径逐值一致）。未知键返回 false 由调用方跳过。
        /// </summary>
        private static bool MapEffectKey(string effectKey, out StatType type, out StatModifier.ModifierType kind)
        {
            switch (effectKey)
            {
                case EffectKeyHpPct: type = StatType.MaxHp; kind = StatModifier.ModifierType.PercentAdd; return true;
                case EffectKeyAtkPct: type = StatType.Atk; kind = StatModifier.ModifierType.PercentAdd; return true;
                case EffectKeyDefPct: type = StatType.Def; kind = StatModifier.ModifierType.PercentAdd; return true;
                case EffectKeyCritRate: type = StatType.CritRate; kind = StatModifier.ModifierType.Flat; return true;
                case EffectKeyCritDmg: type = StatType.CritDmg; kind = StatModifier.ModifierType.Flat; return true;
                case EffectKeyEnergyRecharge: type = StatType.EnergyRecharge; kind = StatModifier.ModifierType.Flat; return true;
                case EffectKeyDmgBonus: type = StatType.DamageBonus; kind = StatModifier.ModifierType.Flat; return true;
                case EffectKeyHealBonus: type = StatType.HealBonus; kind = StatModifier.ModifierType.Flat; return true;
                default:
                    type = default;
                    kind = default;
                    return false;
            }
        }
    }
}
