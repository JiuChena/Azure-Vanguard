using Framework.Gameplay.Abilities;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存 Sprint 能力的静态配置。</summary>
    [CreateAssetMenu(fileName = "SprintAbility", menuName = "Framework/Gameplay/Abilities/Sprint")]
    public sealed class SprintAbilitySO : AbilityDefinitionSO
    {
        public float sprintDistance = 2f;
        public int sprintCount = 60;
        
        /// <summary>创建 Sprint 能力运行时。</summary>
        /// <returns>使用当前配置的 Sprint 能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new SprintAbilityRuntime(this);
        }
    }
}
