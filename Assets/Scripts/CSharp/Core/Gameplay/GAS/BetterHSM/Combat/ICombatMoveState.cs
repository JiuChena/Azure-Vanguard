namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>战斗移动状态标记接口：寻路能力经 HSM 当前状态是否实现本接口裁决走停，
    /// 不依赖具体泛型状态类型（CombatMoveState&lt;TContext&gt; 实现）。</summary>
    public interface ICombatMoveState
    {
    }
}
