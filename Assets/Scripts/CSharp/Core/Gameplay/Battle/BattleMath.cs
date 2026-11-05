using System;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗数值核心（双通道伤害，与技能描述文本一致）：
    /// dmg = max(1, round((atk×rate + atkadd) × 暴击乘区) − def)。
    /// rate/atkadd 由 ①数据层按普攻等级折算（NormalAttackData）。
    /// rng 显式注入（战斗开局发种子）——服务器按 seed+seq 复核的关键：本类无其他随机源；
    /// rng 传 null 视为不暴击（确定性推演用）。round 取 AwayFromZero（常规四舍五入，避免银行家舍入歧义）。
    /// </summary>
    public static class BattleMath
    {
        /// <summary>结算一次伤害（最低 1 点）。</summary>
        public static int CalcDamage(int atk, float rate, float atkAdd, int def,
            float critRate, float critDmg, Random rng)
        {
            float raw = atk * rate + atkAdd;
            bool crit = rng != null && rng.NextDouble() < critRate;
            if (crit) raw *= 1f + critDmg;
            int dmg = (int)Math.Round(raw, MidpointRounding.AwayFromZero) - def;
            return Math.Max(1, dmg);
        }

        /// <summary>结算一次攻击型伤害（2026-11-04：带伤害加成与暴击结果输出，供天赋/统一伤害入口使用）。</summary>
        public static int CalcDamage(int atk, float rate, float atkAdd, int def,
            float critRate, float critDmg, Random rng, float damageBonus, out bool crit)
        {
            float raw = (atk * rate + atkAdd) * (1f + damageBonus);
            crit = rng != null && rng.NextDouble() < critRate;
            if (crit) raw *= 1f + critDmg;
            int dmg = (int)Math.Round(raw, MidpointRounding.AwayFromZero) - def;
            return Math.Max(1, dmg);
        }

        /// <summary>结算一次数值型伤害（追奏等“基础值”伤害：不减防御，可暴击、吃伤害加成）。</summary>
        public static int CalcNumericDamage(float baseValue, float damageBonus,
            float critRate, float critDmg, Random rng, out bool crit)
        {
            float raw = baseValue * (1f + damageBonus);
            crit = rng != null && rng.NextDouble() < critRate;
            if (crit) raw *= 1f + critDmg;
            int dmg = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            return Math.Max(1, dmg);
        }
    }
}
