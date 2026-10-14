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
    }
}
