using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>保存 Sprint 能力向其他能力公开的运行时数据。</summary>
    public sealed class SprintAbilityRuntimeData : IAbilityRuntimeData
    {
        public int remainSprintCount = 0;
        public Vector3 sprintDirection = Vector3.zero;
        
        /// <summary>清空 Sprint 能力共享数据。</summary>
        public void Reset()
        {
        }
    }
}
