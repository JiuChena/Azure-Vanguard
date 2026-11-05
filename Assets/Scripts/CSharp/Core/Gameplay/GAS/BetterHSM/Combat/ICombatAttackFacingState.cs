namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>
    /// 攻击面向标记（2026-11-06）：只有实现此接口的 HSM 状态允许索敌/转向。
    /// 当前仅 AttackStart 与 Attacking；AttackEnd/Skill/Talent/Idle/Move 不转。
    /// </summary>
    public interface ICombatAttackFacingState
    {
    }
}
