using UnityEngine;

namespace Framework.Gameplay.Abilities.Stat
{
    /// <summary>状态（stat）能力配置：单位出生后由运行时按 id 从战斗数据模块装配独立状态数据并注册进上下文。
    /// 首切片无可调字段（白值基底全从表来）；战斗内 buff 驱动（每拍到期/衰减）接入后在此扩配置。</summary>
    [CreateAssetMenu(fileName = "StatAbility", menuName = "Framework/Gameplay/Abilities/Stat")]
    public sealed class StatAbilitySO : AbilityDefinitionSO
    {
        /// <summary>创建状态能力运行时。</summary>
        /// <returns>使用当前配置的状态能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new StatAbilityRuntime(this);
        }
    }
}
