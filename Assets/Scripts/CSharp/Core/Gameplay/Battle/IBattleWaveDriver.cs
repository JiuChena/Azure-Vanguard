using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 波次驱动抽象（③模拟层唯一依赖面，纯 C#）：BattleController.Step 每拍调 Tick 驱动出兵，
    /// 出兵动作经 spawnOne 回调外抛（敌人 id + 等级 + 出怪点坐标 + 行进路线点列，路线可 null）。
    /// 场景侧实现 = CombatManager（Mono），验证侧实现 = 脚本化桩；驱动时钟跟随模拟步进（暂停/倍速天然生效）。
    /// </summary>
    public interface IBattleWaveDriver
    {
        /// <summary>波次总数（HUD 波次 X/Y 的分母）。</summary>
        int TotalWaves { get; }

        /// <summary>当前波号（1 基；全部生成完停在最后一波）。</summary>
        int CurrentWaveNumber { get; }

        /// <summary>是否全部波次已生成完（胜负评估输入）。</summary>
        bool AllSpawned { get; }

        /// <summary>推进一拍：到点条目入创建队列，按创建间隔逐只外抛 spawnOne（路线 = 波次行配给）。</summary>
        void Tick(float dt, Action<string, int, Vector3, IReadOnlyList<Vector3>> spawnOne);
    }
}
