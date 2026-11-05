using System.Collections.Generic;

namespace Framework.Gameplay.Battle
{
    /// <summary>全场 Effect 的获取判定上下文：单位尝试获取某 Effect 时传入。</summary>
    public readonly struct TalentAcquireContext
    {
        /// <summary>尝试获取的单位。</summary>
        public readonly BattleUnitState Unit;

        /// <summary>单位阵营。</summary>
        public readonly BattleCamp Camp;

        /// <summary>效果来源单位（如 CH0221）。</summary>
        public readonly BattleUnitState Source;

        /// <summary>战场控制器。</summary>
        public readonly BattleController Battle;

        /// <summary>来源是否存活（倒地/移除后为 false）。</summary>
        public readonly bool SourceAlive;

        /// <summary>该单位已获取的效果 id 集合。</summary>
        public readonly IReadOnlyCollection<string> AcquiredIds;

        public TalentAcquireContext(BattleUnitState unit, BattleCamp camp, BattleUnitState source,
            BattleController battle, bool sourceAlive, IReadOnlyCollection<string> acquiredIds)
        {
            Unit = unit;
            Camp = camp;
            Source = source;
            Battle = battle;
            SourceAlive = sourceAlive;
            AcquiredIds = acquiredIds ?? System.Array.Empty<string>();
        }
    }
}
