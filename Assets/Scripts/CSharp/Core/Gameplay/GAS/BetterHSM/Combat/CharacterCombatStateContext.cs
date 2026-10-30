namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>角色战斗状态上下文：继承共享战斗上下文的决策标志与 BE 播放入口；角色专属决策位将来按需加字段。</summary>
    public sealed class CharacterCombatStateContext : CombatStateContext
    {
        /// <summary>
        /// 创建角色状态上下文。
        /// </summary>
        /// <param name="ownerContext">当前角色单位的 GAS 上下文；不允许为 null。</param>
        public CharacterCombatStateContext(AbilityOwnerContext ownerContext) : base(ownerContext)
        {
        }
    }
}
