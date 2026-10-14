namespace Framework.Gameplay.Battle
{
    /// <summary>守点模拟状态（纯 C#）：HP 与破损判定；位置等场景数据归 ②表现层，③只持有数值。</summary>
    public sealed class GuardPointState
    {
        public int MaxHp { get; }

        public int Hp { get; private set; }

        // 守点是否已破（胜负评估输入）
        public bool IsBroken => Hp <= 0;

        public GuardPointState(int maxHp)
        {
            MaxHp = maxHp;
            Hp = maxHp;
        }

        /// <summary>扣血（clamp 到 0；不处理复活——守点不可修复，胜负口径）。</summary>
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            Hp = System.Math.Max(0, Hp - amount);
        }
    }
}
