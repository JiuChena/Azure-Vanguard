using UnityEngine;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存战斗移动（寻路）能力的静态配置；走停参数在单位 NavMeshAgent 上，本配置无常量。</summary>
    [CreateAssetMenu(fileName = "BattleMoveAbility", menuName = "Framework/Gameplay/Abilities/BattleMove")]
    public sealed class BattleMoveAbilitySO : AbilityDefinitionSO
    {
        /// <summary>创建战斗移动能力运行时。</summary>
        /// <returns>战斗移动能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new BattleMoveAbilityRuntime();
        }
    }
}
