namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 全场 Effect 契约（2026-11-04 用户拍板）：每个注册效果自带获取权限判定 CanAcquire(context)。
    /// 新单位登场时由 CombatEffectService 逐个询问，返回 true 才允许获取。
    /// </summary>
    public interface IBattleGlobalEffect
    {
        /// <summary>效果唯一 id（如 talent_CH0221_22104）。</summary>
        string Id { get; }

        /// <summary>效果来源单位（光环拥有者）。</summary>
        BattleUnitState Source { get; }

        /// <summary>Effect 页展示用 buff id（0 = 不展示）。</summary>
        int DisplayBuffId { get; }

        /// <summary>获取权限判定：返回 false 不允许该单位获取本效果。</summary>
        bool CanAcquire(in TalentAcquireContext context);

        /// <summary>单位获取效果（登记光环/监听）。</summary>
        void OnAcquire(BattleUnitState unit);

        /// <summary>单位失去效果（撤加成/清状态）。</summary>
        void OnRelease(BattleUnitState unit);

        /// <summary>持有者造成伤害时回调（追奏等触发型效果用；光环可实现为空）。</summary>
        void OnDamageDealt(in BattleDamageEvent e);
    }
}
