using System;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 点数台账（③点数经济核心）：Tick 时间供给累加 + 通用扣费口。
    /// Spend 的 type 口径（Deploy/Revive/BuyBuff/BuyUpgrade）由调用方记账，
    /// 台账只管余额——扩展购买类指令（挂账 #30）不改动本类。
    /// 供给到上限后溢出丢失（明日方舟口径：到顶期间不累计）。
    /// </summary>
    public sealed class PointLedger
    {
        // 当前点数
        public int Current { get; private set; }

        /// <summary>距下次供给的秒数（已到顶恒 0；HUD 补给倒计时用）。</summary>
        public float SecondsToNextSupply => Current >= Cap ? 0f : supplyInterval - supplyTimer;

        // 点数上限（时间供给封顶）
        public int Cap { get; }

        private readonly float supplyInterval;
        private readonly int supplyAmount;
        private float supplyTimer;

        public PointLedger(int initial, float supplyInterval, int supplyAmount, int cap)
        {
            Current = initial;
            this.supplyInterval = supplyInterval;
            this.supplyAmount = supplyAmount;
            Cap = cap;
        }

        /// <summary>时间推进：按间隔供给累加，封顶 clamp。</summary>
        public void Tick(float dt)
        {
            if (Current >= Cap) return;
            supplyTimer += dt;
            if (supplyTimer < supplyInterval) return;
            supplyTimer -= supplyInterval;
            Current = Math.Min(Cap, Current + supplyAmount);
        }

        /// <summary>余额是否足够。</summary>
        public bool CanAfford(int cost) => Current >= cost;

        /// <summary>扣费：足够则扣并返回 true，不足不动余额。</summary>
        public bool TrySpend(int cost)
        {
            if (cost < 0 || Current < cost) return false;
            Current -= cost;
            return true;
        }
    }
}
