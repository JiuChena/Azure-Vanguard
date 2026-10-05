using Framework.Gameplay.Abilities.Input;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>保存 CH0221 BetterHSM 测试状态共同读取的数据。</summary>
    public sealed class CH0221TestStateContext
    {
        // 当前 CH0221 实体所属的 GAS 上下文。
        private readonly AbilityOwnerContext _ownerContext;

        /// <summary>
        /// 创建 CH0221 测试状态上下文。
        /// </summary>
        /// <param name="ownerContext">当前 CH0221 实体的 GAS 上下文；不允许为 null。</param>
        /// <exception cref="System.ArgumentNullException">GAS 上下文为空时抛出。</exception>
        public CH0221TestStateContext(AbilityOwnerContext ownerContext)
        {
            // 保存由 GAS 传入的当前实体唯一上下文。
            if (ownerContext == null) throw new System.ArgumentNullException(nameof(ownerContext));

            _ownerContext = ownerContext;
        }

        /// <summary>
        /// 获取当前 CH0221 实体所属的 GAS 上下文。
        /// </summary>
        public AbilityOwnerContext OwnerContext => _ownerContext;

        /// <summary>
        /// 判断当前帧是否存在有效的平面移动输入。
        /// </summary>
        /// <returns>输入能力已提供非零移动输入时返回 true；输入能力尚未挂载或尚未初始化时返回 false。</returns>
        public bool HasMoveInput()
        {
            // 测试状态图只读取已有输入 RuntimeData，不要求额外组件或业务能力。
            if (!_ownerContext.TryGet(
                    AbilityRuntimeDataType.Input,
                    out InputRuntimeData inputRuntimeData))
            {
                return false;
            }

            return inputRuntimeData.Move.sqrMagnitude > 0.0001f;
        }

        /// <summary>
        /// 按叶子状态键播放当前实体的 BE 行为；BE 行为能力未挂载时忽略本次请求。
        /// </summary>
        /// <param name="behaviorKey">叶子状态类中声明的 BehaviorKey 常量。</param>
        public void PlayBehavior(string behaviorKey)
        {
            // 状态图不直接依赖 BE 能力：未挂载行为能力时视为表现层缺失，仅提示不中断逻辑。
            if (!_ownerContext.TryGet(
                    AbilityRuntimeDataType.Behavior,
                    out BehaviorAbilityRuntimeData behaviorData))
            {
                Debug.LogWarning($"[BetterHSM Test][CH0221] 未挂载 BE 行为能力，忽略播放请求 '{behaviorKey}'", _ownerContext.Owner);
                return;
            }

            behaviorData.Play(behaviorKey);
        }

        /// <summary>
        /// 停止当前实体的 BE 行为播放；BE 行为能力未挂载时忽略。
        /// </summary>
        public void StopBehavior()
        {
            if (_ownerContext.TryGet(
                    AbilityRuntimeDataType.Behavior,
                    out BehaviorAbilityRuntimeData behaviorData))
            {
                behaviorData.Stop();
            }
        }
    }
}
