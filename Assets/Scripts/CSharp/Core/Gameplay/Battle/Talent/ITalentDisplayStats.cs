using Framework.Gameplay.Abilities.Stat;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 天赋面板数值展示（2026-11-04）：效果获取者视图就绪后，把光环加成挂到单位 StatRuntimeData，
    /// 让角色面板显示正确的最终数值；战斗结算仍走 TalentAura，避免双算。
    /// </summary>
    public interface ITalentDisplayStats
    {
        /// <summary>把展示用属性加成挂到单位 stat（同来源覆盖，幂等）。</summary>
        void ApplyDisplayStats(StatRuntimeData stats);

        /// <summary>撤掉展示用属性加成（按来源整组撤，幂等）。</summary>
        void RemoveDisplayStats(StatRuntimeData stats);
    }
}
