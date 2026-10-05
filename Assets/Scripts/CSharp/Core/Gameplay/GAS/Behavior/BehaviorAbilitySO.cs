using UnityEngine;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存 BE 行为能力的静态配置：为单位创建 BehaviorExecutor 播放宿主。</summary>
    [CreateAssetMenu(fileName = "BehaviorAbility", menuName = "Framework/Gameplay/Abilities/Behavior")]
    public sealed class BehaviorAbilitySO : AbilityDefinitionSO
    {
        // 是否输出 BE 播放头的开始、停止、完成与循环日志。
        [Tooltip("是否输出 BE 播放头的开始/停止/完成/循环流程日志")]
        [SerializeField] private bool _logBehaviorFlow;

        /// <summary>获取是否输出 BE 播放流程日志。</summary>
        public bool LogBehaviorFlow => _logBehaviorFlow;

        /// <summary>创建 BE 行为能力运行时。</summary>
        /// <returns>使用当前配置的 BE 行为能力运行时实例。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new BehaviorAbilityRuntime(this);
        }
    }
}
