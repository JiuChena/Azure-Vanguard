using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗资源子服务：点数台账供给/扣费、技能 CD 节拍、能量入账/消耗。
    /// 所有计时跟随模拟 Step，暂停/倍速天然生效。
    /// </summary>
    public sealed class CombatResourceService : ICombatSubservice
    {
        // 所属战斗服务（能量结算读光环）。
        private CombatService service;

        /// <summary>点数台账（每场新建；Reset 置空）。</summary>
        public PointLedger Ledger { get; private set; }

        /// <summary>技能 CD 管理器（常驻实例，Reset 清空）。</summary>
        public SkillCooldownManager Cooldowns { get; } = new SkillCooldownManager();

        /// <summary>当前点数（无战斗时为 0）。</summary>
        public int Points => Ledger != null ? Ledger.Current : 0;

        /// <summary>距下次点数补给的秒数（无战斗/到顶为 0）。</summary>
        public float SecondsToNextSupply => Ledger != null ? Ledger.SecondsToNextSupply : 0f;

        /// <summary>绑定战斗服务与点数台账（重复调用先清空）。</summary>
        /// <param name="combatService">所属战斗服务；不允许为 null。</param>
        /// <param name="ledger">点数台账；不允许为 null。</param>
        public void Begin(CombatService combatService, PointLedger ledger)
        {
            Reset();
            service = combatService;
            Ledger = ledger;
        }

        /// <summary>清空点数台账与全部 CD 记录。</summary>
        public void Reset()
        {
            Ledger = null;
            Cooldowns.Clear();
            service = null;
        }

        /// <summary>推进一拍：点数供给 + 技能 CD。</summary>
        /// <param name="dt">本拍时长（秒）。</param>
        public void Tick(float dt)
        {
            Ledger?.Tick(dt);
            Cooldowns.Tick(dt);
        }

        /// <summary>尝试扣费（余额不足返回 false，不动余额）。</summary>
        /// <param name="cost">费用。</param>
        /// <returns>扣费成功返回 true。</returns>
        public bool TrySpend(int cost)
        {
            return Ledger != null && Ledger.TrySpend(cost);
        }

        /// <summary>能量入账（受光环充能效率乘区，clamp 到单位上限）。</summary>
        /// <param name="unit">目标单位。</param>
        /// <param name="amount">基础回能量。</param>
        public void AddEnergy(BattleUnitState unit, int amount)
        {
            if (unit == null || !unit.IsAlive || unit.MaxEnergy <= 0 || amount <= 0) return;
            float aura = service != null && service.Effects != null
                ? service.Effects.GetEnergyRecharge(unit.UnitId)
                : 0f;
            float energyRecharge = unit.Stats.EnergyRecharge + aura;
            int gained = Mathf.RoundToInt(amount * energyRecharge);
            if (gained <= 0) return;
            unit.Energy = Mathf.Min(unit.MaxEnergy, unit.Energy + gained);
        }

        /// <summary>消耗能量（Q 技能等；返回实际扣除量）。</summary>
        /// <param name="unit">目标单位。</param>
        /// <param name="amount">请求消耗量。</param>
        /// <returns>实际扣除量。</returns>
        public int SpendEnergy(BattleUnitState unit, int amount)
        {
            if (unit == null || unit.MaxEnergy <= 0 || amount <= 0) return 0;
            int actual = Mathf.Min(unit.Energy, amount);
            unit.Energy -= actual;
            return actual;
        }
    }
}
