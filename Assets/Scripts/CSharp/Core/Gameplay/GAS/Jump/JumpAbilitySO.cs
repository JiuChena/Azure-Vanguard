using Framework.Gameplay.Abilities;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存 Jump 能力的静态配置。</summary>
    [CreateAssetMenu(fileName = "JumpAbility", menuName = "Framework/Gameplay/Abilities/Jump")]
    public sealed class JumpAbilitySO : AbilityDefinitionSO
    {
        public float JumpSpeed = 10f;
        
        /// <summary>创建 Jump 能力运行时。</summary>
        /// <returns>使用当前配置的 Jump 能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new JumpAbilityRuntime(this);
        }
    }
}
