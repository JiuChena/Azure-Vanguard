using Framework.Gameplay.Abilities;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存 Move 能力的静态配置。</summary>
    [CreateAssetMenu(fileName = "MoveAbility", menuName = "Framework/Gameplay/Abilities/Move")]
    public sealed class MoveAbilitySO : AbilityDefinitionSO
    {
        public float MoveSpeed = 3f;
        
        /// <summary>创建 Move 能力运行时。</summary>
        /// <returns>使用当前配置的 Move 能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new MoveAbilityRuntime(this);
        }
    }
}
