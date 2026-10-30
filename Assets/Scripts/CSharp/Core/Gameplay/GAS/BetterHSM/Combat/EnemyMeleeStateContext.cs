namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>近战敌人战斗状态上下文：继承共享战斗上下文的决策标志与 BE 播放入口；敌人专属决策位将来按需加字段。</summary>
    public sealed class EnemyMeleeStateContext : CombatStateContext
    {
        /// <summary>
        /// 创建近战敌人状态上下文。
        /// </summary>
        /// <param name="ownerContext">当前敌人单位的 GAS 上下文；不允许为 null。</param>
        public EnemyMeleeStateContext(AbilityOwnerContext ownerContext) : base(ownerContext)
        {
        }
    }
}
